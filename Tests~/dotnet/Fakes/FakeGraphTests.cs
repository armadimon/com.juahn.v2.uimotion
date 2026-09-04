using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class FakeGraphTests
    {
        [Test]
        public void AddNode_AssignsSequentialIds()
        {
            var graph = new FakeGraph();
            NodeId a = graph.Add(new RecordingEffect("a"));
            NodeId b = graph.Add(new RecordingEffect("b"));

            Assert.That(a.Value, Is.EqualTo(1));
            Assert.That(b.Value, Is.EqualTo(2));
        }

        [Test]
        public void AddNode_SetsNodeIdOnTheNode()
        {
            var graph = new FakeGraph();
            var node = new RecordingEffect("a");
            NodeId id = graph.Add(node);

            Assert.That(node.Id, Is.EqualTo(id));
        }

        [Test]
        public void GetNode_ReturnsNull_ForUnknownId()
        {
            var graph = new FakeGraph();
            Assert.That(graph.GetNode(new NodeId(99)), Is.Null);
        }

        [Test]
        public void GetNode_ReturnsNull_ForNoneId()
        {
            var graph = new FakeGraph();
            graph.Add(new RecordingEffect("a"));
            Assert.That(graph.GetNode(NodeId.None), Is.Null);
        }

        [Test]
        public void Link_PreservesChildOrder()
        {
            var graph = new FakeGraph();
            NodeId parent = graph.Add(new RecordingEffect("p"));
            NodeId first = graph.Add(new RecordingEffect("1"));
            NodeId second = graph.Add(new RecordingEffect("2"));

            graph.Link(parent, first);
            graph.Link(parent, second);

            Assert.That(graph.GetChildren(parent), Is.EqualTo(new[] { first, second }));
        }

        [Test]
        public void GetChildren_ReturnsEmpty_NotNull_ForLeaf()
        {
            var graph = new FakeGraph();
            NodeId leaf = graph.Add(new RecordingEffect("leaf"));

            Assert.That(graph.GetChildren(leaf), Is.Not.Null);
            Assert.That(graph.GetChildren(leaf).Count, Is.EqualTo(0));
        }

        [Test]
        public void GetEntry_ReturnsNone_ForUnknownTrigger()
        {
            var graph = new FakeGraph();
            Assert.That(graph.GetEntry("Nope"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void DeclareTrigger_MakesEntryFindable()
        {
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("e"));
            graph.DeclareTrigger("Start", entry);

            Assert.That(graph.GetEntry("Start"), Is.EqualTo(entry));
        }

        [Test]
        public void DeclareTrigger_CarriesPolicy()
        {
            var graph = new FakeGraph();
            NodeId entry = graph.Add(new RecordingEffect("e"));
            graph.DeclareTrigger("Q", entry, TriggerPolicy.Queue);

            Assert.That(graph.Triggers.Count, Is.EqualTo(1));
            Assert.That(graph.Triggers[0].Policy, Is.EqualTo(TriggerPolicy.Queue));
        }

        [Test]
        public void RecordingEffect_WithoutDuration_StartsAndEndsImmediately()
        {
            var log = new ExecutionLog();
            var node = new RecordingEffect("a") { Log = log };

            IMotionHandle handle = node.Play(null);

            Assert.That(handle.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void RecordingEffect_WithDuration_EndsOnlyWhenTimeElapses()
        {
            var log = new ExecutionLog();
            var node = new RecordingEffect("a", 1f) { Log = log };

            IMotionHandle handle = node.Play(null);
            Assert.That(handle.IsDone, Is.False);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }));

            handle.Tick(1f);
            Assert.That(handle.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void FakeSlotResolver_ResolvesOnlyBoundNames()
        {
            var resolver = new FakeSlotResolver();
            var target = new object();
            resolver.Bind("Icon", target);

            Assert.That(resolver.Resolve(new SlotRef("Icon")), Is.SameAs(target));
            Assert.That(resolver.Resolve(new SlotRef("Panel")), Is.Null);
            Assert.That(resolver.Resolve(default(SlotRef)), Is.Null);
        }

        [Test]
        public void FakeLog_CollectsWarningsAndErrorsSeparately()
        {
            var log = new FakeLog();
            log.Warn("w");
            log.Error("e");

            Assert.That(log.Warnings, Is.EqualTo(new[] { "w" }));
            Assert.That(log.Errors, Is.EqualTo(new[] { "e" }));
        }

        // --- 미검증이던 경로 -------------------------------------------------
        // 아래 도구들은 이후 실행 엔진 테스트가 기대는 것들이다. 도구가 틀리면
        // 엔진의 버그로 오인하게 되므로 도구 자체를 여기서 검증한다.

        [Test]
        public void RecordingEffect_WithDuration_DoesNotLogEnd_MidFlight()
        {
            // Tick(1f) 한 번으로 끝내버리면 "중간에는 end가 찍히면 안 된다"를 지키지 못한다.
            var log = new ExecutionLog();
            var node = new RecordingEffect("a", 1f) { Log = log };

            IMotionHandle handle = node.Play(null);

            handle.Tick(0.3f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }), "30%에서 end가 찍히면 안 된다");

            handle.Tick(0.3f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }), "60%에서도 마찬가지다");

            handle.Tick(0.4f);
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
            Assert.That(handle.IsDone, Is.True);
        }

        [Test]
        public void RecordingEffect_WithDuration_LogsEndExactlyOnce()
        {
            var log = new ExecutionLog();
            var node = new RecordingEffect("a", 1f) { Log = log };

            IMotionHandle handle = node.Play(null);
            handle.Tick(1f);
            handle.Tick(1f);
            handle.Tick(1f);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void RecordingEffect_RegisterRevert_RunsOnScopeCancel()
        {
            // 이 경로는 지금까지 한 번도 실행된 적이 없었다.
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            var scope = new MotionScope("T");
            var ctx = new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog());

            var node = new RecordingEffect("a", 1f) { Log = log, RegisterRevert = true };
            node.Play(ctx);

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }));

            scope.Cancel();

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:revert" }));
        }

        [Test]
        public void RecordingEffect_WithoutRegisterRevert_RegistersNothing()
        {
            var log = new ExecutionLog();
            var graph = new FakeGraph();
            var scope = new MotionScope("T");
            var ctx = new MotionContext(graph, scope, new FakeSlotResolver(), new FakeLog());

            var node = new RecordingEffect("a", 1f) { Log = log };
            node.Play(ctx);
            scope.Cancel();

            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start" }));
        }

        [Test]
        public void RecordingEffect_RegisterRevert_WithNullContext_DoesNotThrow()
        {
            var log = new ExecutionLog();
            var node = new RecordingEffect("a") { Log = log, RegisterRevert = true };

            Assert.DoesNotThrow(delegate { node.Play(null); });
            Assert.That(log.Entries, Is.EqualTo(new[] { "a:start", "a:end" }));
        }

        [Test]
        public void SlotDependentEffect_SkipsAndWarns_WhenSlotUnbound()
        {
            // 지금까지 인스턴스화된 적조차 없던 도구다.
            var log = new ExecutionLog();
            var sink = new FakeLog();
            var graph = new FakeGraph();
            var scope = new MotionScope("T");
            var ctx = new MotionContext(graph, scope, new FakeSlotResolver(), sink);

            var node = new SlotDependentEffect { Target = new SlotRef("Icon"), Log = log, Name = "icon" };
            IMotionHandle handle = node.Play(ctx);

            Assert.That(MotionHandle.IsSkipped(handle), Is.True);
            Assert.That(log.Entries.Count, Is.EqualTo(0), "건너뛴 노드는 시작을 기록하지 않는다");
            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void SlotDependentEffect_Runs_WhenSlotBound()
        {
            var log = new ExecutionLog();
            var sink = new FakeLog();
            var resolver = new FakeSlotResolver();
            resolver.Bind("Icon", new object());

            var ctx = new MotionContext(new FakeGraph(), new MotionScope("T"), resolver, sink);

            var node = new SlotDependentEffect { Target = new SlotRef("Icon"), Log = log, Name = "icon" };
            IMotionHandle handle = node.Play(ctx);

            Assert.That(MotionHandle.IsSkipped(handle), Is.False);
            Assert.That(handle.IsDone, Is.True);
            Assert.That(log.Entries, Is.EqualTo(new[] { "icon:start" }));
            Assert.That(sink.Warnings.Count, Is.EqualTo(0));
        }

        [Test]
        public void SlotDependentEffect_SelfSlot_IsNotSpecialCased_ByTheResolver()
        {
            // SlotRef.Self는 유효한 이름이지만 FakeSlotResolver는 특별 취급하지 않는다.
            // 실행기 상위 계층이 Self를 채워 주는 구조이므로, 가짜에 바인딩이 없으면 건너뛴다.
            // 이 경계를 명시적으로 못 박아 둔다 - 나중에 "Self는 알아서 되겠지"라는 오해를 막는다.
            var sink = new FakeLog();
            var ctx = new MotionContext(new FakeGraph(), new MotionScope("T"), new FakeSlotResolver(), sink);

            var node = new SlotDependentEffect { Target = SlotRef.Self, Log = new ExecutionLog() };
            IMotionHandle handle = node.Play(ctx);

            Assert.That(MotionHandle.IsSkipped(handle), Is.True);
        }

        [Test]
        public void DeclareSlot_AppearsInSlots()
        {
            var graph = new FakeGraph();
            graph.DeclareSlot("Panel");
            graph.DeclareSlot("Icon");

            Assert.That(graph.Slots.Count, Is.EqualTo(2));
            Assert.That(graph.Slots[0].Name, Is.EqualTo("Panel"));
            Assert.That(graph.Slots[1].Name, Is.EqualTo("Icon"));
        }

        [Test]
        public void FreshGraph_HasNoSlotsOrTriggers()
        {
            var graph = new FakeGraph();

            Assert.That(graph.Slots.Count, Is.EqualTo(0));
            Assert.That(graph.Triggers.Count, Is.EqualTo(0));
        }
    }
}
