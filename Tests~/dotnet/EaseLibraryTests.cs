using System;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class EaseLibraryTests
    {
        private static EaseKind[] AllKinds()
        {
            return (EaseKind[])Enum.GetValues(typeof(EaseKind));
        }

        [Test]
        public void EveryEase_StartsAtZero()
        {
            foreach (EaseKind kind in AllKinds())
            {
                Assert.That(EaseLibrary.Evaluate(kind, 0f), Is.EqualTo(0f).Within(1e-5f),
                    "t=0에서 0이 아님: " + kind);
            }
        }

        [Test]
        public void EveryEase_EndsAtOne()
        {
            foreach (EaseKind kind in AllKinds())
            {
                Assert.That(EaseLibrary.Evaluate(kind, 1f), Is.EqualTo(1f).Within(1e-5f),
                    "t=1에서 1이 아님: " + kind);
            }
        }

        [Test]
        public void EveryEase_ClampsInputBelowZero()
        {
            foreach (EaseKind kind in AllKinds())
            {
                Assert.That(EaseLibrary.Evaluate(kind, -5f), Is.EqualTo(0f).Within(1e-5f),
                    "음수 t를 클램프하지 않음: " + kind);
            }
        }

        [Test]
        public void EveryEase_ClampsInputAboveOne()
        {
            foreach (EaseKind kind in AllKinds())
            {
                Assert.That(EaseLibrary.Evaluate(kind, 5f), Is.EqualTo(1f).Within(1e-5f),
                    "1보다 큰 t를 클램프하지 않음: " + kind);
            }
        }

        [Test]
        public void Linear_IsIdentity()
        {
            Assert.That(EaseLibrary.Evaluate(EaseKind.Linear, 0.25f), Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(EaseLibrary.Evaluate(EaseKind.Linear, 0.5f), Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void OutQuad_IsAheadOfLinear_InTheMiddle()
        {
            // "Out" 계열은 빠르게 시작해 느리게 끝난다. 중간에서 선형보다 앞서 있어야 한다.
            float eased = EaseLibrary.Evaluate(EaseKind.OutQuad, 0.5f);
            Assert.That(eased, Is.GreaterThan(0.5f));
        }

        [Test]
        public void InQuad_IsBehindLinear_InTheMiddle()
        {
            float eased = EaseLibrary.Evaluate(EaseKind.InQuad, 0.5f);
            Assert.That(eased, Is.LessThan(0.5f));
        }

        [Test]
        public void OutBack_Overshoots()
        {
            // OutBack은 1을 넘었다가 돌아온다. 그게 이 이징의 존재 이유다.
            bool overshot = false;
            for (int i = 1; i < 100; i++)
            {
                if (EaseLibrary.Evaluate(EaseKind.OutBack, i / 100f) > 1f)
                {
                    overshot = true;
                    break;
                }
            }

            Assert.That(overshot, Is.True, "OutBack이 1을 넘지 않는다");
        }

        [Test]
        public void UnknownKind_FallsBackToLinear()
        {
            // 직렬화된 그래프가 미래 버전의 이징 값을 들고 올 수 있다. 죽지 않아야 한다.
            float value = EaseLibrary.Evaluate((EaseKind)9999, 0.25f);
            Assert.That(value, Is.EqualTo(0.25f).Within(1e-5f));
        }

        // --- 회귀 고정 -------------------------------------------------------
        // 위 불변식 테스트는 경계와 대소 관계만 본다. 곡선 중간이 완전히 틀려도 통과한다.
        // 아래 값들은 easings.net 표준 정의에서 독립적으로 계산한 것으로,
        // 지금의 (검증된) 곡선 모양을 고정해 앞으로의 변경이 조용히 곡선을 바꾸지 못하게 한다.

        [Test]
        public void InOutCubic_MatchesReferenceCurve()
        {
            // easeInOutCubic(x) = x < 0.5 ? 4x^3 : 1 - (-2x+2)^3/2
            Assert.That(EaseLibrary.Evaluate(EaseKind.InOutCubic, 0.25f), Is.EqualTo(0.0625f).Within(1e-4f));
            Assert.That(EaseLibrary.Evaluate(EaseKind.InOutCubic, 0.75f), Is.EqualTo(0.9375f).Within(1e-4f));
        }

        [Test]
        public void OutBack_MatchesReferenceCurve()
        {
            // easeOutBack(x) = 1 + c3*(x-1)^3 + c1*(x-1)^2, c1 = 1.70158, c3 = c1 + 1
            Assert.That(EaseLibrary.Evaluate(EaseKind.OutBack, 0.5f), Is.EqualTo(1.0876975f).Within(1e-4f));
            // 오버슈트가 최대인 지점(x = 1 - 2*c1/(3*c3) ≈ 0.5801) 근처
            Assert.That(EaseLibrary.Evaluate(EaseKind.OutBack, 0.6f), Is.EqualTo(1.09935168f).Within(1e-4f));
        }

        [Test]
        public void OutElastic_MatchesReferenceCurve()
        {
            // easeOutElastic(x) = pow(2, -10x) * sin((10x - 0.75) * c4) + 1, c4 = 2*pi/3
            // 부호나 주기가 반전되면 여기서 드러난다.
            Assert.That(EaseLibrary.Evaluate(EaseKind.OutElastic, 0.25f), Is.EqualTo(0.9116117f).Within(1e-4f));
            Assert.That(EaseLibrary.Evaluate(EaseKind.OutElastic, 0.5f), Is.EqualTo(1.015625f).Within(1e-4f));
        }

        [Test]
        public void OutBounce_MatchesReferenceCurve_AcrossAllFourSegments()
        {
            // easeOutBounce(x): n1 = 7.5625, d1 = 2.75, 4개 구간
            Assert.That(EaseLibrary.Evaluate(EaseKind.OutBounce, 0.2f), Is.EqualTo(0.3025f).Within(1e-4f), "구간 1 (x < 1/d1)");
            Assert.That(EaseLibrary.Evaluate(EaseKind.OutBounce, 0.5f), Is.EqualTo(0.765625f).Within(1e-4f), "구간 2 (x < 2/d1)");
            Assert.That(EaseLibrary.Evaluate(EaseKind.OutBounce, 0.8f), Is.EqualTo(0.94f).Within(1e-4f), "구간 3 (x < 2.5/d1)");
            Assert.That(EaseLibrary.Evaluate(EaseKind.OutBounce, 0.95f), Is.EqualTo(0.98453125f).Within(1e-4f), "구간 4 (그 외)");
        }
    }
}
