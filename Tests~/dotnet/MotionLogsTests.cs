using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionLogsTests
    {
        [Test]
        public void NullLog_IsSafe()
        {
            Assert.DoesNotThrow(() => MotionLogs.WarnOnce(null, "k", "m"));
        }

        [Test]
        public void OnceLogger_SuppressesRepeats()
        {
            var sink = new FakeLog();
            var once = new OnceLogger(sink);

            MotionLogs.WarnOnce(once, "slot:Icon", "unbound slot 'Icon'");
            MotionLogs.WarnOnce(once, "slot:Icon", "unbound slot 'Icon'");
            MotionLogs.WarnOnce(once, "slot:Icon", "unbound slot 'Icon'");

            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void OnceLogger_DifferentKeys_BothPass()
        {
            var sink = new FakeLog();
            var once = new OnceLogger(sink);

            MotionLogs.WarnOnce(once, "slot:Icon", "a");
            MotionLogs.WarnOnce(once, "slot:Label", "b");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void PlainLog_FallsBackToWarn()
        {
            // 억제를 모르는 로그라도 경고 자체는 나와야 한다. 조용한 실패가 최악이다.
            var sink = new FakeLog();

            MotionLogs.WarnOnce(sink, "k", "m");
            MotionLogs.WarnOnce(sink, "k", "m");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }
    }
}
