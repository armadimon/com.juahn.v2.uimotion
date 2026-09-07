using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class GraphEnumerationTests
    {
        private sealed class PlainNode : MotionEffectNode
        {
            public PlainNode(int id)
            {
                Id = new NodeId(id);
            }

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        /// <summary>대상 그래프를 직접 물려 주는 서브그래프 노드.</summary>
        private sealed class DirectSubGraphNode : SubGraphNode
        {
            private IMotionGraphView _target;

            public DirectSubGraphNode(int id, IMotionGraphView target)
            {
                Id = new NodeId(id);
                _target = target;
            }

            public void Retarget(IMotionGraphView target)
            {
                _target = target;
            }

            protected override IMotionGraphView ResolveGraph() => _target;
        }

        private static MotionGraphIndex Index(params MotionNodeBase[] nodes)
        {
            return new MotionGraphIndex("g", nodes, null, null, null);
        }

        [Test]
        public void NodeIds_ListsEveryNode()
        {
            MotionGraphIndex index = Index(new PlainNode(1), new PlainNode(7), new PlainNode(3));

            IReadOnlyList<NodeId> ids = index.NodeIds;

            Assert.That(ids.Count, Is.EqualTo(3));
            Assert.That(ids, Does.Contain(new NodeId(1)));
            Assert.That(ids, Does.Contain(new NodeId(3)));
            Assert.That(ids, Does.Contain(new NodeId(7)));
        }

        [Test]
        public void NodeIds_KeepsAuthoringOrder()
        {
            // 순서가 결정적이어야 검사 결과 목록이 리로드마다 뒤바뀌지 않는다.
            MotionGraphIndex index = Index(new PlainNode(5), new PlainNode(2), new PlainNode(9));

            Assert.That(index.NodeIds[0], Is.EqualTo(new NodeId(5)));
            Assert.That(index.NodeIds[1], Is.EqualTo(new NodeId(2)));
            Assert.That(index.NodeIds[2], Is.EqualTo(new NodeId(9)));
        }

        [Test]
        public void NodeIds_SkipsMissingAndDuplicateNodes()
        {
            MotionGraphIndex index = Index(new PlainNode(1), null, new PlainNode(1));

            Assert.That(index.NodeIds.Count, Is.EqualTo(1));
        }

        [Test]
        public void NodeIds_EmptyGraph_IsEmptyNotNull()
        {
            MotionGraphIndex index = Index();

            Assert.That(index.NodeIds, Is.Not.Null);
            Assert.That(index.NodeIds, Is.Empty);
        }

        // --- 순환 검출 회귀 -------------------------------------------------

        [Test]
        public void CycleDetector_FindsCycleBehindAnIdGap()
        {
            // 저작 API는 id를 재사용하지 않으므로 노드를 지우면 id에 구멍이 생긴다.
            // 옛 검출기는 구멍에서 멈춰 그 뒤의 서브그래프를 보지 않았다.
            var placeholder = new PlainNode(1);
            var sub = new DirectSubGraphNode(3, null);

            MotionGraphIndex root = Index(placeholder, sub);
            sub.Retarget(root);

            Assert.That(GraphCycleDetector.HasCycle(root), Is.True,
                "id 2가 비어 있다고 해서 노드 3을 건너뛰면 안 된다");
        }

        [Test]
        public void CycleDetector_NoCycle_IsFalse()
        {
            var leaf = Index(new PlainNode(1));
            var sub = new DirectSubGraphNode(3, leaf);
            MotionGraphIndex root = Index(new PlainNode(1), sub);

            Assert.That(GraphCycleDetector.HasCycle(root), Is.False);
        }

        [Test]
        public void CycleDetector_DiamondIsNotACycle()
        {
            // 같은 서브그래프를 두 곳에서 쓰는 것은 재사용이지 순환이 아니다.
            var shared = Index(new PlainNode(1));
            MotionGraphIndex root = Index(
                new DirectSubGraphNode(1, shared),
                new DirectSubGraphNode(2, shared));

            Assert.That(GraphCycleDetector.HasCycle(root), Is.False);
        }

        // --- 자식을 막는 노드 -----------------------------------------------

        [Test]
        public void BlocksChildren_DefaultsToFalse()
        {
            Assert.That(new PlainNode(1).BlocksChildren, Is.False);
        }
    }
}
