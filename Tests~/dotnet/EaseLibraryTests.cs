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
    }
}
