using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    /// <summary>
    /// Host는 코어에 뚫은 구멍 하나다. 코어는 이것이 무엇인지 모르고 나르기만 한다 —
    /// Unity 계층이 MotionPlayer를 넣고, 효과 노드가 캐스트해서 트윈 러너를 꺼낸다.
    /// </summary>
    [TestFixture]
    public sealed class MotionContextHostTests
    {
        private sealed class HostProbeNode : MotionEffectNode
        {
            public object SeenHost;
            public int SeenDepth = -1;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                SeenHost = ctx.Host;
                SeenDepth = ctx.Depth;
                return MotionHandle.Completed;
            }
        }

        [Test]
        public void Context_WithoutHost_HasNullHost()
        {
            var graph = new FakeGraph { GraphName = "g" };
            var scope = new MotionScope("t", null);
            var ctx = new MotionContext(graph, scope, null, null);

            Assert.That(ctx.Host, Is.Null);
        }

        [Test]
        public void Context_CarriesHost()
        {
            var host = new object();
            var graph = new FakeGraph { GraphName = "g" };
            var scope = new MotionScope("t", null);
            var ctx = new MotionContext(graph, scope, null, null, null, 0, host);

            Assert.That(ctx.Host, Is.SameAs(host));
        }

        [Test]
        public void Runtime_PassesHostToNodes()
        {
            var host = new object();
            var probe = new HostProbeNode();

            var graph = new FakeGraph { GraphName = "g" };
            NodeId id = graph.Add(probe);
            graph.DeclareTrigger("Start", id);

            var runtime = new MotionRuntime(graph, null, null, host);
            runtime.Fire("Start");
            runtime.Tick(0f);

            Assert.That(probe.SeenHost, Is.SameAs(host));
        }

        [Test]
        public void SubGraph_PropagatesHostAndDepth()
        {
            var host = new object();

            var inner = new FakeGraph { GraphName = "inner" };
            var probe = new HostProbeNode();
            NodeId innerEntry = inner.Add(probe);
            inner.DeclareTrigger("Start", innerEntry);

            var outer = new FakeGraph { GraphName = "outer" };
            var sub = new FakeSubGraphNode { Target = inner };
            NodeId outerEntry = outer.Add(sub);
            outer.DeclareTrigger("Start", outerEntry);

            var runtime = new MotionRuntime(outer, null, null, host);
            runtime.Fire("Start");
            runtime.Tick(0f);

            Assert.That(probe.SeenHost, Is.SameAs(host), "서브그래프가 Host를 잃으면 안쪽 노드가 트윈 러너를 못 찾는다");
            Assert.That(probe.SeenDepth, Is.EqualTo(1));
        }
    }
}
