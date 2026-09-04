using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 실행기가 그래프를 보는 <b>유일한</b> 창구. 실행기는 ScriptableObject도 직렬화도 모른다.
    ///
    /// Unity 계층의 <c>MotionGraph</c>(ScriptableObject)와 테스트용 가짜 그래프가 이것을 구현한다.
    /// 그래프는 실행 상태를 일절 갖지 않는다 — 같은 에셋을 수백 개 오브젝트가 동시에 쓴다.
    /// </summary>
    public interface IMotionGraphView
    {
        /// <summary>진단용 이름.</summary>
        string GraphName { get; }

        IReadOnlyList<TriggerDeclaration> Triggers { get; }

        IReadOnlyList<SlotDeclaration> Slots { get; }

        /// <summary>노드를 찾는다. 없으면 null — 결손 노드는 실행 시 건너뛴다.</summary>
        MotionNodeBase GetNode(NodeId id);

        /// <summary>트리거의 진입 노드. 그런 트리거가 없으면 <see cref="NodeId.None"/>.</summary>
        NodeId GetEntry(string triggerName);

        /// <summary>
        /// 이 노드의 자식들. <b>순서가 보장된다</b> — <c>SequenceNode</c>가 이 순서로 실행한다.
        /// 자식이 없으면 빈 목록을 돌려준다(null 금지).
        /// </summary>
        IReadOnlyList<NodeId> GetChildren(NodeId parent);
    }
}
