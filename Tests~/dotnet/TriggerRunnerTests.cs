using System.Collections.Generic;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class TriggerRunnerTests
    {
        private FakeGraph _graph;
        private ExecutionLog _log;
        private NodeId _entry;

        [SetUp]
        public void SetUp()
        {
            _graph = new FakeGraph();
            _log = new ExecutionLog();
            _entry = _graph.Add(new RecordingEffect("fx", 1f) { Log = _log });
        }

        private TriggerRunner Make(TriggerPolicy policy)
        {
            return new TriggerRunner("T", policy, delegate
            {
                var scope = new MotionScope("T");
                scope.Begin(new MotionContext(_graph, scope, new FakeSlotResolver(), new FakeLog()), _entry);
                return scope;
            });
        }

        [Test]
        public void FreshRunner_IsNotPlaying()
        {
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            Assert.That(runner.IsPlaying, Is.False);
            Assert.That(runner.Name, Is.EqualTo("T"));
        }

        [Test]
        public void Fire_StartsPlaying()
        {
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            runner.Fire();

            Assert.That(runner.IsPlaying, Is.True);
        }

        [Test]
        public void Restart_CutsCurrentAndStartsOver()
        {
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            runner.Fire();
            runner.Tick(0.9f);
            runner.Fire();
            runner.Tick(0.5f);

            Assert.That(runner.IsPlaying, Is.True, "새로 시작했으므로 아직 돌고 있다");
            Assert.That(_log.Entries, Is.EqualTo(new[] { "fx:start", "fx:start" }),
                "끊긴 연출은 완료를 기록하지 않고, 새 연출이 시작만 기록한다");
        }

        [Test]
        public void Ignore_DropsFireWhilePlaying()
        {
            TriggerRunner runner = Make(TriggerPolicy.Ignore);
            runner.Fire();
            runner.Tick(0.9f);
            runner.Fire();
            runner.Tick(0.1f);

            Assert.That(_log.Entries, Is.EqualTo(new[] { "fx:start", "fx:end" }),
                "첫 발사가 그대로 끝난다");
            Assert.That(runner.IsPlaying, Is.False);
        }

        [Test]
        public void Queue_RunsAgainAfterCurrentFinishes()
        {
            TriggerRunner runner = Make(TriggerPolicy.Queue);
            runner.Fire();
            runner.Fire();

            runner.Tick(1f);
            Assert.That(runner.IsPlaying, Is.True, "대기 중이던 것이 이어서 시작된다");

            runner.Tick(1f);
            Assert.That(_log.Entries, Is.EqualTo(new[] { "fx:start", "fx:end", "fx:start", "fx:end" }));
            Assert.That(runner.IsPlaying, Is.False);
        }

        [Test]
        public void Queue_HoldsAtMostOne()
        {
            // 큐가 무한히 쌓이면 연타 한 번에 연출이 수십 번 돈다.
            TriggerRunner runner = Make(TriggerPolicy.Queue);
            runner.Fire();
            runner.Fire();
            runner.Fire();
            runner.Fire();

            runner.Tick(1f);
            runner.Tick(1f);

            Assert.That(runner.IsPlaying, Is.False, "대기는 하나만 유지한다");
            Assert.That(_log.Entries, Is.EqualTo(new[] { "fx:start", "fx:end", "fx:start", "fx:end" }));
        }

        [Test]
        public void Stop_CancelsAndClearsQueue()
        {
            TriggerRunner runner = Make(TriggerPolicy.Queue);
            runner.Fire();
            runner.Fire();
            runner.Stop();
            runner.Tick(5f);

            Assert.That(runner.IsPlaying, Is.False);
            Assert.That(_log.Entries, Is.EqualTo(new string[0]),
                "첫 스코프는 Tick된 적이 없어 Play가 호출되지 않았고, 대기하던 것도 사라진다");
        }

        [Test]
        public void Stop_WhenIdle_DoesNothing()
        {
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            Assert.DoesNotThrow(delegate { runner.Stop(); });
            Assert.That(runner.IsPlaying, Is.False);
        }

        [Test]
        public void CompletedNaturally_RaisedOnNaturalFinish()
        {
            int raised = 0;
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            runner.CompletedNaturally += delegate { raised++; };

            runner.Fire();
            runner.Tick(1f);

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void CompletedNaturally_NotRaisedOnStop()
        {
            // Start가 끝나면 Loop를 자동 발사하는 규칙이 이 이벤트에 걸린다.
            // 취소에도 걸리면 닫히는 중에 유지 연출이 다시 시작된다.
            int raised = 0;
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            runner.CompletedNaturally += delegate { raised++; };

            runner.Fire();
            runner.Tick(0.5f);
            runner.Stop();
            runner.Tick(1f);

            Assert.That(raised, Is.EqualTo(0));
        }

        [Test]
        public void CompletedNaturally_NotRaisedOnRestart()
        {
            int raised = 0;
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            runner.CompletedNaturally += delegate { raised++; };

            runner.Fire();
            runner.Tick(0.5f);
            runner.Fire();          // 끊고 다시 시작 - 자연 완료가 아니다
            runner.Tick(1f);

            Assert.That(raised, Is.EqualTo(1), "두 번째 발사만 자연 완료했다");
        }

        [Test]
        public void Tick_WhenIdle_DoesNothing()
        {
            TriggerRunner runner = Make(TriggerPolicy.Restart);
            Assert.DoesNotThrow(delegate { runner.Tick(1f); });
            Assert.That(runner.IsPlaying, Is.False);
        }

        [Test]
        public void FireAfterNaturalFinish_StartsAgain()
        {
            TriggerRunner runner = Make(TriggerPolicy.Ignore);
            runner.Fire();
            runner.Tick(1f);
            Assert.That(runner.IsPlaying, Is.False);

            runner.Fire();

            Assert.That(runner.IsPlaying, Is.True, "끝난 뒤의 발사는 Ignore 정책이어도 통과한다");
        }

        [Test]
        public void ScopeThatCompletesImmediately_RaisesCompletedNaturally()
        {
            // 진입 노드가 없거나 즉시 끝나는 그래프. 팩토리가 이미 끝난 스코프를 돌려준다.
            var graph = new FakeGraph();
            int raised = 0;

            var runner = new TriggerRunner("T", TriggerPolicy.Restart, delegate
            {
                var scope = new MotionScope("T");
                scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), NodeId.None);
                return scope;
            });
            runner.CompletedNaturally += delegate { raised++; };

            runner.Fire();

            Assert.That(runner.IsPlaying, Is.False, "이미 끝난 스코프를 붙들고 있으면 안 된다");
            Assert.That(raised, Is.EqualTo(1));
        }
    }
}
