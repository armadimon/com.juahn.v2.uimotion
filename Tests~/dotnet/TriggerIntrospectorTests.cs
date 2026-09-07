using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class TriggerIntrospectorTests
    {
        private sealed class PlainNode : MotionEffectNode
        {
            public PlainNode(int id) { Id = new NodeId(id); }
            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private static TriggerNode Trigger(int id, string name,
            TriggerPolicy policy = TriggerPolicy.Restart)
        {
            return new TriggerNode { Id = new NodeId(id), TriggerName = name, Policy = policy };
        }

        private static List<TriggerDeclaration> Collect(params MotionNodeBase[] nodes)
        {
            var into = new List<TriggerDeclaration>();
            TriggerIntrospector.Collect(nodes, into, null);
            return into;
        }

        [Test]
        public void NullNodes_YieldsEmpty()
        {
            var into = new List<TriggerDeclaration>();
            TriggerIntrospector.Collect(null, into, null);
            Assert.That(into, Is.Empty);
        }

        [Test]
        public void TriggerNode_BecomesItsOwnEntry()
        {
            // 진입점이 노드 자신이다. 그래서 "어느 노드가 진입점인가"가 캔버스에서 보인다.
            List<TriggerDeclaration> triggers = Collect(Trigger(7, "Start"));

            Assert.That(triggers.Count, Is.EqualTo(1));
            Assert.That(triggers[0].Name, Is.EqualTo("Start"));
            Assert.That(triggers[0].Entry, Is.EqualTo(new NodeId(7)));
            Assert.That(triggers[0].Policy, Is.EqualTo(TriggerPolicy.Restart));
        }

        [Test]
        public void PolicyComesFromTheNode()
        {
            List<TriggerDeclaration> triggers = Collect(Trigger(1, "Click", TriggerPolicy.Ignore));

            Assert.That(triggers[0].Policy, Is.EqualTo(TriggerPolicy.Ignore));
        }

        [Test]
        public void NonTriggerNodes_AreIgnored()
        {
            Assert.That(Collect(new PlainNode(1), new PlainNode(2)), Is.Empty);
        }

        [Test]
        public void NullNodeInArray_IsSkipped()
        {
            // 결손 노드. 타입이 사라진 그래프를 열면 SerializeReference가 null을 남긴다.
            List<TriggerDeclaration> triggers = Collect(null, Trigger(2, "Start"), null);

            Assert.That(triggers.Count, Is.EqualTo(1));
        }

        [Test]
        public void NamelessTrigger_IsSkippedAndWarned()
        {
            var log = new FakeLog();
            var into = new List<TriggerDeclaration>();

            TriggerIntrospector.Collect(new MotionNodeBase[] { Trigger(1, "  ") }, into, log);

            Assert.That(into, Is.Empty);
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void NodeWithoutId_IsSkipped()
        {
            var orphan = new TriggerNode { TriggerName = "Start" };

            Assert.That(Collect(orphan), Is.Empty);
        }

        [Test]
        public void DuplicateName_FirstWins_AndWarns()
        {
            var log = new FakeLog();
            var into = new List<TriggerDeclaration>();

            TriggerIntrospector.Collect(
                new MotionNodeBase[] { Trigger(1, "Start"), Trigger(2, "Start") }, into, log);

            Assert.That(into.Count, Is.EqualTo(1));
            Assert.That(into[0].Entry, Is.EqualTo(new NodeId(1)));
            Assert.That(log.Warnings.Count, Is.EqualTo(1));
        }

        [Test]
        public void AuthoringOrder_IsPreserved()
        {
            // 순서가 결정적이어야 인스펙터와 패널의 목록이 리로드마다 흔들리지 않는다.
            List<TriggerDeclaration> triggers = Collect(
                Trigger(5, "End"), Trigger(2, "Start"), Trigger(9, "Loop"));

            Assert.That(triggers[0].Name, Is.EqualTo("End"));
            Assert.That(triggers[1].Name, Is.EqualTo("Start"));
            Assert.That(triggers[2].Name, Is.EqualTo("Loop"));
        }

        [Test]
        public void Collect_ClearsTargetList()
        {
            var into = new List<TriggerDeclaration> { new TriggerDeclaration("stale", NodeId.None) };
            TriggerIntrospector.Collect(new MotionNodeBase[] { Trigger(1, "Start") }, into, null);

            Assert.That(into.Count, Is.EqualTo(1));
            Assert.That(into[0].Name, Is.EqualTo("Start"));
        }

        [Test]
        public void TriggerNameIsTrimmed()
        {
            // 사람이 이름을 칠 때 앞뒤 공백이 들어가면 Fire("Start")가 조용히 빗나간다.
            List<TriggerDeclaration> triggers = Collect(Trigger(1, " Start "));

            Assert.That(triggers[0].Name, Is.EqualTo("Start"));
        }
    }
}
