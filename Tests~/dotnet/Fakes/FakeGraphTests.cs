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
    }
}
