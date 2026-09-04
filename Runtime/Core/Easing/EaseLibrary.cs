using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 이징 함수 평가. 입력 t는 항상 [0,1]로 클램프된다.
    ///
    /// 반환값은 클램프하지 않는다 — <see cref="EaseKind.OutBack"/>처럼 1을 넘었다가 돌아오는
    /// 이징이 있고, 그 오버슈트가 연출의 핵심이기 때문이다.
    /// </summary>
    public static class EaseLibrary
    {
        private const float BackOvershoot = 1.70158f;

        /// <summary>
        /// <paramref name="t"/>를 [0,1]로 클램프한 뒤 이징을 적용한다.
        /// 모르는 <paramref name="kind"/>는 <see cref="EaseKind.Linear"/>로 떨어진다 —
        /// 미래 버전이 만든 그래프를 열어도 죽지 않기 위해서다.
        /// </summary>
        public static float Evaluate(EaseKind kind, float t)
        {
            if (t <= 0f)
            {
                return 0f;
            }

            if (t >= 1f)
            {
                return 1f;
            }

            switch (kind)
            {
                case EaseKind.Linear: return t;

                case EaseKind.InQuad: return t * t;
                case EaseKind.OutQuad: return 1f - (1f - t) * (1f - t);
                case EaseKind.InOutQuad: return t < 0.5f
                    ? 2f * t * t
                    : 1f - 2f * (1f - t) * (1f - t);

                case EaseKind.InCubic: return t * t * t;
                case EaseKind.OutCubic:
                {
                    float inv = 1f - t;
                    return 1f - inv * inv * inv;
                }
                case EaseKind.InOutCubic: return t < 0.5f
                    ? 4f * t * t * t
                    : 1f - (float)Math.Pow(-2f * t + 2f, 3d) * 0.5f;

                case EaseKind.InSine: return 1f - (float)Math.Cos(t * Math.PI * 0.5d);
                case EaseKind.OutSine: return (float)Math.Sin(t * Math.PI * 0.5d);
                case EaseKind.InOutSine: return -(float)(Math.Cos(Math.PI * t) - 1d) * 0.5f;

                case EaseKind.OutBack:
                {
                    const float c1 = BackOvershoot;
                    const float c3 = c1 + 1f;
                    float inv = t - 1f;
                    return 1f + c3 * inv * inv * inv + c1 * inv * inv;
                }

                case EaseKind.OutElastic:
                {
                    double period = 2d * Math.PI / 3d;
                    return (float)(Math.Pow(2d, -10d * t) * Math.Sin((t * 10d - 0.75d) * period) + 1d);
                }

                case EaseKind.OutBounce: return OutBounce(t);

                default: return t;
            }
        }

        private static float OutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
            {
                return n1 * t * t;
            }

            if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }

            if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }

            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
