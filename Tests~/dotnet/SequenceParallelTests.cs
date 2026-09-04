using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class SequenceParallelTests
    {
        private static MotionScope Begin(FakeGraph graph, NodeId entry)
        {
            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog()), entry);
            return scope;
        }

        [Test]
        public void Sequence_OwnsChildren()
        {
            Assert.That(new SequenceNode().OwnsChildren, Is.True);
        }

        [Test]
        public void Parallel_OwnsChildren()
        {
            Assert.That(new ParallelNode().OwnsChildren, Is.True);
        }

        [Test]
        public void Sequence_RunsChildrenInOrder_OneAtATime()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId seq = graph.Add(new SequenceNode());
            graph.Link(seq, graph.Add(new RecordingEffect("a", 1f) { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("b", 1f) { Log = log }));

            MotionScope scope = Begin(graph, seq);

            scope.Tick(0f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }), "b가 아직 시작되면 안 된다");

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "b:start" }));

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "b:start", "b:end" }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Sequence_WithNoChildren_CompletesImmediately()
        {
            var graph = new FakeGraph();
            NodeId seq = graph.Add(new SequenceNode());

            MotionScope scope = Begin(graph, seq);
            scope.Tick(0f);

            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Sequence_InstantChildren_AllFinishInOneTick()
        {
            // 즉시 끝나는 자식이 여럿 이어져도 한 프레임에 다 소화해야 한다.
            // 안 그러면 자식 하나당 한 프레임씩 밀린다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId seq = graph.Add(new SequenceNode());
            graph.Link(seq, graph.Add(new RecordingEffect("a") { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("b") { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("c") { Log = log }));

            MotionScope scope = Begin(graph, seq);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[]
            {
                "a:start", "a:end", "b:start", "b:end", "c:start", "c:end"
            }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Sequence_Cancel_StopsCurrentChild_AndNeverStartsNext()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId seq = graph.Add(new SequenceNode());
            graph.Link(seq, graph.Add(new RecordingEffect("a", 1f) { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("b", 1f) { Log = log }));

            MotionScope scope = Begin(graph, seq);
            scope.Tick(0.5f);
            scope.Cancel();
            scope.Tick(5f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }), "b는 영영 시작되지 않는다");
        }

        [Test]
        public void Sequence_ChildWithItsOwnChildren_FinishesWholeSubtreeFirst()
        {
            // 자식의 자식은 NodeRun이 이어 붙인다. Sequence는 "그 서브트리 전체가 끝날 때까지" 기다린다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId seq = graph.Add(new SequenceNode());
            NodeId a = graph.Add(new RecordingEffect("a") { Log = log });
            NodeId aChild = graph.Add(new RecordingEffect("a2", 1f) { Log = log });
            NodeId b = graph.Add(new RecordingEffect("b") { Log = log });
            graph.Link(seq, a);
            graph.Link(seq, b);
            graph.Link(a, aChild);

            MotionScope scope = Begin(graph, seq);

            scope.Tick(0f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "a2:start" }),
                "a의 자식이 도는 동안 b는 시작되지 않는다");

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[]
            {
                "a:start", "a:end", "a2:start", "a2:end", "b:start", "b:end"
            }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Parallel_StartsAllChildrenAtOnce()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId par = graph.Add(new ParallelNode());
            graph.Link(par, graph.Add(new RecordingEffect("a", 1f) { Log = log }));
            graph.Link(par, graph.Add(new RecordingEffect("b", 2f) { Log = log }));

            MotionScope scope = Begin(graph, par);

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "b:start", "a:end" }));
            Assert.That(scope.IsDone, Is.False, "가장 긴 자식이 끝나야 완료다");

            scope.Tick(1f);
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Parallel_WithNoChildren_CompletesImmediately()
        {
            var graph = new FakeGraph();
            NodeId par = graph.Add(new ParallelNode());

            MotionScope scope = Begin(graph, par);
            scope.Tick(0f);

            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Parallel_ChildrenCanHaveTheirOwnChildren()
        {
            // 자식의 자식은 NodeRun이 이어 붙인다. Parallel이 관여하지 않는다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId par = graph.Add(new ParallelNode());
            NodeId a = graph.Add(new RecordingEffect("a") { Log = log });
            NodeId aChild = graph.Add(new RecordingEffect("a2") { Log = log });
            graph.Link(par, a);
            graph.Link(a, aChild);

            MotionScope scope = Begin(graph, par);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end", "a2:start", "a2:end" }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void Parallel_Cancel_StopsAllChildren()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId par = graph.Add(new ParallelNode());
            graph.Link(par, graph.Add(new RecordingEffect("a", 1f) { Log = log }));
            graph.Link(par, graph.Add(new RecordingEffect("b", 1f) { Log = log }));

            MotionScope scope = Begin(graph, par);
            scope.Tick(0.5f);
            scope.Cancel();
            scope.Tick(5f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "b:start" }));
        }

        [Test]
        public void NestedSequenceInsideParallel_Works()
        {
            // 흐름 노드를 중첩해도 조율이 깨지지 않는지 본다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId par = graph.Add(new ParallelNode());
            NodeId seq = graph.Add(new SequenceNode());
            NodeId solo = graph.Add(new RecordingEffect("solo", 3f) { Log = log });
            graph.Link(par, seq);
            graph.Link(par, solo);
            graph.Link(seq, graph.Add(new RecordingEffect("s1", 1f) { Log = log }));
            graph.Link(seq, graph.Add(new RecordingEffect("s2", 1f) { Log = log }));

            MotionScope scope = Begin(graph, par);

            scope.Tick(1f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "s1:start", "solo:start", "s1:end", "s2:start" }));

            scope.Tick(1f);
            Assert.That(scope.IsDone, Is.False, "solo가 아직 1초 남았다");

            scope.Tick(1f);
            Assert.That(scope.IsDone, Is.True);
        }
    }
}
