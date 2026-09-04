using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class OnceLoggerTests
    {
        [Test]
        public void SameKey_WarnsOnlyOnce()
        {
            // 매 프레임 도는 연출이 매 프레임 경고하면 콘솔이 죽는다.
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            for (int i = 0; i < 100; i++)
            {
                logger.WarnOnce("slot:Icon", "slot not bound: Icon");
            }

            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
            Assert.That(sink.Warnings[0], Is.EqualTo("slot not bound: Icon"));
        }

        [Test]
        public void DifferentKeys_WarnSeparately()
        {
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            logger.WarnOnce("slot:Icon", "a");
            logger.WarnOnce("slot:Title", "b");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void Reset_AllowsWarningAgain()
        {
            // 플레이어가 다시 활성화되면 다시 경고할 기회를 준다.
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            logger.WarnOnce("k", "m");
            logger.Reset();
            logger.WarnOnce("k", "m");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void PlainWarn_IsNotSuppressed()
        {
            // 억제는 WarnOnce에만 적용된다. Warn은 그대로 흘려보낸다.
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            logger.Warn("m");
            logger.Warn("m");

            Assert.That(sink.Warnings.Count, Is.EqualTo(2));
        }

        [Test]
        public void Errors_PassThroughEveryTime()
        {
            // 오류는 억제하지 않는다. 놓치면 안 되는 것이다.
            var sink = new FakeLog();
            var logger = new OnceLogger(sink);

            logger.Error("boom");
            logger.Error("boom");

            Assert.That(sink.Errors.Count, Is.EqualTo(2));
        }

        [Test]
        public void NullSink_DoesNotThrow()
        {
            var logger = new OnceLogger(null);

            Assert.DoesNotThrow(delegate { logger.WarnOnce("k", "m"); });
            Assert.DoesNotThrow(delegate { logger.Warn("m"); });
            Assert.DoesNotThrow(delegate { logger.Error("m"); });
            Assert.DoesNotThrow(delegate { logger.Reset(); });
        }

        [Test]
        public void NullSink_StillSuppresses()
        {
            // 싱크가 없어도 억제 기록은 남아야 한다 - 나중에 싱크가 붙는 경우를 위해서가 아니라,
            // 억제 판단이 싱크 유무에 따라 달라지면 동작이 예측 불가능해지기 때문이다.
            var logger = new OnceLogger(null);

            Assert.DoesNotThrow(delegate
            {
                logger.WarnOnce("k", "m");
                logger.WarnOnce("k", "m");
            });
        }

        [Test]
        public void ImplementsIMotionLog()
        {
            // 실행기는 IMotionLog만 안다. OnceLogger를 그대로 꽂을 수 있어야 한다.
            var sink = new FakeLog();
            IMotionLog log = new OnceLogger(sink);

            log.Warn("m");

            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
        }
    }
}
