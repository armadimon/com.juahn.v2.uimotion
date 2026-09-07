using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프를 바꾸는 쪽. <b>에디터 저작용이다</b> — 런타임에 부르면 프로젝트의 에셋을
    /// 통째로 바꾸는 것이 되므로 절대 부르지 않는다.
    ///
    /// 별도 어셈블리인 에디터 패키지가 써야 하므로 public이고, <c>#if UNITY_EDITOR</c>로
    /// 감싸지 않는다.
    ///
    /// <b>규칙: 이 클래스에 메서드를 추가하면 마지막 줄이 <c>Invalidate()</c>여야 한다.</b>
    /// 강제하는 장치가 없으므로 사람이 지켜야 한다. 빠뜨리면 낡은 인덱스가 조용히 살아남는다.
    /// 예외는 <see cref="SetNodePosition"/> 하나뿐이다 — 이유는 그 메서드의 주석에 있다.
    /// </summary>
    public sealed partial class MotionGraph
    {
        /// <summary>노드를 넣고 새 id를 부여해 돌려준다.</summary>
        public NodeId AddNode(MotionNodeBase node)
        {
            if (node == null)
            {
                return NodeId.None;
            }

            var id = new NodeId(_nextNodeId++);
            node.Id = id;
            _nodes.Add(node);
            Invalidate();
            return id;
        }

        /// <summary>
        /// 노드와 그것에 닿는 간선을 전부 지운다. 그 노드를 진입점으로 삼던 트리거는
        /// 진입점을 잃고 <see cref="NodeId.None"/>이 된다 — 트리거 자체를 조용히 지우지는 않는다.
        /// </summary>
        public bool RemoveNode(NodeId id)
        {
            if (!id.IsValid)
            {
                return false;
            }

            bool removed = false;

            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i] != null && _nodes[i].Id == id)
                {
                    _nodes.RemoveAt(i);
                    removed = true;
                }
            }

            if (!removed)
            {
                return false;
            }

            for (int i = _links.Count - 1; i >= 0; i--)
            {
                if (_links[i].From == id || _links[i].To == id)
                {
                    _links.RemoveAt(i);
                }
            }

            for (int i = 0; i < _triggers.Count; i++)
            {
                if (_triggers[i] != null && _triggers[i].Entry == id)
                {
                    _triggers[i].Entry = NodeId.None;
                }
            }

            for (int i = _layout.Count - 1; i >= 0; i--)
            {
                if (_layout[i].Node == id)
                {
                    _layout.RemoveAt(i);
                }
            }

            Invalidate();
            return true;
        }

        /// <summary>
        /// 그래프 창에서의 노드 위치를 저장한다.
        ///
        /// <b>인덱스를 무효화하지 않는다.</b> 위치는 실행에 아무 영향이 없고, 노드를 끌 때마다
        /// 인덱스를 다시 만들면 노드가 많은 그래프에서 끌기가 눈에 띄게 버벅인다.
        /// 이것이 이 클래스에서 <c>Invalidate()</c>를 부르지 않는 유일한 메서드다.
        /// </summary>
        public void SetNodePosition(NodeId id, Vector2 position)
        {
            if (!id.IsValid)
            {
                return;
            }

            for (int i = 0; i < _layout.Count; i++)
            {
                if (_layout[i].Node == id)
                {
                    _layout[i] = new NodeLayout(id, position);
                    return;
                }
            }

            _layout.Add(new NodeLayout(id, position));
        }

        /// <summary>간선을 잇는다. 같은 간선을 두 번 넣지 않는다.</summary>
        public bool Link(NodeId from, NodeId to)
        {
            var link = new NodeLink(from, to);
            if (!link.IsValid || _links.Contains(link))
            {
                return false;
            }

            _links.Add(link);
            Invalidate();
            return true;
        }

        public bool Unlink(NodeId from, NodeId to)
        {
            if (!_links.Remove(new NodeLink(from, to)))
            {
                return false;
            }

            Invalidate();
            return true;
        }

        /// <summary>
        /// 간선의 순서를 바꾼다. 같은 부모에서 나가는 간선의 순서가 곧 실행 순서이므로
        /// 그래프 창의 "위로/아래로"가 이것을 부른다.
        /// </summary>
        public bool MoveLink(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _links.Count || toIndex < 0 || toIndex >= _links.Count)
            {
                return false;
            }

            NodeLink link = _links[fromIndex];
            _links.RemoveAt(fromIndex);
            _links.Insert(toIndex, link);
            Invalidate();
            return true;
        }

        /// <summary>트리거를 선언하거나 이미 있으면 덮어쓴다.</summary>
        public void SetTrigger(string triggerName, NodeId entry, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            if (string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            for (int i = 0; i < _triggers.Count; i++)
            {
                if (_triggers[i] != null && _triggers[i].Name == triggerName)
                {
                    _triggers[i].Entry = entry;
                    _triggers[i].Policy = policy;
                    Invalidate();
                    return;
                }
            }

            _triggers.Add(new TriggerDeclaration(triggerName, entry, policy));
            Invalidate();
        }

        public bool RemoveTrigger(string triggerName)
        {
            for (int i = _triggers.Count - 1; i >= 0; i--)
            {
                if (_triggers[i] != null && _triggers[i].Name == triggerName)
                {
                    _triggers.RemoveAt(i);
                    Invalidate();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 결손 노드를 정리한다. 지운 개수를 돌려준다.
        ///
        /// <b>결손 노드는 id가 없다.</b> 타입이 사라진 그래프를 열면 <c>SerializeReference</c>가
        /// 배열에 <c>null</c>을 남기는데, null에는 <see cref="NodeId"/>가 없으므로
        /// <see cref="RemoveNode"/>로는 지목할 수 없다. 그래서 별도 API가 필요하다.
        ///
        /// 그래프 창도 이것을 그리지 못한다 — 노드가 없으니 뷰를 만들 수 없다.
        /// 검사기는 오류로 잡는데 고칠 방법이 없는 상태가 되므로, 에디터가 이 메서드를
        /// "결손 노드 정리" 버튼으로 노출한다.
        ///
        /// 남아 있던 간선과 트리거 진입점은 건드리지 않는다 — 그것들은 이미 존재하지 않는
        /// id를 가리키고 있고, 검사기가 따로 오류로 보고한다.
        /// </summary>
        public int RemoveMissingNodes()
        {
            int removed = 0;

            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i] == null)
                {
                    _nodes.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Invalidate();
            }

            return removed;
        }

        /// <summary>결손 노드가 하나라도 있는가. 에디터가 버튼을 보일지 정할 때 쓴다.</summary>
        public bool HasMissingNodes()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] == null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>파생 인덱스를 버린다. 다음 조회에서 다시 계산된다.</summary>
        public void Invalidate()
        {
            DropDerived();
        }
    }
}
