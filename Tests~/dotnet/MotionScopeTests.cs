using System.Collections.Generic;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionScopeTests
    {
        [Test]
        public void FreshScope_IsNotDoneAndNotCancelled()
        {
            var scope = new MotionScope("Start");
            Assert.That(scope.IsDone, Is.False);
            Assert.That(scope.IsCancelled, Is.False);
            Assert.That(scope.TriggerName, Is.EqualTo("Start"));
        }

        [Test]
        public void Cancel_RunsRevertsInReverseOrder()
        {
            // 역순이어야 하는 이유: 나중에 등록된 것이 앞의 것 위에 쌓여 있다.
            // 스케일을 바꾼 뒤 위치를 바꿨다면, 위치를 먼저 되돌려야 한다.
            var order = new List<string>();
            var scope = new MotionScope("Start");

            scope.Remember(delegate { order.Add("first"); });
            scope.Remember(delegate { order.Add("second"); });
            scope.Remember(delegate { order.Add("third"); });

            scope.Cancel();

            Assert.That(order, Is.EqualTo(new[] { "third", "second", "first" }));
        }

        [Test]
        public void Cancel_MarksCancelledAndDone()
        {
            var scope = new MotionScope("Start");
            scope.Cancel();

            Assert.That(scope.IsCancelled, Is.True);
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Cancel_Twice_RunsRevertsOnlyOnce()
        {
            int calls = 0;
            var scope = new MotionScope("Start");
            scope.Remember(delegate { calls++; });

            scope.Cancel();
            scope.Cancel();

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Remember_AfterCancel_RunsImmediately()
        {
            // 취소된 스코프에 늦게 등록이 들어오면 그 자리에서 되돌려야 한다.
            // 안 그러면 아무도 되돌리지 않아 값이 남는다.
            int calls = 0;
            var scope = new MotionScope("Start");
            scope.Cancel();

            scope.Remember(delegate { calls++; });

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Remember_Null_IsIgnored()
        {
            var scope = new MotionScope("Start");
            Assert.DoesNotThrow(delegate { scope.Remember(null); });
            Assert.DoesNotThrow(delegate { scope.Cancel(); });
        }

        [Test]
        public void RevertThatThrows_DoesNotStopOtherReverts()
        {
            // 오브젝트 하나가 이미 파괴돼 예외가 나도 나머지는 전부 되돌려야 한다.
            var order = new List<string>();
            var log = new FakeLog();
            var scope = new MotionScope("Start", log);

            scope.Remember(delegate { order.Add("a"); });
            scope.Remember(delegate { throw new System.InvalidOperationException("boom"); });
            scope.Remember(delegate { order.Add("c"); });

            scope.Cancel();

            Assert.That(order, Is.EqualTo(new[] { "c", "a" }));
            Assert.That(log.Errors.Count, Is.EqualTo(1));
        }

        [Test]
        public void RevertThatThrows_WithoutLog_DoesNotCrash()
        {
            var scope = new MotionScope("Start");
            scope.Remember(delegate { throw new System.InvalidOperationException("boom"); });

            Assert.DoesNotThrow(delegate { scope.Cancel(); });
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Cancel_RaisesCompleted()
        {
            int raised = 0;
            var scope = new MotionScope("Start");
            scope.Completed += delegate { raised++; };

            scope.Cancel();

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_Twice_RaisesCompletedOnce()
        {
            int raised = 0;
            var scope = new MotionScope("Start");
            scope.Completed += delegate { raised++; };

            scope.Cancel();
            scope.Cancel();

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void ImplementsIMotionScope()
        {
            // 노드는 IMotionScope만 안다. MotionScope를 그대로 꽂을 수 있어야 한다.
            int calls = 0;
            var scope = new MotionScope("Start");
            IMotionScope narrow = scope;

            narrow.Remember(delegate { calls++; });
            scope.Cancel();

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Remember_AfterNaturalCompletion_IsDropped()
        {
            // 자연 완료 후 늦게 도착한 등록은 실행하지 않는다 - 자연 완료는 되돌리지 않으므로.
            // 그렇다고 리스트에 쌓아 두면 캡처된 참조까지 영원히 남는다.
            int calls = 0;
            var scope = new MotionScope("T");
            scope.CompleteNaturally();

            scope.Remember(delegate { calls++; });

            Assert.That(calls, Is.EqualTo(0), "자연 완료는 되돌리지 않는다");
            Assert.That(scope.IsDone, Is.True);
            Assert.That(scope.IsCancelled, Is.False);
        }

        [Test]
        public void Cancel_AfterNaturalCompletion_DoesNotRunLateReverts()
        {
            int calls = 0;
            var scope = new MotionScope("T");
            scope.CompleteNaturally();
            scope.Remember(delegate { calls++; });

            scope.Cancel();

            Assert.That(calls, Is.EqualTo(0));
        }

        [Test]
        public void Cancel_RevertThatCancelsAgain_DoesNotLoopOrDoubleRun()
        {
            // 복구가 다른 UI를 건드리다 같은 스코프를 다시 취소시키는 경로가 실제로 있을 수 있다.
            // 이 정합성은 "_done을 복구 루프 전에 세팅한다"는 구현 디테일에 의존하므로 고정한다.
            int calls = 0;
            MotionScope scope = null;
            scope = new MotionScope("T");
            scope.Remember(delegate
            {
                calls++;
                scope.Cancel();
            });

            Assert.DoesNotThrow(delegate { scope.Cancel(); });
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Cancel_RevertThatRemembersAgain_RunsTheNewOneImmediately()
        {
            var order = new List<string>();
            MotionScope scope = null;
            scope = new MotionScope("T");
            scope.Remember(delegate
            {
                order.Add("outer");
                scope.Remember(delegate { order.Add("inner"); });
            });

            Assert.DoesNotThrow(delegate { scope.Cancel(); });
            Assert.That(order, Is.EqualTo(new[] { "outer", "inner" }),
                "취소된 스코프에 늦게 등록되면 그 자리에서 실행된다");
        }
    }
}
