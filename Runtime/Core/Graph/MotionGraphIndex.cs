using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 직렬화된 배열들(노드 · 간선 · 트리거)을 실행기가 쓸 수 있는 조회 구조로 바꾼다.
    ///
    /// <b>이것이 <see cref="IMotionGraphView"/> 구현의 전부다.</b> Unity의 <c>MotionGraph</c>는
    /// ScriptableObject 껍데기일 뿐이고 모든 조회를 여기에 위임한다. 그 덕분에 그래프 로직이
    /// UnityEngine 없이 <c>dotnet test</c>로 검증된다.
    ///
    /// <b>읽기 전용이다.</b> 만들 때 한 번 계산하고 그 뒤로는 바뀌지 않는다. 그래프가 실행
    /// 상태를 갖지 않는다는 불변식이 여기에 걸려 있다 — 같은 에셋을 수백 개 오브젝트가
    /// 동시에 쓰기 때문이다. 저작 쪽이 배열을 바꾸면 인덱스를 <b>새로 만든다</b>.
    /// </summary>
    public sealed class MotionGraphIndex : IMotionGraphView
    {
        private static readonly NodeId[] NoChildren = new NodeId[0];

        private readonly Dictionary<int, MotionNodeBase> _nodes = new Dictionary<int, MotionNodeBase>();
        private readonly Dictionary<int, List<NodeId>> _children = new Dictionary<int, List<NodeId>>();
        private readonly Dictionary<string, NodeId> _entries = new Dictionary<string, NodeId>(StringComparer.Ordinal);
        private readonly List<TriggerDeclaration> _triggers = new List<TriggerDeclaration>();
        private readonly List<SlotDeclaration> _slots = new List<SlotDeclaration>();

        public MotionGraphIndex(
            string graphName,
            IReadOnlyList<MotionNodeBase> nodes,
            IReadOnlyList<NodeLink> links,
            IReadOnlyList<TriggerDeclaration> triggers,
            IMotionLog log)
        {
            GraphName = string.IsNullOrEmpty(graphName) ? "<unnamed graph>" : graphName;

            IndexNodes(nodes, log);
            IndexLinks(links);
            IndexTriggers(triggers, log);

            SlotIntrospector.Collect(nodes, _slots);
        }

        public string GraphName { get; }

        public IReadOnlyList<TriggerDeclaration> Triggers => _triggers;

        public IReadOnlyList<SlotDeclaration> Slots => _slots;

        public MotionNodeBase GetNode(NodeId id)
        {
            MotionNodeBase node;
            return _nodes.TryGetValue(id.Value, out node) ? node : null;
        }

        public NodeId GetEntry(string triggerName)
        {
            if (triggerName == null)
            {
                return NodeId.None;
            }

            NodeId entry;
            return _entries.TryGetValue(triggerName, out entry) ? entry : NodeId.None;
        }

        public IReadOnlyList<NodeId> GetChildren(NodeId parent)
        {
            List<NodeId> list;
            return _children.TryGetValue(parent.Value, out list) ? (IReadOnlyList<NodeId>)list : NoChildren;
        }

        private void IndexNodes(IReadOnlyList<MotionNodeBase> nodes, IMotionLog log)
        {
            if (nodes == null)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                MotionNodeBase node = nodes[i];

                // 결손 노드 — 타입이 사라진 그래프를 열면 SerializeReference가 null을 남긴다.
                // 조용히 건너뛰고 나머지 그래프를 살린다. 경고를 내지 않는 이유는
                // 에디터가 결손을 눈에 보이게 표시할 것이기 때문이다.
                if (node == null)
                {
                    continue;
                }

                if (!node.Id.IsValid)
                {
                    Warn(log, "node of type " + node.GetType().Name + " has no id and was dropped");
                    continue;
                }

                if (_nodes.ContainsKey(node.Id.Value))
                {
                    Warn(log, "duplicate node id " + node.Id + "; the first one wins");
                    continue;
                }

                _nodes[node.Id.Value] = node;
            }
        }

        private void IndexLinks(IReadOnlyList<NodeLink> links)
        {
            if (links == null)
            {
                return;
            }

            for (int i = 0; i < links.Count; i++)
            {
                NodeLink link = links[i];
                if (!link.IsValid)
                {
                    continue;
                }

                List<NodeId> list;
                if (!_children.TryGetValue(link.From.Value, out list))
                {
                    list = new List<NodeId>();
                    _children[link.From.Value] = list;
                }

                list.Add(link.To);
            }
        }

        private void IndexTriggers(IReadOnlyList<TriggerDeclaration> triggers, IMotionLog log)
        {
            if (triggers == null)
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];
                if (decl == null || string.IsNullOrEmpty(decl.Name))
                {
                    continue;
                }

                if (_entries.ContainsKey(decl.Name))
                {
                    Warn(log, "duplicate trigger '" + decl.Name + "'; the first one wins");
                    continue;
                }

                _entries[decl.Name] = decl.Entry;
                _triggers.Add(decl);
            }
        }

        private static void Warn(IMotionLog log, string message)
        {
            if (log != null)
            {
                log.Warn(message);
            }
        }
    }
}
