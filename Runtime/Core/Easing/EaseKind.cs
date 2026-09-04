namespace Juahn.UiMotion
{
    /// <summary>
    /// 이징 종류. 값은 직렬화되므로 <b>절대 바꾸지 않는다</b>. 새 이징은 뒤에 추가한다.
    /// </summary>
    public enum EaseKind
    {
        Linear = 0,

        InQuad = 10,
        OutQuad = 11,
        InOutQuad = 12,

        InCubic = 20,
        OutCubic = 21,
        InOutCubic = 22,

        InSine = 30,
        OutSine = 31,
        InOutSine = 32,

        OutBack = 40,
        OutElastic = 41,
        OutBounce = 42,
    }
}
