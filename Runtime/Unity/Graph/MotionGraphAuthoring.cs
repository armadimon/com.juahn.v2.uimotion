using System;
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
    /// <b>규칙: 이 클래스에 그래프를 바꾸는 메서드를 추가하면 마지막 줄이
    /// <c>Invalidate()</c>여야 한다.</b> 강제하는 장치가 없으므로 사람이 지켜야 한다.
    /// 빠뜨리면 낡은 인덱스가 조용히 살아남는다. 예외는 <see cref="SetNodePosition"/>
    /// 하나뿐이다 — 이유는 그 메서드의 주석에 있다. 아무것도 바꾸지 않는 조회
    /// (<see cref="FindTrigger"/> 등)는 이 규칙의 대상이 아니다.
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
        /// 노드와 그것에 닿는 간선을 전부 지운다.
        ///
        /// 트리거 노드를 지우면 <b>그 트리거가 사라진다.</b> 트리거 목록은 노드에서
        /// 계산되는 파생값이므로 그것이 맞는 동작이다 — 진입점을 잃은 유령 트리거가
        /// 목록에 남는 일이 없다.
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

        /// <summary>
        /// 트리거 노드를 만들어 넣는다. 같은 이름이 이미 있으면 그 노드의 id를 돌려주고
        /// 새로 만들지 않는다 — 이름이 겹치면 뒤엣것이 무시되므로 만들어 봐야 혼란만 준다.
        ///
        /// <b>이미 있는 노드를 돌려줄 때 <paramref name="policy"/>는 적용되지 않는다.</b>
        /// 이 API는 트리거를 만드는 것이지 고치는 것이 아니고, 이미 있는 노드의 정책은
        /// 그 노드를 인스펙터에서 고쳐야 한다.
        /// </summary>
        public NodeId AddTrigger(string triggerName, TriggerPolicy policy = TriggerPolicy.Restart)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return NodeId.None;
            }

            string trimmed = triggerName.Trim();

            NodeId existing = FindTrigger(trimmed);
            if (existing.IsValid)
            {
                return existing;
            }

            return AddNode(new TriggerNode { TriggerName = trimmed, Policy = policy });
        }

        /// <summary>이 이름의 트리거 노드를 찾는다. 없으면 <see cref="NodeId.None"/>.</summary>
        public NodeId FindTrigger(string triggerName)
        {
            if (string.IsNullOrWhiteSpace(triggerName))
            {
                return NodeId.None;
            }

            string trimmed = triggerName.Trim();

            for (int i = 0; i < _nodes.Count; i++)
            {
                var trigger = _nodes[i] as TriggerNode;
                if (trigger == null || trigger.TriggerName == null)
                {
                    continue;
                }

                if (trigger.TriggerName.Trim() == trimmed)
                {
                    return trigger.Id;
                }
            }

            return NodeId.None;
        }

        /// <summary>
        /// 옛 형식의 트리거 목록을 트리거 노드로 옮긴다. 옮긴 개수를 돌려준다.
        ///
        /// 옛 진입 노드는 새 트리거 노드의 <b>자식이 된다</b> — 예전에는 진입점이 그
        /// 노드였고 이제는 트리거 노드가 그 앞에 서기 때문이다. 실행 결과는 같다.
        ///
        /// 두 번 불러도 안전하다. 옮긴 것은 목록에서 지운다.
        /// </summary>
        public MigrationReport MigrateLegacyTriggers()
        {
            var report = new MigrationReport();

            if (_triggers.Count == 0)
            {
                return report;
            }

            // 목록을 통째로 떼어 내고 앞에서부터 돈다.
            //
            // 통째로 떼어 내는 이유 — 아래에서 노드를 넣을 때마다 인덱스가 무효화되고,
            // 다시 만들어질 때 남아 있는 옛 목록을 보고 오류를 낸다. 한 항목씩 지우면
            // 마이그레이션 도중에 그 오류가 항목 수만큼 콘솔에 찍힌다.
            //
            // 앞에서부터 도는 이유 — 저작 순서를 지켜야 트리거 노드가 거꾸로 만들어지지
            // 않고, 이름이 겹칠 때 옛 인덱스와 같은 "먼저 나온 것이 이긴다"가 된다.
            var legacy = new List<TriggerDeclaration>(_triggers);
            _triggers.Clear();

            var migrated = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < legacy.Count; i++)
            {
                TriggerDeclaration decl = legacy[i];
                if (decl == null || string.IsNullOrWhiteSpace(decl.Name))
                {
                    report.Dropped.Add("이름 없는 트리거 선언을 버렸습니다. 발사할 방법이 없던 것입니다.");
                    continue;
                }

                string name = decl.Name.Trim();
                if (!migrated.Add(name))
                {
                    report.Dropped.Add("트리거 '" + name + "'이 여러 번 선언돼 있어 먼저 나온 것만 옮겼습니다.");
                    continue;
                }

                NodeId trigger = AddTrigger(name, decl.Policy);
                if (!trigger.IsValid)
                {
                    report.Dropped.Add("트리거 '" + name + "'의 노드를 만들지 못했습니다.");
                    continue;
                }

                // 이미 트리거 노드가 있었다면(예전 표식 방식) AddTrigger가 그것을 돌려주고
                // 정책은 손대지 않는다. 정책의 진실은 옛 목록이므로 여기서 옮긴다.
                var node = FindNodeInList(trigger) as TriggerNode;
                if (node != null)
                {
                    node.Policy = decl.Policy;
                }

                // 옛 진입 노드를 새 트리거 노드 아래에 붙인다.
                //
                // 잇지 못하는 경우를 조용히 넘기지 않는다. 그러면 그 트리거를 발사해도
                // 아무 일도 일어나지 않는데 오류가 하나도 없는 상태가 된다 —
                // 마이그레이션이 만들 수 있는 가장 나쁜 결과다.
                MotionNodeBase entry = decl.Entry.IsValid ? FindNodeInList(decl.Entry) : null;

                if (!decl.Entry.IsValid)
                {
                    report.Unlinked.Add("트리거 '" + name + "'은 옮기기 전에도 진입 노드가 없었습니다. 연결할 연출을 직접 이어 주세요.");
                }
                else if (entry == null)
                {
                    report.Unlinked.Add("트리거 '" + name + "'의 진입 노드 " + decl.Entry +
                        "가 이미 사라져 있어 잇지 못했습니다. 연결할 연출을 직접 이어 주세요.");
                }
                else if (decl.Entry == trigger)
                {
                    // 예전 표식 방식. 그 노드가 곧 트리거 노드이므로 이을 것이 없다. 정상이다.
                }
                else if (entry is TriggerNode)
                {
                    report.Unlinked.Add("트리거 '" + name + "'의 진입 노드가 다른 트리거 노드(" + decl.Entry +
                        ")를 가리키고 있어 잇지 않았습니다. 그 아래 연출이 끊겼는지 확인하세요.");
                }
                else
                {
                    Link(trigger, decl.Entry);
                }

                report.Moved++;
            }

            Invalidate();
            return report;
        }

        /// <summary>
        /// 저작 목록을 직접 훑는다. <see cref="MotionGraph.GetNode"/>가 아니라 이것을 쓰는
        /// 이유는 마이그레이션 도중 노드를 넣을 때마다 인덱스가 무효화되어 조회 한 번마다
        /// 인덱스가 통째로 다시 계산되기 때문이다.
        /// </summary>
        private MotionNodeBase FindNodeInList(NodeId id)
        {
            if (!id.IsValid)
            {
                return null;
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null && _nodes[i].Id == id)
                {
                    return _nodes[i];
                }
            }

            return null;
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
        /// 남아 있던 간선은 건드리지 않는다 — 이미 존재하지 않는 id를 가리키고 있고,
        /// 검사기가 따로 오류로 보고한다.
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
