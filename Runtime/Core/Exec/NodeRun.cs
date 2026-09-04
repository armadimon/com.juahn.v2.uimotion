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
        /// <summary>
        /// 노드 사슬의 최대 중첩 깊이. 이 구조는 재귀라서 아주 깊은 사슬이
        /// 스택 오버플로로 프로세스를 통째로 죽인다 — <c>catch</c>로 잡히지도 않는다.
        ///
        /// 정상적인 UI 연출 그래프는 10~20 단계면 충분하다. 이 상한에 걸린다는 것은
        /// 그래프가 잘못됐다는 뜻이므로, 에러를 남기고 그 가지를 잘라낸다.
        /// </summary>
        public const int MaxDepth = 256;

        private readonly IMotionContext _ctx;
        private readonly NodeId _id;
        private readonly int _depth;

        private MotionNodeBase _node;
        private IMotionHandle _self;
        private List<NodeRun> _children;
        private bool _started;
        private bool _selfDone;
        private bool _cancelled;

        public NodeRun(IMotionContext ctx, NodeId id)
            : this(ctx, id, 0)
        {
        }

        private NodeRun(IMotionContext ctx, NodeId id, int depth)
        {
            _ctx = ctx;
            _id = id;
            _depth = depth;
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

            if (_depth >= MaxDepth)
            {
                if (_ctx.Log != null)
                {
                    _ctx.Log.Error("node chain exceeded depth " + MaxDepth +
                        " at node " + _id + "; the branch was cut off. the graph is likely malformed.");
                }

                _self = MotionHandle.Completed;
                return;
            }

            _node = _ctx.Graph.GetNode(_id);

            // 결손 노드 — 타입이 사라졌거나 id가 어긋났다. 건너뛰고 나머지를 살린다.
            if (_node == null)
            {
                _self = MotionHandle.Completed;
                return;
            }

            _self = _node.Play(_ctx);
        }

        private void SpawnChildren()
        {
            if (_cancelled)
            {
                return;
            }

            // 자식을 직접 조율하는 노드는 실행기가 건드리지 않는다.
            if (_node != null && _node.OwnsChildren)
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
                _children.Add(new NodeRun(_ctx, childIds[i], _depth + 1));
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
