using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class DelayRepeatTests
    {
        private static MotionScope Begin(FakeGraph graph, NodeId entry)
        {
            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);
            return scope;
        }

        [Test]
        public void Delay_DoesNotOwnChildren()
        {
            // 자식 전파는 NodeRun에 맡긴다. Delay는 기다리기만 한다.
            Assert.That(new DelayNode().OwnsChildren, Is.False);
        }

        [Test]
        public void Repeat_OwnsChildren()
        {
            Assert.That(new RepeatNode().OwnsChildren, Is.True);
        }

        [Test]
        public void Delay_HoldsChildrenUntilElapsed()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId delay = graph.Add(new DelayNode { Seconds = 1f });
            graph.Link(delay, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, delay);

            scope.Tick(0.5f);
            Assert.That(log.Entries.Count, Is.EqualTo(0), "지연 중에는 자식이 시작되지 않는다");

            scope.Tick(0.5f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Delay_ZeroSeconds_PassesThroughImmediately()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId delay = graph.Add(new DelayNode { Seconds = 0f });
            graph.Link(delay, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, delay);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Delay_NegativeSeconds_PassesThroughImmediately()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId delay = graph.Add(new DelayNode { Seconds = -3f });
            graph.Link(delay, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, delay);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void Delay_WithNoChildren_JustCompletes()
        {
            var graph = new FakeGraph();
            NodeId delay = graph.Add(new DelayNode { Seconds = 1f });

            MotionScope scope = Begin(graph, delay);
            scope.Tick(1f);

            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Repeat_RunsChildrenGivenNumberOfTimes()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = 3 });
            graph.Link(repeat, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[]
            {
                "a:start", "a:end", "a:start", "a:end", "a:start", "a:end"
            }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Repeat_ZeroCount_CompletesWithoutRunning()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = 0 });
            graph.Link(repeat, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(0f);

            Assert.That(log.Entries.Count, Is.EqualTo(0));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Repeat_WithNoChildren_CompletesImmediately()
        {
            var graph = new FakeGraph();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(0f);

            Assert.That(scope.IsDone, Is.True, "돌릴 자식이 없으면 무한 반복도 끝난다");
        }

        [Test]
        public void Repeat_Infinite_NeverCompletes()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            graph.Link(repeat, graph.Add(new RecordingEffect("a", 1f) { Log = log }));

            MotionScope scope = Begin(graph, repeat);

            for (int i = 0; i < 10; i++)
            {
                scope.Tick(1f);
            }

            Assert.That(scope.IsDone, Is.False, "무한 반복은 스스로 끝나지 않는다");
            Assert.That(log.Entries.Count, Is.GreaterThanOrEqualTo(20), "여러 사이클이 실제로 돌았다");
        }

        [Test]
        public void Repeat_Infinite_StopsOnCancel()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            graph.Link(repeat, graph.Add(new RecordingEffect("a", 1f) { Log = log }));

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(1f);
            int before = log.Entries.Count;

            scope.Cancel();
            scope.Tick(5f);

            Assert.That(log.Entries.Count, Is.EqualTo(before));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Repeat_DropsLeftoverTime_WhenCycleCompletes()
        {
            // 알려진 한계를 명시적으로 못 박는다.
            // 1초짜리 자식을 2.5초로 진행시켜도 두 번 도는 게 아니라 한 번만 끝나고,
            // 다음 사이클은 0에서 시작한다. 남은 1.5초는 버려진다.
            //
            // 넘기려면 NodeRun이 소비한 시간을 보고해야 하는데 실행 모델 전체를 건드리는
            // 변경이다. UI 장식 루프에서 사이클당 최대 한 프레임 손실은 눈에 보이지 않는다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            graph.Link(repeat, graph.Add(new RecordingEffect("a", 1f) { Log = log }));

            MotionScope scope = Begin(graph, repeat);
            scope.Tick(2.5f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "a:start" }),
                "한 사이클만 끝나고 다음 사이클이 0에서 시작한다");
        }

        [Test]
        public void Repeat_InstantChild_Infinite_DoesNotHangTheFrame()
        {
            // 지속시간 0짜리 자식을 무한 반복으로 걸면 상한이 없을 때 프레임이 영원히 안 끝난다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = RepeatNode.Infinite });
            graph.Link(repeat, graph.Add(new RecordingEffect("a") { Log = log }));

            MotionScope scope = Begin(graph, repeat);

            Assert.DoesNotThrow(delegate { scope.Tick(0f); });
            Assert.That(scope.IsDone, Is.False);
            Assert.That(log.Entries.Count, Is.LessThanOrEqualTo(RepeatNode.MaxCyclesPerTick * 2),
                "한 Tick에서 도는 사이클 수에 상한이 있어야 한다");
        }

        [Test]
        public void Repeat_MultipleChildren_CycleEndsWhenAllFinish()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId repeat = graph.Add(new RepeatNode { Count = 2 });
            graph.Link(repeat, graph.Add(new RecordingEffect("a", 1f) { Log = log }));
            graph.Link(repeat, graph.Add(new RecordingEffect("b", 2f) { Log = log }));

            MotionScope scope = Begin(graph, repeat);

            scope.Tick(1f);
            Assert.That(scope.IsDone, Is.False, "b가 아직 돈다 - 사이클이 안 끝났다");

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[]
            {
                "a:start", "b:start", "a:end", "b:end", "a:start", "b:start"
            }), "첫 사이클이 끝나고 두 번째가 0에서 시작한다");
        }
    }
}
