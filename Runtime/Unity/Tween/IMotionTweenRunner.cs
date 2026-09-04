using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 교체 가능한 트윈 백엔드.
    ///
    /// <b>표면이 일부러 작다.</b> 러너는 "시간이 어떻게 흐르고 이징을 어떻게 샘플링하는가"만
    /// 알고, "RectTransform을 어떻게 움직이는가"는 모른다. 그래서 효과 노드를 백엔드마다
    /// 다시 짤 필요가 없다 — 노드는 한 번만 짜고 백엔드만 갈아 끼운다.
    ///
    /// 기본 구현은 코어의 타이머다(<see cref="BuiltinTweenRunner"/>). DOTween 백엔드는
    /// 별도 패키지에서 이 인터페이스를 구현한다.
    /// </summary>
    public interface IMotionTweenRunner
    {
        /// <summary>
        /// <paramref name="duration"/>초 동안 <b>이징된</b> 진행률을 <paramref name="onEased"/>로 흘린다.
        ///
        /// 계약:
        /// <list type="bullet">
        /// <item>끝날 때 정확히 1을 한 번 보낸다. 그러지 않으면 페이드인이 0.97에서 멈춘다</item>
        /// <item><paramref name="duration"/>이 0 이하면 즉시 1을 한 번 보내고 끝난다</item>
        /// <item>이징이 1을 넘길 수 있다(OutBack · OutElastic). 노드는 <c>LerpUnclamped</c>를 쓴다</item>
        /// </list>
        /// </summary>
        IMotionHandle Run(float duration, EaseKind ease, Action<float> onEased);
    }
}
