using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionScopeExecutionTests
    {
        private static MotionContext Context(FakeGraph graph, MotionScope scope, FakeLog sink = null)
        {
            return new MotionContext(graph, scope, new FakeSlotResolver(), sink ?? new FakeLog());
        }

        [Test]
        public void Begin_ThenTick_RunsEntryNode()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId entry = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            scope.Begin(Context(graph, scope), entry);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
            Assert.That(scope.IsDone, Is.True);
            Assert.That(scope.IsCancelled, Is.False);
        }

        [Test]
        public void NaturalCompletion_DoesNotRunReverts()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a") { Log = log, RegisterRevert = true });

            var scope = new MotionScope("T");
            scope.Begin(Context(graph, scope), entry);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }),
                "자연 완료 시 되돌리면 페이드인이 끝나자마자 투명해진다");
        }

        [Test]
        public void Cancel_MidFlight_RunsReverts()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a", 1f) { Log = log, RegisterRevert = true });

            var scope = new MotionScope("T");
            scope.Begin(Context(graph, scope), entry);
            scope.Tick(0.5f);
            scope.Cancel();

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:revert" }));
        }

        [Test]
        public void Completed_RaisedOnce_OnNaturalFinish()
        {
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a"));

            int raised = 0;
            var scope = new MotionScope("T");
            scope.Completed += delegate { raised++; };

            scope.Begin(Context(graph, scope), entry);
            scope.Tick(0f);
            scope.Tick(0f);

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void Begin_WithNoneEntry_CompletesImmediately()
        {
            var graph = new FakeGraph();
            var scope = new MotionScope("T");

            scope.Begin(Context(graph, scope), NodeId.None);

            Assert.That(scope.IsDone, Is.True);
            Assert.That(scope.IsCancelled, Is.False, "할 일이 없는 것은 취소가 아니다");
        }

        [Test]
        public void Tick_BeforeBegin_DoesNothing()
        {
            var scope = new MotionScope("T");
            Assert.DoesNotThrow(delegate { scope.Tick(1f); });
            Assert.That(scope.IsDone, Is.False);
        }

        [Test]
        public void Cancel_StopsFurtherTicks()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a", 1f) { Log = log });

            var scope = new MotionScope("T");
            scope.Begin(Context(graph, scope), entry);
            scope.Tick(0.1f);
            scope.Cancel();
            scope.Tick(5f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }));
        }

        [Test]
        public void Begin_Twice_IsIgnored()
        {
            // 실행기가 실수로 두 번 불러도 트리가 두 개 생기면 안 된다.
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            scope.Begin(Context(graph, scope), entry);
            scope.Begin(Context(graph, scope), entry);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void Begin_AfterCancel_IsIgnored()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            scope.Cancel();
            scope.Begin(Context(graph, scope), entry);
            scope.Tick(0f);

            Assert.That(log.Entries.Count, Is.EqualTo(0), "이미 취소된 스코프는 아무것도 시작하지 않는다");
        }

        [Test]
        public void ChainedNodes_RunToCompletion()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId a = graph.Add(new RecordingEffect("a", 1f) { Log = log });
            NodeId b = graph.Add(new RecordingEffect("b", 1f) { Log = log });
            graph.Link(a, b);

            var scope = new MotionScope("T");
            scope.Begin(Context(graph, scope), a);

            scope.Tick(1f);
            Assert.That(scope.IsDone, Is.False, "자식이 아직 돈다");

            scope.Tick(1f);
            Assert.That(scope.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "b:start", "b:end" }));
        }

        [Test]
        public void RevertsRunInReverseOrder_AcrossChainedNodes()
        {
            // 사슬로 이어진 두 노드가 각각 복구를 등록하면, 나중 것이 먼저 되돌아가야 한다.
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            NodeId a = graph.Add(new RecordingEffect("a") { Log = log, RegisterRevert = true });
            NodeId b = graph.Add(new RecordingEffect("b", 1f) { Log = log, RegisterRevert = true });
            graph.Link(a, b);

            var scope = new MotionScope("T");
            scope.Begin(Context(graph, scope), a);
            scope.Tick(0.5f);
            scope.Cancel();

            Assert.That(log.Entries, Is.EqualTo(new[]
            {
                "a:start", "a:end", "b:start", "b:revert", "a:revert"
            }));
        }
    }
}
