using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드 하나와 그 아래 전파를 굴리는 단위. 자기 핸들이 끝나면 자식들을
    /// <b>동시에</b> 시작하고, 자식이 전부 끝나야 자신도 끝난다.
    ///
    /// 자식 시작을 실행기가 맡는 덕분에 효과 노드가 <c>Punch → Fade</c>처럼
    /// 이어지는 배선을 스스로 구현하지 않아도 된다. 자식을 직접 조율하고 싶은 노드는
    /// <see cref="MotionNodeBase.OwnsChildren"/>을 true로 덮는다.
    /// </summary>
    public sealed class NodeRun
    {
        private readonly IMotionContext _ctx;
        private readonly NodeId _id;

        private IMotionHandle _self;
        private List<NodeRun> _children;
        private bool _started;
        private bool _selfDone;
        private bool _cancelled;

        public NodeRun(IMotionContext ctx, NodeId id)
        {
            _ctx = ctx;
            _id = id;
        }

        public bool IsDone { get; private set; }

        public void Tick(float deltaSeconds)
        {
            if (IsDone)
            {
                return;
            }

            if (!_started)
            {
                Start();
            }

            if (!_selfDone)
            {
                _self.Tick(deltaSeconds);

                if (!_self.IsDone)
                {
                    return;
                }

                _selfDone = true;
                SpawnChildren();

                // 자식을 방금 만들었다면 이번 프레임에 한 번 굴려 준다.
                // 안 그러면 즉시 끝나는 자식이 한 프레임씩 밀린다.
                if (_children != null)
                {
                    TickChildren(0f);
                }

                UpdateDone();
                return;
            }

            TickChildren(deltaSeconds);
            UpdateDone();
        }

        public void Cancel()
        {
            if (IsDone)
            {
                return;
            }

            _cancelled = true;

            if (_self != null)
            {
                _self.Cancel();
            }

            if (_children != null)
            {
                for (int i = 0; i < _children.Count; i++)
                {
                    _children[i].Cancel();
                }
            }

            IsDone = true;
        }

        private void Start()
        {
            _started = true;

            MotionNodeBase node = _ctx.Graph.GetNode(_id);

            // 결손 노드 — 타입이 사라졌거나 id가 어긋났다. 건너뛰고 나머지를 살린다.
            if (node == null)
            {
                _self = MotionHandle.Completed;
                return;
            }

            _self = node.Play(_ctx);
        }

        private void SpawnChildren()
        {
            if (_cancelled)
            {
                return;
            }

            MotionNodeBase node = _ctx.Graph.GetNode(_id);

            // 자식을 직접 조율하는 노드는 실행기가 건드리지 않는다.
            if (node != null && node.OwnsChildren)
            {
                return;
            }

            // 계약상 GetChildren은 null을 돌려주지 않지만, 구현체가 어길 수 있으므로 방어한다.
            IReadOnlyList<NodeId> childIds = _ctx.Graph.GetChildren(_id);
            if (childIds == null || childIds.Count == 0)
            {
                return;
            }

            _children = new List<NodeRun>(childIds.Count);
            for (int i = 0; i < childIds.Count; i++)
            {
                _children.Add(new NodeRun(_ctx, childIds[i]));
            }
        }

        private void TickChildren(float deltaSeconds)
        {
            if (_children == null)
            {
                return;
            }

            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].Tick(deltaSeconds);
            }
        }

        private void UpdateDone()
        {
            if (!_selfDone)
            {
                return;
            }

            if (_children == null)
            {
                IsDone = true;
                return;
            }

            for (int i = 0; i < _children.Count; i++)
            {
                if (!_children[i].IsDone)
                {
                    return;
                }
            }

            IsDone = true;
        }
    }
}
