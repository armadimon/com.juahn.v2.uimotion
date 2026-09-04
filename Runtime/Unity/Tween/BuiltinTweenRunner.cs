using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 의존성 0인 기본 트윈 러너. 코어의 타이머 핸들과 이징 표만 쓴다.
    ///
    /// 이것이 있어서 패키지가 어떤 외부 트윈 라이브러리도 없이 완전히 동작한다.
    /// DOTween은 유료 에셋이라 코어에 넣으면 MIT 배포가 막힌다.
    ///
    /// 상태가 없으므로 인스턴스 하나를 공유한다.
    /// </summary>
    public sealed class BuiltinTweenRunner : IMotionTweenRunner
    {
        public static readonly BuiltinTweenRunner Shared = new BuiltinTweenRunner();

        public IMotionHandle Run(float duration, EaseKind ease, Action<float> onEased)
        {
            if (onEased == null)
            {
                return MotionHandle.FromTimer(duration, null);
            }

            EaseKind captured = ease;
            return MotionHandle.FromTimer(duration, delegate(float t)
            {
                onEased(EaseLibrary.Evaluate(captured, t));
            });
        }
    }
}
