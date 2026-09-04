namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 실행될 때 받는 문맥. 노드는 이것 말고 바깥세상을 알지 못한다.
    /// </summary>
    public interface IMotionContext
    {
        /// <summary>지금 돌고 있는 그래프.</summary>
        IMotionGraphView Graph { get; }

        /// <summary>이 실행이 속한 스코프. 원상 복구를 여기에 등록한다.</summary>
        IMotionScope Scope { get; }

        IMotionLog Log { get; }

        /// <summary>다른 트리거를 건드리는 통로. 실행기 밖에서 만든 문맥에서는 null일 수 있다.</summary>
        ITriggerSink Triggers { get; }

        /// <summary>슬롯을 실제 대상으로. 바인딩되지 않았으면 null.</summary>
        object ResolveSlot(SlotRef slot);
    }
}
