using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionHandleTests
    {
        [Test]
        public void Completed_IsDoneImmediately()
        {
            Assert.That(MotionHandle.Completed.IsDone, Is.True);
        }

        [Test]
        public void Skipped_IsDoneImmediately()
        {
            Assert.That(MotionHandle.Skipped.IsDone, Is.True);
        }

        [Test]
        public void Skipped_IsDistinguishableFromCompleted()
        {
            // 진단이 "슬롯이 비어서 건너뜀"과 "정상적으로 즉시 끝남"을 구분해야 한다.
            Assert.That(MotionHandle.IsSkipped(MotionHandle.Skipped), Is.True);
            Assert.That(MotionHandle.IsSkipped(MotionHandle.Completed), Is.False);
        }

        [Test]
        public void FromTimer_ReportsProgress_UntilDone()
        {
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(1f, t => samples.Add(t));

            Assert.That(handle.IsDone, Is.False);

            handle.Tick(0.5f);
            handle.Tick(0.5f);

            Assert.That(handle.IsDone, Is.True);
            Assert.That(samples, Is.EqualTo(new[] { 0.5f, 1f }).Within(1e-5f));
        }

        [Test]
        public void FromTimer_EmitsFinalOne_EvenOnOvershoot()
        {
            // 마지막 프레임이 커서 duration을 훌쩍 넘겨도 끝값은 정확히 1이어야 한다.
            // 안 그러면 페이드인이 alpha 0.97에서 멈춘다.
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(1f, t => samples.Add(t));

            handle.Tick(10f);

            Assert.That(samples, Is.EqualTo(new[] { 1f }).Within(1e-5f));
        }

        [Test]
        public void FromTimer_ZeroDuration_CompletesOnFirstTick_WithOne()
        {
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(0f, t => samples.Add(t));

            handle.Tick(0f);

            Assert.That(handle.IsDone, Is.True);
            Assert.That(samples, Is.EqualTo(new[] { 1f }).Within(1e-5f));
        }

        [Test]
        public void FromTimer_Cancel_StopsProgress()
        {
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(1f, t => samples.Add(t));

            handle.Tick(0.25f);
            handle.Cancel();
            handle.Tick(0.25f);

            Assert.That(handle.IsDone, Is.True, "취소된 핸들은 완료로 취급한다");
            Assert.That(samples.Count, Is.EqualTo(1), "취소 후에는 진행률을 더 쏘지 않는다");
        }

        [Test]
        public void FromTimer_TickAfterDone_DoesNothing()
        {
            var samples = new System.Collections.Generic.List<float>();
            IMotionHandle handle = MotionHandle.FromTimer(1f, t => samples.Add(t));

            handle.Tick(1f);
            handle.Tick(1f);

            Assert.That(samples.Count, Is.EqualTo(1));
        }

        [Test]
        public void FromTimer_NullCallback_DoesNotThrow()
        {
            // Delay 노드는 진행률이 필요 없어 null을 넘긴다.
            IMotionHandle handle = MotionHandle.FromTimer(1f, null);

            Assert.DoesNotThrow(() => handle.Tick(0.5f));
            Assert.DoesNotThrow(() => handle.Tick(0.5f));
            Assert.That(handle.IsDone, Is.True);
        }

        [Test]
        public void SharedHandles_AreSafeToReuse()
        {
            // Completed/Skipped는 싱글턴이라 수백 개 노드가 동시에 돌려준다.
            // 상태가 없어야 하고 Tick/Cancel이 아무 일도 하지 않아야 한다.
            MotionHandle.Completed.Tick(1f);
            MotionHandle.Completed.Cancel();
            Assert.That(MotionHandle.Completed.IsDone, Is.True);
        }

        [Test]
        public void SeparateTimerHandles_HaveIndependentState()
        {
            // 같은 그래프 에셋을 수백 개 오브젝트가 동시에 쓴다. 핸들은 인스턴스마다 독립이어야 한다.
            IMotionHandle a = MotionHandle.FromTimer(1f, null);
            IMotionHandle b = MotionHandle.FromTimer(1f, null);

            a.Tick(1f);

            Assert.That(a.IsDone, Is.True);
            Assert.That(b.IsDone, Is.False);
        }

        [Test]
        public void Forever_NeverCompletesOnItsOwn()
        {
            IMotionHandle handle = MotionHandle.Forever(null);

            for (int i = 0; i < 1000; i++)
            {
                handle.Tick(1f);
            }

            Assert.That(handle.IsDone, Is.False, "유지 연출은 취소될 때까지 끝나지 않는다");
        }

        [Test]
        public void Forever_AccumulatesElapsed()
        {
            float last = -1f;
            IMotionHandle handle = MotionHandle.Forever(delegate(float elapsed) { last = elapsed; });

            handle.Tick(0.5f);
            Assert.That(last, Is.EqualTo(0.5f).Within(1e-5f));

            handle.Tick(0.25f);
            Assert.That(last, Is.EqualTo(0.75f).Within(1e-5f));
        }

        [Test]
        public void Forever_FirstTickReportsElapsedNotZero()
        {
            // 첫 틱에서 0을 보내면 사인파가 한 프레임 멈춘 것처럼 보인다.
            float first = -1f;
            IMotionHandle handle = MotionHandle.Forever(delegate(float elapsed) { first = elapsed; });

            handle.Tick(0.1f);

            Assert.That(first, Is.EqualTo(0.1f).Within(1e-5f));
        }

        [Test]
        public void Forever_CancelStopsCallbacks()
        {
            int calls = 0;
            IMotionHandle handle = MotionHandle.Forever(delegate { calls++; });

            handle.Tick(1f);
            handle.Cancel();
            handle.Tick(1f);
            handle.Tick(1f);

            Assert.That(handle.IsDone, Is.True);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Forever_NullCallback_IsSafe()
        {
            IMotionHandle handle = MotionHandle.Forever(null);

            Assert.DoesNotThrow(delegate { handle.Tick(1f); });
            Assert.DoesNotThrow(handle.Cancel);
        }
    }
}
