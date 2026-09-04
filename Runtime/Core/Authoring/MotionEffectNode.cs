using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 무언가를 움직이는 노드. 슬롯을 잡고 값을 바꾼다.
    ///
    /// 자식을 소유하지 않는다 — 효과가 끝나면 실행기가 자식들을 이어서 시작한다.
    /// 그래서 <c>Punch → Fade</c> 배선이 "펀치가 끝나면 페이드"로 읽힌다.
    /// </summary>
    [Serializable]
    public abstract class MotionEffectNode : MotionNodeBase
    {
    }
}
