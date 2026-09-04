using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 자식을 <b>차례로</b> 실행한다. 앞의 자식이 완전히 끝나야 다음이 시작된다.
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
