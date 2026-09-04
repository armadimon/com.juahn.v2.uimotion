using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 자식을 <b>차례로</b> 실행한다. 앞의 자식이 완전히 끝나야 다음이 시작된다.
    ///
    /// <b>알려진 한계</b> — 한 번의 <c>Tick</c>에서 첫 자식만 그 프레임의 델타를 받는다.
    /// 같은 프레임에 이어 시작되는 자식은 델타 0으로 시작하고, 앞 자식이 쓰고 남은 시간은
    /// 버려진다. <c>RepeatNode</c>와 같은 이유이고 같은 판단이다 — 넘기려면
    /// <see cref="NodeRun"/>이 소비한 시간을 보고해야 하는데 실행 모델 전체를 건드리는 변경이다.
    ///
    /// 매 프레임 작은 델타로 틱하는 정상 사용에서는 노드 전이당 최대 한 프레임 손실이라
    /// 눈에 보이지 않는다. 큰 델타를 한 번에 먹이는 경로에서는 체감된다.
    /// </summary>
    [MotionNode(Name = "Sequence", Category = "Flow",
        Summary = "자식을 차례로 실행한다. 앞의 자식이 완전히 끝나야 다음이 시작된다.",
        Sample = "Sequence")]
    [Serializable]
    public sealed class SequenceNode : MotionFlowNode
    {
        public override bool OwnsChildren => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            IReadOnlyList<NodeId> children = ctx.Graph.GetChildren(Id);
            if (children == null || children.Count == 0)
            {
                return MotionHandle.Completed;
            }

            return new SequenceHandle(ctx, children);
        }

        private sealed class SequenceHandle : IMotionHandle
        {
            private readonly IMotionContext _ctx;
            private readonly IReadOnlyList<NodeId> _children;

            private NodeRun _current;
            private int _index;
            private bool _done;

            public SequenceHandle(IMotionContext ctx, IReadOnlyList<NodeId> children)
            {
                _ctx = ctx;
                _children = children;
                _index = 0;
            }

            public bool IsDone => _done;

            public void Tick(float deltaSeconds)
            {
                if (_done)
                {
                    return;
                }

                float remaining = deltaSeconds;

                // 즉시 끝나는 자식이 여럿 이어질 수 있다. 한 프레임에 다 소화한다 —
                // 안 그러면 자식 하나당 한 프레임씩 밀린다.
                while (!_done)
                {
                    if (_current == null)
                    {
                        if (_index >= _children.Count)
                        {
                            _done = true;
                            return;
                        }

                        _current = new NodeRun(_ctx, _children[_index]);
                        _index++;
                    }

                    _current.Tick(remaining);
                    remaining = 0f;

                    if (!_current.IsDone)
                    {
                        return;
                    }

                    _current = null;
                }
            }

            public void Cancel()
            {
                if (_done)
                {
                    return;
                }

                if (_current != null)
                {
                    _current.Cancel();
                    _current = null;
                }

                _done = true;
            }
        }
    }
}
