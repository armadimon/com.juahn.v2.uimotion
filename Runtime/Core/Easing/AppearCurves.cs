using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 등장 연출의 크기 곡선. IdlePaori의 결과 등장(<c>SlotRevealGrid</c>)에서 그대로 옮겨 왔다.
    ///
    /// <b>왜 <see cref="EaseLibrary"/>에 넣지 않는가</b> — 이징은 0에서 1로 가는 진행도 함수이고
    /// 매개변수를 받지 않는다. 여기 있는 둘은 매개변수를 받고 <b>배율 자체</b>를 돌려준다.
    /// 같은 자리에 두면 <see cref="EaseKind"/>가 매개변수 있는 것과 없는 것을 섞어 갖게 된다.
    ///
    /// 코어에 두는 이유는 이 수학이 UnityEngine을 하나도 쓰지 않기 때문이다. 그래야
    /// <c>dotnet test</c>로 검증할 수 있고, 검증되지 않은 곡선은 눈으로만 확인하게 된다.
    /// </summary>
    public static class AppearCurves
    {
        /// <summary>기본 오버슈트. <see cref="EaseKind.OutBack"/>이 쓰는 값과 같다.</summary>
        public const float DefaultOvershoot = 1.70158f;

        /// <summary>
        /// 오버슈트를 지정할 수 있는 OutBack.
        ///
        /// <see cref="EaseLibrary"/>의 <see cref="EaseKind.OutBack"/>과 식이 같고 상수만 열었다.
        /// 등장 팝은 세기가 연출의 전부라 고정 상수로는 "강조 칸"을 만들 수 없다 —
        /// IdlePaori는 일반 칸에 1.7, 강조 칸에 3.2를 쓴다.
        /// </summary>
        /// <param name="t">진행도. [0,1]로 클램프된다.</param>
        /// <param name="overshoot">넘치는 정도. 0이면 넘치지 않는다.</param>
        public static float OutBack(float t, float overshoot)
        {
            t = Clamp01(t);

            float c1 = overshoot;
            float c3 = c1 + 1f;
            float inv = t - 1f;

            return 1f + c3 * inv * inv * inv + c1 * inv * inv;
        }

        /// <summary>
        /// 슬램 — 큰 크기에서 <b>줄어들며 박힌다.</b>
        ///
        /// <paramref name="dropRatio"/>까지는 <paramref name="start"/>배에서 1배로 가속하며
        /// 내려오고(뒤로 갈수록 빨라져야 떨어지는 것처럼 보인다), 남은 구간에서 1 아래로
        /// <paramref name="impact"/>만큼 눌렸다 1로 돌아온다.
        ///
        /// 눌림이 사인 반주기인 것은 <b>시작과 끝이 정확히 1이 되기 때문이다.</b> 어느 프레임에
        /// 끊겨도 크기가 튀지 않고, 마지막 프레임을 놓쳐도 대상이 어긋난 크기로 남지 않는다.
        /// </summary>
        /// <param name="t">진행도. [0,1]로 클램프된다.</param>
        /// <param name="start">시작 배율. 1 미만이면 1로 올린다 — 슬램은 줄어들며 박히는 것이다.</param>
        /// <param name="dropRatio">내려오는 데 쓰는 구간 비율. [0.5, 1]로 클램프된다.</param>
        /// <param name="impact">박히는 순간 1 아래로 눌리는 정도. 0이면 눌리지 않는다.</param>
        public static float Slam(float t, float start, float dropRatio, float impact)
        {
            t = Clamp01(t);

            if (start < 1f)
            {
                start = 1f;
            }

            float drop = Clamp(dropRatio, 0.5f, 1f);

            if (t < drop)
            {
                float u = drop > 0f ? t / drop : 1f;
                float accel = u * u * u;
                return start + (1f - start) * accel;
            }

            float v = drop < 1f ? (t - drop) / (1f - drop) : 1f;
            return 1f - impact * (float)Math.Sin(v * Math.PI);
        }

        private static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
