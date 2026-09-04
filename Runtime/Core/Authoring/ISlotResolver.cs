namespace Juahn.UiMotion
{
    /// <summary>
    /// 슬롯 이름을 실제 대상으로 바꾼다. Unity 계층의 <c>SlotTable</c>이 구현한다.
    ///
    /// 코어는 대상이 무엇인지 모른다 — <c>object</c>로 받아 효과 노드가 캐스트한다.
    /// 그래서 코어가 <c>RectTransform</c> 같은 Unity 타입을 몰라도 된다.
    /// </summary>
    public interface ISlotResolver
    {
        /// <summary>바인딩되지 않았으면 null.</summary>
        object Resolve(SlotRef slot);
    }
}
