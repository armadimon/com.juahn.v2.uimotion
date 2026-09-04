using System.Collections.Generic;

namespace Juahn.UiMotion.Tests
{
    /// <summary>
    /// 테스트용 인메모리 그래프. 노드를 넣고 링크하면 <see cref="IMotionGraphView"/>가 된다.
    /// </summary>
    public sealed class FakeGraph : IMotionGraphView
    {
        private readonly List<MotionNodeBase> _nodes = new List<MotionNodeBase>();
        private readonly Dictionary<int, List<NodeId>> _children = new Dictionary<int, List<NodeId>>();
        private readonly List<TriggerDeclaration> _triggers = new List<TriggerDeclaration>();
        private readonly List<SlotDeclaration> _slots = new List<SlotDeclaration>();

        private static readonly NodeId[] NoChildren = new NodeId[0];

        public string GraphName { get; set; } = "FakeGraph";

        public IReadOnlyList<TriggerDeclaration> Triggers => _triggers;

        public IReadOnlyList<SlotDeclaration> Slots => _slots;

        /// <summary>노드를 넣고 id를 부여한다. id는 1부터 순서대로다.</summary>
        public NodeId Add(MotionNodeBase node)
        {
            _nodes.Add(node);
            var id = new NodeId(_nodes.Count);
            node.Id = id;
            return id;
        }

        /// <summary>부모에 자식을 잇는다. 부른 순서가 곧 자식 순서다.</summary>
        public void Link(NodeId parent, NodeId child)
        {
            List<NodeId> list;
            if (!_children.TryGetValue(parent.Value, out list))
            {
                list = new List<NodeId>();
                _children[parent.Value] = list;
            }

            list.Add(child);
        }

        public void DeclareTrigger(string name, NodeId entry, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            _triggers.Add(new TriggerDeclaration(name, entry, policy));
        }

        public void DeclareSlot(string name)
        {
            _slots.Add(new SlotDeclaration(name));
        }

        public MotionNodeBase GetNode(NodeId id)
        {
            int index = id.Value - 1;
            if (index < 0 || index >= _nodes.Count)
            {
                return null;
            }

            return _nodes[index];
        }

        public NodeId GetEntry(string triggerName)
        {
            for (int i = 0; i < _triggers.Count; i++)
            {
                if (_triggers[i].Name == triggerName)
                {
                    return _triggers[i].Entry;
                }
            }

            return NodeId.None;
        }

        public IReadOnlyList<NodeId> GetChildren(NodeId parent)
        {
            List<NodeId> list;
            if (_children.TryGetValue(parent.Value, out list))
            {
                return list;
            }

            return NoChildren;
        }
    }
}
