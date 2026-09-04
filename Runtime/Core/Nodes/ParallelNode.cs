using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 자식을 <b>동시에</b> 시작하고, 전부 끝나야 완료한다.
    /// </summary>
    [MotionNode(Name = "Parallel", Category = "Flow",
        Summary = "자식을 동시에 시작하고 전부 끝나야 완료한다.",
        Sample = "Parallel")]
    [Serializable]
    public sealed class ParallelNode : MotionFlowNode
    {
        public override bool OwnsChildren => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            IReadOnlyList<NodeId> children = ctx.Graph.GetChildren(Id);
            if (children == null || children.Count == 0)
            {
                return MotionHandle.Completed;
            }

            return new ParallelHandle(ctx, children);
        }

        private sealed class ParallelHandle : IMotionHandle
        {
            private readonly List<NodeRun> _runs;
            private bool _started;
            private bool _done;

            public ParallelHandle(IMotionContext ctx, IReadOnlyList<NodeId> children)
            {
                _runs = new List<NodeRun>(children.Count);
                for (int i = 0; i < children.Count; i++)
                {
                    _runs.Add(new NodeRun(ctx, children[i]));
                }
            }

            public bool IsDone => _done;

            public void Tick(float deltaSeconds)
            {
                if (_done)
                {
                    return;
                }

                if (!_started)
                {
                    _started = true;

                    // 모든 자식을 먼저 "시작"만 시킨다(델타 0) — 그러지 않으면 앞 자식이
                    // 이번 프레임에 실제 델타를 다 써버려 뒤 자식이 시작되기도 전에 끝나 버린다.
                    for (int i = 0; i < _runs.Count; i++)
                    {
                        _runs[i].Tick(0f);
                    }
                }

                bool allDone = true;
                for (int i = 0; i < _runs.Count; i++)
                {
                    _runs[i].Tick(deltaSeconds);

                    if (!_runs[i].IsDone)
                    {
                        allDone = false;
                    }
                }

                _done = allDone;
            }

            public void Cancel()
            {
                if (_done)
                {
                    return;
                }

                for (int i = 0; i < _runs.Count; i++)
                {
                    _runs[i].Cancel();
                }

                _done = true;
            }
        }
    }
}
