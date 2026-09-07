using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    /// <summary>
    /// 등장 곡선. 여기서 지키는 것은 <b>끝값</b>이다 — 곡선이 1로 끝나지 않으면 연출이 끝난 뒤
    /// 대상이 어긋난 크기로 남고, 그것은 프리팹에 구워져 나중에 원인을 찾을 수 없게 된다.
    /// </summary>
    [TestFixture]
    public sealed class AppearCurvesTests
    {
        private const float Tolerance = 1e-5f;

        // --- OutBack ----------------------------------------------------

        [Test]
        public void OutBack_StartsAtZero_AndEndsAtOne()
        {
            Assert.That(AppearCurves.OutBack(0f, 1.7f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(AppearCurves.OutBack(1f, 1.7f), Is.EqualTo(1f).Within(Tolerance));
            Assert.That(AppearCurves.OutBack(1f, 3.2f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void OutBack_WithDefaultOvershoot_MatchesEaseLibrary()
        {
            // 같은 식을 두 곳에 두게 됐으므로 어긋나지 않는지 잠근다. 어긋나면 그래프에서
            // Scale 노드와 Pop 노드가 같은 값인데 다르게 움직인다.
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;

                Assert.That(
                    AppearCurves.OutBack(t, AppearCurves.DefaultOvershoot),
                    Is.EqualTo(EaseLibrary.Evaluate(EaseKind.OutBack, t)).Within(Tolerance),
                    "t=" + t + "에서 EaseLibrary와 어긋남");
            }
        }

        [Test]
        public void OutBack_Overshoots_AndStrongerOvershootGoesHigher()
        {
            float weak = Peak(1.7f);
            float strong = Peak(3.2f);

            Assert.That(weak, Is.GreaterThan(1f), "1.7이면 1을 넘어야 한다");
            Assert.That(strong, Is.GreaterThan(weak), "3.2가 1.7보다 더 넘쳐야 한다");
        }

        [Test]
        public void OutBack_WithZeroOvershoot_NeverExceedsOne()
        {
            for (int i = 0; i <= 100; i++)
            {
                Assert.That(AppearCurves.OutBack(i / 100f, 0f), Is.LessThanOrEqualTo(1f + Tolerance));
            }
        }

        [Test]
        public void OutBack_ClampsInput()
        {
            Assert.That(AppearCurves.OutBack(-5f, 1.7f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(AppearCurves.OutBack(5f, 1.7f), Is.EqualTo(1f).Within(Tolerance));
        }

        // --- Slam -------------------------------------------------------

        [Test]
        public void Slam_StartsAtStartScale_AndEndsAtOne()
        {
            Assert.That(AppearCurves.Slam(0f, 2.2f, 0.72f, 0.12f), Is.EqualTo(2.2f).Within(Tolerance));
            Assert.That(AppearCurves.Slam(1f, 2.2f, 0.72f, 0.12f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Slam_ReachesOneAtDropPoint()
        {
            // 낙하 구간이 끝나는 지점에서 정확히 제자리 크기여야 한다. 여기가 "박히는" 순간이고,
            // 이 값이 1이 아니면 눌림이 엉뚱한 크기를 기준으로 돈다.
            Assert.That(AppearCurves.Slam(0.72f, 2.2f, 0.72f, 0.12f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Slam_DipsBelowOneAfterImpact()
        {
            float mid = AppearCurves.Slam(0.86f, 2.2f, 0.72f, 0.12f);

            Assert.That(mid, Is.LessThan(1f), "박힌 뒤에는 제자리 크기 아래로 눌려야 한다");
            Assert.That(mid, Is.GreaterThan(1f - 0.12f - Tolerance), "impact보다 더 눌리면 안 된다");
        }

        [Test]
        public void Slam_Descends_NeverRisingDuringDrop()
        {
            float previous = AppearCurves.Slam(0f, 2.2f, 0.72f, 0.12f);

            for (int i = 1; i <= 72; i++)
            {
                float current = AppearCurves.Slam(i / 100f, 2.2f, 0.72f, 0.12f);

                Assert.That(current, Is.LessThanOrEqualTo(previous + Tolerance),
                    "낙하 구간에서 다시 커졌다. t=" + (i / 100f));

                previous = current;
            }
        }

        [Test]
        public void Slam_Accelerates_StayingAboveLinearDuringDrop()
        {
            // 슬램의 맛은 <b>뒤로 갈수록 빨라지는 것</b>에서 나온다. 등속으로 내려오면 그냥
            // 줄어드는 크기일 뿐 떨어져서 박히는 것으로 보이지 않는다.
            //
            // 가속한다는 것은 곧 초반에 덜 내려온다는 뜻이다 - 같은 진행도에서 등속선보다
            // 위에 있어야 한다. 이 검사가 없으면 곡선을 선형으로 바꿔도 아무도 모른다.
            const float start = 2.2f;
            const float drop = 0.72f;

            for (int i = 1; i < 72; i++)
            {
                float t = i / 100f;
                float linear = start + (1f - start) * (t / drop);
                float actual = AppearCurves.Slam(t, start, drop, 0.12f);

                Assert.That(actual, Is.GreaterThan(linear),
                    "등속선보다 아래로 내려갔다 - 가속하지 않는다. t=" + t);
            }
        }

        [Test]
        public void Slam_DropsFastestAtTheEnd()
        {
            // 가속의 직접 확인 - 마지막 구간의 낙폭이 첫 구간보다 커야 한다.
            const float start = 2.2f;
            const float drop = 0.72f;
            const float step = 0.01f;

            float firstDelta = AppearCurves.Slam(0f, start, drop, 0f)
                - AppearCurves.Slam(step, start, drop, 0f);

            float lastDelta = AppearCurves.Slam(drop - step, start, drop, 0f)
                - AppearCurves.Slam(drop, start, drop, 0f);

            Assert.That(lastDelta, Is.GreaterThan(firstDelta * 2f),
                "끝으로 갈수록 빨라지지 않는다");
        }

        [Test]
        public void Slam_WithZeroImpact_StaysAtOneAfterDrop()
        {
            for (int i = 72; i <= 100; i++)
            {
                Assert.That(AppearCurves.Slam(i / 100f, 2.2f, 0.72f, 0f),
                    Is.EqualTo(1f).Within(Tolerance));
            }
        }

        [Test]
        public void Slam_WithStartBelowOne_TreatsStartAsOne()
        {
            // 슬램은 줄어들며 박히는 것이다. 1 미만을 주면 커지며 박히는 정반대 연출이 되므로
            // 그것을 허용하지 않는다 - 저작 실수가 조용히 다른 연출로 나타나면 안 된다.
            Assert.That(AppearCurves.Slam(0f, 0.4f, 0.72f, 0.12f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Slam_WithFullDropRatio_HasNoImpactPhase()
        {
            // drop이 1이면 눌림 구간의 길이가 0이다. 0으로 나누지 않고 1로 끝나야 한다.
            Assert.That(AppearCurves.Slam(1f, 2.2f, 1f, 0.12f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Slam_ClampsDropRatioToHalf()
        {
            // 0.5 미만을 주면 0.5로 잡힌다. 그래서 t=0.5에서는 이미 제자리 크기다.
            Assert.That(AppearCurves.Slam(0.5f, 2.2f, 0.1f, 0f), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void Slam_ClampsInput()
        {
            Assert.That(AppearCurves.Slam(-5f, 2.2f, 0.72f, 0.12f), Is.EqualTo(2.2f).Within(Tolerance));
            Assert.That(AppearCurves.Slam(5f, 2.2f, 0.72f, 0.12f), Is.EqualTo(1f).Within(Tolerance));
        }

        private static float Peak(float overshoot)
        {
            float peak = 0f;

            for (int i = 0; i <= 100; i++)
            {
                float value = AppearCurves.OutBack(i / 100f, overshoot);
                if (value > peak)
                {
                    peak = value;
                }
            }

            return peak;
        }
    }
}
