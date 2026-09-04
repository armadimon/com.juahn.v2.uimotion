using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 다른 그래프를 노드 하나처럼 실행한다. 자주 쓰는 관용구를 한 곳에서 고치기 위한 재사용 단위.
    ///
    /// <b>추상 클래스다.</b> 코어는 그래프 에셋을 참조할 수 없으므로 대상 해석을 파생에 맡긴다 —
    /// Unity 계층이 <c>MotionGraph</c> 필드를 든 파생을 제공한다.
    /// </summary>
    [Serializable]
    public abstract class SubGraphNode : MotionFlowNode
    {
        /// <summary>한 번의 실행에서 허용하는 최대 중첩 깊이. 손으로 만든 순환 에셋에 대한 방어.</summary>
        public const int MaxDepth = 8;

        /// <summary>안쪽 그래프에서 발사할 트리거.</summary>
        public string EntryTrigger = MotionRuntime.StartTrigger;

        /// <summary>대상 그래프. 없으면 null.</summary>
        protected abstract IMotionGraphView ResolveGraph();

        /// <summary>검사 도구가 대상 그래프를 들여다보는 창구.</summary>
        public IMotionGraphView PeekGraph()
        {
            return ResolveGraph();
        }

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            if (ctx == null)
            {
                return MotionHandle.Skipped;
            }

            if (ctx.Depth >= MaxDepth)
            {
                Warn(ctx, "sub-graph nesting exceeded " + MaxDepth + " levels; possible cycle");
                return MotionHandle.Skipped;
            }

            IMotionGraphView inner = ResolveGraph();
            if (inner == null)
            {
                Warn(ctx, "sub-graph node #" + Id.Value + " has no target graph");
                return MotionHandle.Skipped;
            }

            NodeId entry = inner.GetEntry(EntryTrigger);
            if (!entry.IsValid)
            {
                Warn(ctx, "sub-graph '" + inner.GraphName + "' has no trigger '" + EntryTrigger + "'");
                return MotionHandle.Skipped;
            }

            var innerScope = new MotionScope(EntryTrigger, ctx.Log);

            // 바깥 스코프가 취소되면 안쪽도 취소돼야 한다. 안 그러면 안쪽 복구가 영영 실행되지 않는다.
            ctx.Scope.Remember(innerScope.Cancel);

            innerScope.Begin(
                new MotionContext(inner, innerScope, new ContextSlotResolver(ctx), ctx.Log, ctx.Triggers,
                    ctx.Depth + 1),
                entry);

            return new SubGraphHandle(innerScope);
        }

        private static void Warn(IMotionContext ctx, string message)
        {
            if (ctx.Log != null)
            {
                ctx.Log.Warn(message);
            }
        }

        /// <summary>슬롯은 바깥과 공유한다 — 같은 플레이어의 같은 계층이기 때문이다.</summary>
        private sealed class ContextSlotResolver : ISlotResolver
        {
            private readonly IMotionContext _outer;

            public ContextSlotResolver(IMotionContext outer)
            {
                _outer = outer;
            }

            public object Resolve(SlotRef slot)
            {
                return _outer.ResolveSlot(slot);
            }
        }

        private sealed class SubGraphHandle : IMotionHandle
        {
            private readonly MotionScope _inner;

            public SubGraphHandle(MotionScope inner)
            {
                _inner = inner;
            }

            public bool IsDone => _inner.IsDone;

            public void Tick(float deltaSeconds)
            {
                _inner.Tick(deltaSeconds);
            }

            public void Cancel()
            {
                _inner.Cancel();
            }
        }
    }
}
