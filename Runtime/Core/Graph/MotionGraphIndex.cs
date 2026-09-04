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
    ///
    /// 그 불변식을 말로만 두지 않기 위해 조회 결과는 전부 배열로 굳혀서 내보낸다.
    /// <c>List</c>를 <c>IReadOnlyList</c>로 캐스팅해 돌려주면 호출자가 되캐스팅해 고칠 수 있다.
    ///
    /// <b>슬롯 목록은 노드 인덱스와 다른 집합을 본다.</b> 슬롯은 저작된 모든 노드에서
    /// 계산하고, 노드 인덱스는 id가 중복되거나 없는 노드를 버린다. 슬롯은 "이 그래프가
    /// 무엇을 요구하는가"의 선언이므로, 실행되지 못하는 노드 때문에 사람이 채워 둔
    /// 바인딩이 사라지면 안 되기 때문이다.
    /// </summary>
    public sealed class MotionGraphIndex : IMotionGraphView
    {
        private static readonly NodeId[] NoChildren = new NodeId[0];
        private static readonly TriggerDeclaration[] NoTriggers = new TriggerDeclaration[0];

        private readonly Dictionary<int, MotionNodeBase> _nodes = new Dictionary<int, MotionNodeBase>();
        private readonly Dictionary<int, NodeId[]> _children = new Dictionary<int, NodeId[]>();
        private readonly Dictionary<string, NodeId> _entries = new Dictionary<string, NodeId>(StringComparer.Ordinal);

        private readonly TriggerDeclaration[] _triggers;
        private readonly SlotDeclaration[] _slots;

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
            _triggers = IndexTriggers(triggers, log);

            var slots = new List<SlotDeclaration>();
            SlotIntrospector.Collect(nodes, slots);
            _slots = slots.ToArray();
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
            NodeId[] children;
            return _children.TryGetValue(parent.Value, out children) ? children : NoChildren;
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
                    string typeName = node.GetType().Name;
                    Warn(log, "noid:" + typeName,
                        "node of type " + typeName + " has no id and was dropped");
                    continue;
                }

                if (_nodes.ContainsKey(node.Id.Value))
                {
                    Warn(log, "dupnode:" + node.Id.Value,
                        "duplicate node id " + node.Id + "; the first one wins");
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

            var building = new Dictionary<int, List<NodeId>>();

            for (int i = 0; i < links.Count; i++)
            {
                NodeLink link = links[i];
                if (!link.IsValid)
                {
                    continue;
                }

                List<NodeId> list;
                if (!building.TryGetValue(link.From.Value, out list))
                {
                    list = new List<NodeId>();
                    building[link.From.Value] = list;
                }

                list.Add(link.To);
            }

            // 배열로 굳힌다. 조회 결과를 호출자가 고칠 수 있으면 안 되기 때문이다.
            foreach (KeyValuePair<int, List<NodeId>> pair in building)
            {
                _children[pair.Key] = pair.Value.ToArray();
            }
        }

        private TriggerDeclaration[] IndexTriggers(IReadOnlyList<TriggerDeclaration> triggers, IMotionLog log)
        {
            if (triggers == null)
            {
                return NoTriggers;
            }

            var kept = new List<TriggerDeclaration>();

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];
                if (decl == null || string.IsNullOrEmpty(decl.Name))
                {
                    continue;
                }

                if (_entries.ContainsKey(decl.Name))
                {
                    Warn(log, "duptrigger:" + decl.Name,
                        "duplicate trigger '" + decl.Name + "'; the first one wins");
                    continue;
                }

                _entries[decl.Name] = decl.Entry;
                kept.Add(decl);
            }

            return kept.ToArray();
        }

        /// <summary>
        /// 억제되는 경고. <b>키가 인덱스를 다시 만들어도 같아야 한다</b> — 인덱스는 그래프를
        /// 편집할 때마다 새로 만들어지고 로그(<c>OnceLogger</c>)는 그보다 오래 산다.
        /// 억제되지 않으면 중복 노드 id 하나가 <c>OnValidate</c>마다 콘솔에 다시 찍힌다.
        /// </summary>
        private void Warn(IMotionLog log, string key, string message)
        {
            MotionLogs.WarnOnce(log, "graph:" + GraphName + ":" + key, message);
        }
    }
}
