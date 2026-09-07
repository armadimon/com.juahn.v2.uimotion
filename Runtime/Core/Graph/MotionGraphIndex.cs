using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 직렬화된 배열들(노드 · 간선)을 실행기가 쓸 수 있는 조회 구조로 바꾼다.
    ///
    /// <b>트리거 목록은 넘겨받지 않고 노드에서 계산한다</b> — 슬롯과 같은 파생값이다
    /// (<see cref="TriggerIntrospector"/>). 진입점은 <see cref="TriggerNode"/> 자신이다.
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
        private static readonly NodeId[] NoNodes = new NodeId[0];

        private readonly Dictionary<int, MotionNodeBase> _nodes = new Dictionary<int, MotionNodeBase>();
        private readonly Dictionary<int, NodeId[]> _children = new Dictionary<int, NodeId[]>();
        private readonly Dictionary<string, NodeId> _entries = new Dictionary<string, NodeId>(StringComparer.Ordinal);

        private readonly NodeId[] _nodeIds;
        private readonly TriggerDeclaration[] _triggers;
        private readonly SlotDeclaration[] _slots;

        public MotionGraphIndex(
            string graphName,
            IReadOnlyList<MotionNodeBase> nodes,
            IReadOnlyList<NodeLink> links,
            IMotionLog log)
            : this(graphName, nodes, links, null, log)
        {
        }

        /// <param name="legacyTriggers">
        /// 트리거를 노드로 옮기기 전에 저장된 목록. <b>읽지 않는다</b> — 비어 있지 않으면
        /// 그 그래프가 아직 마이그레이션되지 않았다는 뜻이므로 시끄럽게 경고만 한다.
        ///
        /// 조용히 무시하면 마이그레이션을 잊은 그래프가 트리거를 통째로 잃은 채
        /// 아무 말 없이 돌아간다. 팝업이 열리지 않는데 오류가 하나도 없는 상태가 된다.
        /// </param>
        public MotionGraphIndex(
            string graphName,
            IReadOnlyList<MotionNodeBase> nodes,
            IReadOnlyList<NodeLink> links,
            IReadOnlyList<TriggerDeclaration> legacyTriggers,
            IMotionLog log)
        {
            GraphName = string.IsNullOrEmpty(graphName) ? "<unnamed graph>" : graphName;

            _nodeIds = IndexNodes(nodes, log);
            IndexLinks(links);

            var triggers = new List<TriggerDeclaration>();
            TriggerIntrospector.Collect(nodes, triggers, log);
            _triggers = triggers.ToArray();

            for (int i = 0; i < _triggers.Length; i++)
            {
                _entries[_triggers[i].Name] = _triggers[i].Entry;
            }

            WarnAboutLegacyTriggers(legacyTriggers, log);

            var slots = new List<SlotDeclaration>();
            SlotIntrospector.Collect(nodes, slots);
            _slots = slots.ToArray();
        }

        /// <summary>
        /// 옛 목록이 남아 있으면 크게 경고한다. 마이그레이션을 잊은 그래프는
        /// 트리거를 잃은 채 조용히 도는 것이 가장 나쁜 결과다.
        /// </summary>
        private void WarnAboutLegacyTriggers(IReadOnlyList<TriggerDeclaration> legacy, IMotionLog log)
        {
            if (legacy == null || legacy.Count == 0 || log == null)
            {
                return;
            }

            log.Error("graph '" + GraphName + "' still stores " + legacy.Count +
                " trigger declaration(s) from the old format. run " +
                "Window > UI Motion > Migrate Graphs to convert them into Trigger nodes. " +
                "until then this graph has no triggers.");
        }

        public string GraphName { get; }

        public IReadOnlyList<TriggerDeclaration> Triggers => _triggers;

        public IReadOnlyList<SlotDeclaration> Slots => _slots;

        public IReadOnlyList<NodeId> NodeIds => _nodeIds;

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

        /// <summary>
        /// 노드를 사전에 담고 <b>살아남은 id를 저작 순서 그대로</b> 돌려준다.
        /// 사전에 넣는 바로 그 자리에서 목록에도 담으므로 둘이 어긋날 수 없다.
        /// </summary>
        private NodeId[] IndexNodes(IReadOnlyList<MotionNodeBase> nodes, IMotionLog log)
        {
            if (nodes == null)
            {
                return NoNodes;
            }

            var ids = new List<NodeId>(nodes.Count);

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
                ids.Add(node.Id);
            }

            // 조회 결과는 전부 배열로 굳힌다 — 호출자가 되캐스팅해 고칠 수 없어야 한다.
            return ids.ToArray();
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
