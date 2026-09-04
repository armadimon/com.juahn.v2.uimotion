using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class NodeRunTests
    {
        private static MotionContext Context(FakeGraph graph, MotionScope scope)
        {
            return new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog());
        }

        [Test]
        public void InstantNode_IsDoneAfterFirstTick()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId id = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), id);

            run.Tick(0f);

            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void ChildrenStart_AfterParentCompletes()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId parent = graph.Add(new RecordingEffect("p", 1f) { Log = log });
            NodeId child = graph.Add(new RecordingEffect("c") { Log = log });
            graph.Link(parent, child);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), parent);

            run.Tick(0.5f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "p:start" }), "부모가 도는 중엔 자식이 시작되지 않는다");

            run.Tick(0.5f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "p:start", "p:end", "c:start", "c:end" }));
            Assert.That(run.IsDone, Is.True);
        }

        [Test]
        public void MultipleChildren_StartTogether()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId parent = graph.Add(new RecordingEffect("p") { Log = log });
            NodeId a = graph.Add(new RecordingEffect("a", 1f) { Log = log });
            NodeId b = graph.Add(new RecordingEffect("b", 1f) { Log = log });
            graph.Link(parent, a);
            graph.Link(parent, b);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), parent);

            run.Tick(0f);
            Assert.That(run.IsDone, Is.False, "자식이 도는 동안은 끝나지 않는다");

            run.Tick(1f);
            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "p:start", "p:end", "a:start", "b:start", "a:end", "b:end" }));
        }

        [Test]
        public void OwningNode_DoesNotGetAutoChildPropagation()
        {
            // OwnsChildren이 true면 실행기가 자식을 건드리지 않는다.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId parent = graph.Add(new OwningNoop());
            NodeId child = graph.Add(new RecordingEffect("c") { Log = log });
            graph.Link(parent, child);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), parent);

            run.Tick(0f);

            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries.Count, Is.EqualTo(0), "소유 노드의 자식을 실행기가 시작하면 안 된다");
        }

        [Test]
        public void MissingNode_CompletesImmediately()
        {
            // 노드 타입이 사라진 그래프를 열어도 나머지는 돌아야 한다.
            var graph = new FakeGraph();
            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), new NodeId(99));

            run.Tick(0f);

            Assert.That(run.IsDone, Is.True);
        }

        [Test]
        public void Cancel_StopsSelfAndChildren()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId parent = graph.Add(new RecordingEffect("p") { Log = log });
            NodeId child = graph.Add(new RecordingEffect("c", 1f) { Log = log });
            graph.Link(parent, child);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), parent);

            run.Tick(0f);
            run.Cancel();
            run.Tick(1f);

            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "p:start", "p:end", "c:start" }),
                "취소 후에는 자식이 끝나지 않는다");
        }

        [Test]
        public void Cancel_BeforeFirstTick_DoesNotStartAnything()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId id = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), id);

            run.Cancel();
            run.Tick(0f);

            Assert.That(run.IsDone, Is.True);
            Assert.That(log.Entries.Count, Is.EqualTo(0), "시작도 안 한 노드를 취소하면 아무것도 돌지 않는다");
        }

        [Test]
        public void TickAfterDone_DoesNothing()
        {
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId id = graph.Add(new RecordingEffect("a") { Log = log });

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), id);

            run.Tick(0f);
            run.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void DeepChain_PropagatesThroughGenerations()
        {
            // a → b → c 사슬. 각각 자식 하나씩.
            var graph = new FakeGraph();
            var log = new ExecutionLog();
            NodeId a = graph.Add(new RecordingEffect("a") { Log = log });
            NodeId b = graph.Add(new RecordingEffect("b") { Log = log });
            NodeId c = graph.Add(new RecordingEffect("c") { Log = log });
            graph.Link(a, b);
            graph.Link(b, c);

            var scope = new MotionScope("T");
            var run = new NodeRun(Context(graph, scope), a);

            run.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[]
            {
                "a:start", "a:end", "b:start", "b:end", "c:start", "c:end"
            }), "즉시 끝나는 사슬은 한 프레임에 전부 소화된다");
            Assert.That(run.IsDone, Is.True);
        }

        [Test]
        public void Context_ExposesGraphScopeAndLog()
        {
            var graph = new FakeGraph();
            var scope = new MotionScope("T");
            var resolver = new FakeSlotResolver();
            var log = new FakeLog();
            var ctx = new MotionContext(graph, scope, resolver, log);

            Assert.That(ctx.Graph, Is.SameAs(graph));
            Assert.That(ctx.Scope, Is.SameAs(scope));
            Assert.That(ctx.Log, Is.SameAs(log));
        }

        [Test]
        public void Context_ResolveSlot_UsesResolver()
        {
            var resolver = new FakeSlotResolver();
            var target = new object();
            resolver.Bind("Icon", target);

            var ctx = new MotionContext(new FakeGraph(), new MotionScope("T"), resolver, new FakeLog());

            Assert.That(ctx.ResolveSlot(new SlotRef("Icon")), Is.SameAs(target));
            Assert.That(ctx.ResolveSlot(new SlotRef("Nope")), Is.Null);
        }

        [Test]
        public void Context_NullResolver_ReturnsNull()
        {
            var ctx = new MotionContext(new FakeGraph(), new MotionScope("T"), null, new FakeLog());
            Assert.That(ctx.ResolveSlot(SlotRef.Self), Is.Null);
        }

        private sealed class OwningNoop : MotionFlowNode
        {
            public override bool OwnsChildren => true;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }
    }
}
