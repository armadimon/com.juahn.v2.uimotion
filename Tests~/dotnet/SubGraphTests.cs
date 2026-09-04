using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    /// <summary>테스트용 서브그래프 노드. Unity 계층은 MotionGraph 에셋을 들고 같은 일을 한다.</summary>
    public sealed class FakeSubGraphNode : SubGraphNode
    {
        public IMotionGraphView Target;

        protected override IMotionGraphView ResolveGraph()
        {
            return Target;
        }
    }

    [TestFixture]
    public sealed class SubGraphTests
    {
        private static MotionScope Begin(FakeGraph graph, NodeId entry, FakeLog sink = null)
        {
            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(graph, scope, new FakeSlotResolver(), sink ?? new FakeLog()), entry);
            return scope;
        }

        [Test]
        public void SubGraph_RunsInnerGraphEntry()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph { GraphName = "inner" };
            NodeId innerEntry = inner.Add(new RecordingEffect("in") { Log = log });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph { GraphName = "outer" };
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });

            MotionScope scope = Begin(outer, sub);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "in:start", "in:end" }));
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void SubGraph_CompletesOnlyAfterInnerFinishes()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph();
            NodeId innerEntry = inner.Add(new RecordingEffect("in", 1f) { Log = log });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });

            MotionScope scope = Begin(outer, sub);
            scope.Tick(0.5f);
            Assert.That(scope.IsDone, Is.False);

            scope.Tick(0.5f);
            Assert.That(scope.IsDone, Is.True);
        }

        [Test]
        public void SubGraph_PropagatesToItsOwnChildren_AfterInnerFinishes()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph();
            NodeId innerEntry = inner.Add(new RecordingEffect("in", 1f) { Log = log });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });
            outer.Link(sub, outer.Add(new RecordingEffect("after") { Log = log }));

            MotionScope scope = Begin(outer, sub);
            scope.Tick(1f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "in:start", "in:end", "after:start", "after:end" }));
        }

        [Test]
        public void SubGraph_WithNoTarget_SkipsAndWarns()
        {
            var sink = new FakeLog();
            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = null });

            MotionScope scope = Begin(outer, sub, sink);
            scope.Tick(0f);

            Assert.That(scope.IsDone, Is.True);
            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void SubGraph_WithMissingEntryTrigger_SkipsAndWarns()
        {
            var sink = new FakeLog();
            var inner = new FakeGraph { GraphName = "inner" };
            inner.Add(new RecordingEffect("in"));
            // 트리거를 선언하지 않는다

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });

            MotionScope scope = Begin(outer, sub, sink);
            scope.Tick(0f);

            Assert.That(scope.IsDone, Is.True);
            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void SubGraph_UsesCustomEntryTrigger()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph();
            NodeId innerEntry = inner.Add(new RecordingEffect("in") { Log = log });
            inner.DeclareTrigger("Custom", innerEntry);

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner, EntryTrigger = "Custom" });

            MotionScope scope = Begin(outer, sub);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "in:start", "in:end" }));
        }

        [Test]
        public void SubGraph_Cancel_StopsInnerGraph()
        {
            var log = new ExecutionLog();

            var inner = new FakeGraph();
            NodeId innerEntry = inner.Add(new RecordingEffect("in", 1f) { Log = log, RegisterRevert = true });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });

            MotionScope scope = Begin(outer, sub);
            scope.Tick(0.5f);
            scope.Cancel();

            Assert.That(log.Entries, Is.EqualTo(new[] { "in:start", "in:revert" }),
                "바깥 스코프를 취소하면 안쪽 그래프의 복구도 실행돼야 한다");
        }

        [Test]
        public void SubGraph_SharesSlotsWithOuterGraph()
        {
            // 같은 플레이어의 같은 계층이므로 슬롯은 공유한다.
            var log = new ExecutionLog();
            var resolver = new FakeSlotResolver();
            resolver.Bind("Icon", new object());

            var inner = new FakeGraph();
            NodeId innerEntry = inner.Add(new SlotDependentEffect
            {
                Target = new SlotRef("Icon"), Log = log, Name = "icon"
            });
            inner.DeclareTrigger(MotionRuntime.StartTrigger, innerEntry);

            var outer = new FakeGraph();
            NodeId sub = outer.Add(new FakeSubGraphNode { Target = inner });

            var scope = new MotionScope("T");
            scope.Begin(new MotionContext(outer, scope, resolver, new FakeLog()), sub);
            scope.Tick(0f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "icon:start" }),
                "안쪽 노드가 바깥의 슬롯 바인딩을 그대로 본다");
        }

        [Test]
        public void SubGraph_DepthLimit_StopsRuntimeRecursion()
        {
            // 에디터가 순환을 막지만, 손으로 만든 에셋이 새어 들어올 수 있다.
            // 런타임이 스택 오버플로로 죽지 않고 경고 후 멈춰야 한다.
            var sink = new FakeLog();
            var graph = new FakeGraph { GraphName = "self" };
            var node = new FakeSubGraphNode();
            NodeId sub = graph.Add(node);
            graph.DeclareTrigger(MotionRuntime.StartTrigger, sub);
            node.Target = graph;

            MotionScope scope = Begin(graph, sub, sink);

            Assert.DoesNotThrow(delegate { scope.Tick(0f); });
            Assert.That(sink.Warnings.Count, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void CycleDetector_FindsDirectSelfReference()
        {
            var graph = new FakeGraph { GraphName = "self" };
            var node = new FakeSubGraphNode();
            graph.Add(node);
            node.Target = graph;

            Assert.That(GraphCycleDetector.HasCycle(graph), Is.True);
        }

        [Test]
        public void CycleDetector_FindsIndirectCycle()
        {
            var a = new FakeGraph { GraphName = "a" };
            var b = new FakeGraph { GraphName = "b" };

            a.Add(new FakeSubGraphNode { Target = b });
            b.Add(new FakeSubGraphNode { Target = a });

            Assert.That(GraphCycleDetector.HasCycle(a), Is.True);
        }

        [Test]
        public void CycleDetector_AllowsDiamond()
        {
            // 같은 서브그래프를 두 곳에서 쓰는 것은 순환이 아니다. 그게 재사용의 목적이다.
            var shared = new FakeGraph { GraphName = "shared" };
            shared.Add(new RecordingEffect("s"));

            var root = new FakeGraph { GraphName = "root" };
            root.Add(new FakeSubGraphNode { Target = shared });
            root.Add(new FakeSubGraphNode { Target = shared });

            Assert.That(GraphCycleDetector.HasCycle(root), Is.False);
        }

        [Test]
        public void CycleDetector_AllowsDeepChain()
        {
            var a = new FakeGraph { GraphName = "a" };
            var b = new FakeGraph { GraphName = "b" };
            var c = new FakeGraph { GraphName = "c" };

            a.Add(new FakeSubGraphNode { Target = b });
            b.Add(new FakeSubGraphNode { Target = c });
            c.Add(new RecordingEffect("leaf"));

            Assert.That(GraphCycleDetector.HasCycle(a), Is.False);
        }

        [Test]
        public void CycleDetector_IgnoresUnresolvedSubGraphs()
        {
            var root = new FakeGraph();
            root.Add(new FakeSubGraphNode { Target = null });

            Assert.That(GraphCycleDetector.HasCycle(root), Is.False);
        }

        [Test]
        public void CycleDetector_NullGraph_IsNotACycle()
        {
            Assert.That(GraphCycleDetector.HasCycle(null), Is.False);
        }

        [Test]
        public void CycleDetector_GraphWithoutSubGraphs_IsNotACycle()
        {
            var graph = new FakeGraph();
            graph.Add(new RecordingEffect("a"));
            graph.Add(new SequenceNode());

            Assert.That(GraphCycleDetector.HasCycle(graph), Is.False);
        }
    }
}
