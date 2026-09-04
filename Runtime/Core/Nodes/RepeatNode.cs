using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 자식들을 정해진 횟수만큼, 또는 무한히 반복한다. 한 사이클은 자식 전부가
    /// 끝나야 완료된다(<c>Parallel</c>과 같은 묶음 단위).
    ///
    /// <b>알려진 한계</b> — 사이클이 완료되고 남은 시간은 다음 사이클로 넘기지 않는다.
    /// 한 번의 <c>Tick</c>에서 첫 사이클만 그 프레임의 delta를 받고 이후 사이클은 0에서 시작한다.
    /// 넘기려면 <see cref="NodeRun"/>이 소비한 시간을 보고해야 하는데 실행 모델 전체를
    /// 건드리는 변경이다. UI 장식 루프에서 사이클당 최대 한 프레임 손실은 눈에 보이지 않는다.
    /// </summary>
    [MotionNode(Name = "Repeat", Category = "Flow",
        Summary = "자식을 정해진 횟수만큼, 또는 무한히 반복한다. Count에 -1을 넣으면 무한이다.",
        Sample = "Repeat")]
    [Serializable]
    public sealed class RepeatNode : MotionFlowNode
    {
        /// <summary><see cref="Count"/>에 넣으면 끝나지 않는다.</summary>
        public const int Infinite = -1;

        /// <summary>
        /// 한 번의 <c>Tick</c>에서 도는 사이클 수의 상한. 지속시간 0짜리 자식을
        /// 무한 반복으로 걸면 이 상한이 없을 때 프레임이 영원히 끝나지 않는다.
        /// </summary>
        public const int MaxCyclesPerTick = 64;

        [MotionParam(Label = "반복 횟수", Tooltip = "-1이면 무한")]
        public int Count = Infinite;

        public override bool OwnsChildren => true;

        protected override IMotionHandle OnPlay(IMotionContext ctx)
        {
            IReadOnlyList<NodeId> children = ctx.Graph.GetChildren(Id);
            if (children == null || children.Count == 0 || Count == 0)
            {
                return MotionHandle.Completed;
            }

            return new RepeatHandle(ctx, children, Count);
        }

        private sealed class RepeatHandle : IMotionHandle
        {
            private readonly IMotionContext _ctx;
            private readonly IReadOnlyList<NodeId> _children;
            private readonly int _totalCycles;

            private List<NodeRun> _runs;
            private int _completedCycles;
            private bool _done;

            public RepeatHandle(IMotionContext ctx, IReadOnlyList<NodeId> children, int totalCycles)
            {
                _ctx = ctx;
                _children = children;
                _totalCycles = totalCycles;
            }

            public bool IsDone => _done;

            public void Tick(float deltaSeconds)
            {
                if (_done)
                {
                    return;
                }

                float remaining = deltaSeconds;

                for (int guard = 0; guard < MaxCyclesPerTick; guard++)
                {
                    if (_runs == null)
                    {
                        StartCycle();

                        // 사이클의 자식을 전부 델타 0으로 먼저 "시작"만 시킨다 —
                        // Parallel과 같은 이유다. 안 그러면 앞 자식이 이번 사이클의
                        // 델타를 다 써버려 뒤 자식이 시작되기도 전에 끝나 버린다.
                        for (int i = 0; i < _runs.Count; i++)
                        {
                            _runs[i].Tick(0f);
                        }
                    }

                    bool allDone = true;
                    for (int i = 0; i < _runs.Count; i++)
                    {
                        _runs[i].Tick(remaining);

                        if (!_runs[i].IsDone)
                        {
                            allDone = false;
                        }
                    }

                    // 남은 시간은 첫 사이클에서만 쓴다. 위 클래스 주석의 "알려진 한계" 참조.
                    remaining = 0f;

                    if (!allDone)
                    {
                        return;
                    }

                    _runs = null;
                    _completedCycles++;

                    if (_totalCycles != Infinite && _completedCycles >= _totalCycles)
                    {
                        _done = true;
                        return;
                    }
                }
            }

            public void Cancel()
            {
                if (_done)
                {
                    return;
                }

                if (_runs != null)
                {
                    for (int i = 0; i < _runs.Count; i++)
                    {
                        _runs[i].Cancel();
                    }

                    _runs = null;
                }

                _done = true;
            }

            private void StartCycle()
            {
                _runs = new List<NodeRun>(_children.Count);
                for (int i = 0; i < _children.Count; i++)
                {
                    _runs.Add(new NodeRun(_ctx, _children[i]));
                }
            }
        }
    }
}
