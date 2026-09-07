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

        /// <summary>
        /// 트리거 노드 하나. <b>진입점은 이 노드 자신이다</b> — 예전처럼 임의의 노드를
        /// 진입점으로 가리키지 않는다.
        /// </summary>
        private static TriggerNode Trigger(int id, string name)
        {
            return new TriggerNode { Id = new NodeId(id), TriggerName = name };
        }

        private static MotionGraphIndex Build(
            IReadOnlyList<MotionNodeBase> nodes = null,
            IReadOnlyList<NodeLink> links = null,
            IMotionLog log = null)
        {
            return new MotionGraphIndex("g", nodes, links, log);
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
            var index = new MotionGraphIndex(null, null, null, null);
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
        public void SameLogger_AcrossRebuilds_WarnsOnce()
        {
            // 그래프를 편집할 때마다 인덱스는 새로 만들어지지만 로그는 그보다 오래 산다.
            // 억제 키가 인덱스 인스턴스에 묶여 있으면 OnValidate 한 번마다 같은 경고가 다시 찍힌다.
            var sink = new FakeLog();
            var log = new OnceLogger(sink);

            Build(new MotionNodeBase[] { new IdNode(1), new IdNode(1) }, log: log);
            Build(new MotionNodeBase[] { new IdNode(1), new IdNode(1) }, log: log);

            Assert.That(sink.Warnings.Count, Is.EqualTo(1));
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
        public void GetEntry_FindsTriggerNode()
        {
            MotionGraphIndex index = Build(new MotionNodeBase[] { Trigger(5, "Start") });

            Assert.That(index.GetEntry("Start"), Is.EqualTo(new NodeId(5)));
            Assert.That(index.GetEntry("Loop"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void GetEntry_IsCaseSensitive()
        {
            MotionGraphIndex index = Build(new MotionNodeBase[] { Trigger(5, "Start") });

            Assert.That(index.GetEntry("start"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void Triggers_AreDerivedFromNodes()
        {
            // 트리거 목록은 저작값이 아니라 노드에서 계산되는 파생값이다.
            // 진입점은 트리거 노드 자신이므로 죽은 id가 목록에 남을 수 없다.
            var nodes = new MotionNodeBase[] { Trigger(1, "Start"), new IdNode(2), Trigger(3, "End") };

            IReadOnlyList<TriggerDeclaration> triggers = Build(nodes).Triggers;

            Assert.That(triggers.Count, Is.EqualTo(2));
            Assert.That(triggers[0].Name, Is.EqualTo("Start"));
            Assert.That(triggers[0].Entry, Is.EqualTo(new NodeId(1)));
            Assert.That(triggers[1].Name, Is.EqualTo("End"));
            Assert.That(triggers[1].Entry, Is.EqualTo(new NodeId(3)));
        }

        [Test]
        public void DuplicateTrigger_FirstWins_AndLogs()
        {
            var log = new FakeLog();
            var nodes = new MotionNodeBase[] { Trigger(1, "Start"), Trigger(2, "Start") };

            MotionGraphIndex index = Build(nodes, log: log);

            Assert.That(index.GetEntry("Start"), Is.EqualTo(new NodeId(1)));
            Assert.That(index.Triggers.Count, Is.EqualTo(1), "런타임이 러너를 두 번 만들면 안 된다");
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void NamelessTrigger_IsDropped()
        {
            var nodes = new MotionNodeBase[] { new TriggerNode { Id = new NodeId(1) } };

            Assert.That(Build(nodes).Triggers, Is.Empty);
        }

        // --- 옛 형식의 트리거 목록 ----------------------------------------------
        // 마이그레이션을 잊은 그래프가 트리거를 통째로 잃은 채 조용히 도는 것이
        // 가장 나쁜 결과다. 팝업이 열리지 않는데 오류가 하나도 없는 상태가 된다.

        [Test]
        public void LegacyTriggerList_IsNotRead()
        {
            var legacy = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            var index = new MotionGraphIndex("g",
                new MotionNodeBase[] { new IdNode(1) }, null, legacy, new FakeLog());

            Assert.That(index.Triggers, Is.Empty);
            Assert.That(index.GetEntry("Start"), Is.EqualTo(NodeId.None));
        }

        [Test]
        public void LegacyTriggerList_IsAnError()
        {
            var log = new FakeLog();
            var legacy = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            new MotionGraphIndex("g", new MotionNodeBase[] { new IdNode(1) }, null, legacy, log);

            Assert.That(log.Errors.Count, Is.EqualTo(1),
                "경고가 아니라 오류다. 조용히 트리거를 잃는 것이 최악이다");
        }

        [Test]
        public void EmptyLegacyTriggerList_IsSilent()
        {
            var log = new FakeLog();

            new MotionGraphIndex("g", new MotionNodeBase[] { Trigger(1, "Start") },
                null, new TriggerDeclaration[0], log);

            Assert.That(log.Errors, Is.Empty);
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

        // --- 조회 결과가 굳어 있는지 --------------------------------------------
        // "그래프는 실행 상태를 갖지 않는다"는 불변식은 반환값을 호출자가 고칠 수 있으면
        // 무너진다. List를 IReadOnlyList로 캐스팅해 돌려주는 실수를 여기서 막는다.

        [Test]
        public void GetChildren_ResultIsNotAMutableList()
        {
            var nodes = new MotionNodeBase[] { new IdNode(1), new IdNode(2) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };

            IReadOnlyList<NodeId> children = Build(nodes, links).GetChildren(new NodeId(1));

            Assert.That(children, Is.Not.InstanceOf<List<NodeId>>(),
                "되캐스팅해 고칠 수 있는 목록을 돌려주면 안 된다");
        }

        [Test]
        public void Triggers_ResultIsNotAMutableList()
        {
            var nodes = new MotionNodeBase[] { Trigger(1, "Start") };

            Assert.That(Build(nodes).Triggers, Is.Not.InstanceOf<List<TriggerDeclaration>>());
        }

        [Test]
        public void Slots_ResultIsNotAMutableList()
        {
            var nodes = new MotionNodeBase[] { new IdNode(1, "Icon") };

            Assert.That(Build(nodes).Slots, Is.Not.InstanceOf<List<SlotDeclaration>>());
        }

        [Test]
        public void DuplicateIdNode_StillContributesItsSlot()
        {
            // 슬롯 목록과 노드 인덱스는 일부러 다른 집합을 본다. 실행되지 못하는 노드
            // 때문에 사람이 인스펙터에 채워 둔 바인딩이 사라지면 안 되기 때문이다.
            var nodes = new MotionNodeBase[] { new IdNode(1, "Icon"), new IdNode(1, "Label") };

            MotionGraphIndex index = Build(nodes, log: new FakeLog());

            Assert.That(index.GetNode(new NodeId(1)), Is.SameAs(nodes[0]), "노드는 첫 번째만 산다");
            Assert.That(index.Slots.Count, Is.EqualTo(2), "슬롯은 둘 다 선언된다");
        }

        [Test]
        public void Index_DrivesMotionRuntime()
        {
            // Index가 IMotionGraphView로 실제 실행기에 그대로 꽂히는지 확인한다.
            // 진입점은 트리거 노드 자신이고 옛 진입 노드가 그 자식이 된다.
            var log = new ExecutionLog();
            var first = new RecordingEffect("a") { Log = log, Id = new NodeId(2) };
            var second = new RecordingEffect("b") { Log = log, Id = new NodeId(3) };

            var index = new MotionGraphIndex("g",
                new MotionNodeBase[] { Trigger(1, "Start"), first, second },
                new[]
                {
                    new NodeLink(new NodeId(1), new NodeId(2)),
                    new NodeLink(new NodeId(2), new NodeId(3)),
                },
                null);

            var runtime = new MotionRuntime(index, null, null);
            runtime.Fire("Start");
            runtime.Tick(0f);

            Assert.That(log.Entries, Does.Contain("a:start"));
            Assert.That(log.Entries, Does.Contain("b:start"));
        }
    }
}
