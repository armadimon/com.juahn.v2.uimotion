using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionTimerTests
    {
        [Test]
        public void FreshTimer_IsNotDone()
        {
            var timer = new MotionTimer(1f);
            Assert.That(timer.IsDone, Is.False);
            Assert.That(timer.Normalized, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Advance_AccumulatesElapsed()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(0.25f);
            timer.Advance(0.25f);
            Assert.That(timer.Normalized, Is.EqualTo(0.5f).Within(1e-5f));
        }

        [Test]
        public void Advance_PastDuration_IsDone()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(1.5f);
            Assert.That(timer.IsDone, Is.True);
        }

        [Test]
        public void Normalized_NeverExceedsOne()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(99f);
            Assert.That(timer.Normalized, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void ZeroDuration_IsDoneImmediately()
        {
            // 지속시간 0은 "즉시"를 뜻한다. 한 프레임도 기다리지 않는다.
            var timer = new MotionTimer(0f);
            Assert.That(timer.IsDone, Is.True);
            Assert.That(timer.Normalized, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void NegativeDuration_IsTreatedAsZero()
        {
            var timer = new MotionTimer(-3f);
            Assert.That(timer.IsDone, Is.True);
        }

        [Test]
        public void Reset_RewindsToStart()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(0.8f);
            timer.Reset();
            Assert.That(timer.IsDone, Is.False);
            Assert.That(timer.Normalized, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Overshoot_ReportsLeftoverTime()
        {
            // 반복 노드가 남은 시간을 다음 사이클로 넘길 때 쓴다.
            // 이게 없으면 매 사이클마다 최대 한 프레임씩 밀린다.
            var timer = new MotionTimer(1f);
            timer.Advance(1.25f);
            Assert.That(timer.Overshoot, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void Overshoot_IsZero_WhileRunning()
        {
            var timer = new MotionTimer(1f);
            timer.Advance(0.5f);
            Assert.That(timer.Overshoot, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void NegativeAdvance_IsIgnored()
        {
            // 시간이 거꾸로 흐르면 안 된다. 호출부의 실수를 여기서 막는다.
            var timer = new MotionTimer(1f);
            timer.Advance(0.5f);
            timer.Advance(-0.3f);
            Assert.That(timer.Normalized, Is.EqualTo(0.5f).Within(1e-5f));
        }
    }
}
