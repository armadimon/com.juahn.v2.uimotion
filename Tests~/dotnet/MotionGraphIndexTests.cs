using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionGraphIndexTests
    {
        private sealed class IdNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Target;

            public IdNode(int id, string slotName = null)
            {
                Id = new NodeId(id);
                Target = slotName == null ? default : new SlotRef(slotName);
            }

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private static MotionGraphIndex Build(
            IReadOnlyList<MotionNodeBase> nodes = null,
            IReadOnlyList<NodeLink> links = null,
            IReadOnlyList<TriggerDeclaration> triggers = null,
            IMotionLog log = null)
        {
            return new MotionGraphIndex("g", nodes, links, triggers, log);
        }

        [Test]
        public void EmptyGraph_IsSafe()
        {
            MotionGraphIndex index = Build();

            Assert.That(index.GraphName, Is.EqualTo("g"));
            Assert.That(index.Triggers, Is.Empty);
            Assert.That(index.Slots, Is.Empty);
            Assert.That(index.GetNode(new NodeId(1)), Is.Null);
            Assert.That(index.GetEntry("Start"), Is.EqualTo(NodeId.None));
            Assert.That(index.GetChildren(new NodeId(1)), Is.Empty);
        }

        [Test]
        public void BlankName_FallsBack()
        {
            var index = new MotionGraphIndex(null, null, null, null, null);
            Assert.That(index.GraphName, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void GetNode_FindsByDeclaredId()
        {
            var a = new IdNode(7);
            MotionGraphIndex index = Build(new MotionNodeBase[] { a });

            Assert.That(index.GetNode(new NodeId(7)), Is.SameAs(a));
            Assert.That(index.GetNode(new NodeId(8)), Is.Null);
        }

        [Test]
        public void NullNode_IsSkipped_RestSurvives()
        {
            // 결손 노드. 타입이 사라진 그래프를 열면 SerializeReference가 null을 남긴다.
            var a = new IdNode(2);
            MotionGraphIndex index = Build(new MotionNodeBase[] { null, a, null });

            Assert.That(index.GetNode(new NodeId(2)), Is.SameAs(a));
        }

        [Test]
        public void NodeWithoutId_IsSkippedAndLogged()
        {
            var log = new FakeLog();
            var orphan = new IdNode(0);

            MotionGraphIndex index = Build(new MotionNodeBase[] { orphan }, log: log);

            Assert.That(index.GetNode(NodeId.None), Is.Null);
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateId_FirstWins_AndLogs()
        {
            var log = new FakeLog();
            var first = new IdNode(1);
            var second = new IdNode(1);

            MotionGraphIndex index = Build(new MotionNodeBase[] { first, second }, log: log);

            Assert.That(index.GetNode(new NodeId(1)), Is.SameAs(first));
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void GetChildren_PreservesLinkOrder()
        {
            // 같은 부모에서 나가는 간선의 배열 순서가 곧 Sequence의 실행 순서다.
            var nodes = new MotionNodeBase[] { new IdNode(1), new IdNode(2), new IdNode(3), new IdNode(4) };
            var links = new[]
            {
                new NodeLink(new NodeId(1), new NodeId(3)),
                new NodeLink(new NodeId(1), new NodeId(2)),
                new NodeLink(new NodeId(1), new NodeId(4)),
            };

            IReadOnlyList<NodeId> children = Build(nodes, links).GetChildren(new NodeId(1));

            Assert.That(children.Count, Is.EqualTo(3));
            Assert.That(children[0], Is.EqualTo(new NodeId(3)));
            Assert.That(children[1], Is.EqualTo(new NodeId(2)));
            Assert.That(children[2], Is.EqualTo(new NodeId(4)));
        }

        [Test]
        public void GetChildren_NoChildren_ReturnsEmptyNotNull()
        {
            MotionGraphIndex index = Build(new MotionNodeBase[] { new IdNode(1) });

            IReadOnlyList<NodeId> children = index.GetChildren(new NodeId(1));

            Assert.That(children, Is.Not.Null);
            Assert.That(children, Is.Empty);
        }

        [Test]
        public void InvalidLink_IsDropped()
        {
            var nodes = new MotionNodeBase[] { new IdNode(1) };
            var links = new[] { new NodeLink(new NodeId(1), NodeId.None) };

            Assert.That(Build(nodes, links).GetChildren(new NodeId(1)), Is.Empty);
        }

        [Test]
        public void LinkToMissingNode_IsKept()
        {
            // 구조적으로는 멀쩡한 간선이다. 노드가 결손인지는 NodeRun이 판단한다 —
            // "없는 노드"를 두 곳에서 다르게 처리하면 동작을 추론할 수 없게 된다.
            var nodes = new MotionNodeBase[] { new IdNode(1) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(99)) };

            IReadOnlyList<NodeId> children = Build(nodes, links).GetChildren(new NodeId(1));

            Assert.That(children.Count, Is.EqualTo(1));
            Assert.That(children[0], Is.EqualTo(new NodeId(99)));
        }

        [Test]
        public void GetEntry_FindsDeclaredTrigger()
        {
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(5)) };
            MotionGraphIndex index = Build(triggers: triggers);

            Assert.That(index.GetEntry("Start"), Is.EqualTo(new NodeId(5)));
            Assert.That(index.GetEntry("Loop"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void GetEntry_IsCaseSensitive()
        {
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(5)) };

            Assert.That(Build(triggers: triggers).GetEntry("start"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void DuplicateTrigger_FirstWins_AndLogs()
        {
            var log = new FakeLog();
            var triggers = new[]
            {
                new TriggerDeclaration("Start", new NodeId(1)),
                new TriggerDeclaration("Start", new NodeId(2)),
            };

            MotionGraphIndex index = Build(triggers: triggers, log: log);

            Assert.That(index.GetEntry("Start"), Is.EqualTo(new NodeId(1)));
            Assert.That(index.Triggers.Count, Is.EqualTo(1), "런타임이 러너를 두 번 만들면 안 된다");
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void NamelessTrigger_IsDropped()
        {
            var triggers = new[] { new TriggerDeclaration(null, new NodeId(1)) };

            Assert.That(Build(triggers: triggers).Triggers, Is.Empty);
        }

        [Test]
        public void NullTriggerEntry_IsDropped()
        {
            var triggers = new TriggerDeclaration[] { null };

            Assert.That(Build(triggers: triggers).Triggers, Is.Empty);
        }

        [Test]
        public void Slots_AreDerivedFromNodes()
        {
            var nodes = new MotionNodeBase[] { new IdNode(1, "Icon"), new IdNode(2, "Label") };

            IReadOnlyList<SlotDeclaration> slots = Build(nodes).Slots;

            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].Name, Is.EqualTo("Icon"));
            Assert.That(slots[1].Name, Is.EqualTo("Label"));
            Assert.That(slots[0].RequiredType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void Index_DrivesMotionRuntime()
        {
            // Index가 IMotionGraphView로 실제 실행기에 그대로 꽂히는지 확인한다.
            var log = new ExecutionLog();
            var first = new RecordingEffect("a") { Log = log, Id = new NodeId(1) };
            var second = new RecordingEffect("b") { Log = log, Id = new NodeId(2) };

            var index = new MotionGraphIndex("g",
                new MotionNodeBase[] { first, second },
                new[] { new NodeLink(new NodeId(1), new NodeId(2)) },
                new[] { new TriggerDeclaration("Start", new NodeId(1)) },
                null);

            var runtime = new MotionRuntime(index, null, null);
            runtime.Fire("Start");
            runtime.Tick(0f);

            Assert.That(log.Entries, Does.Contain("a:start"));
            Assert.That(log.Entries, Does.Contain("b:start"));
        }
    }
}
