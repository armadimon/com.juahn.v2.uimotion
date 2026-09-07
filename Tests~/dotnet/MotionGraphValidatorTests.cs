using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionGraphValidatorTests
    {
        private sealed class PlainNode : MotionEffectNode
        {
            public PlainNode(int id) { Id = new NodeId(id); }
            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class EndlessNode : MotionEffectNode
        {
            public EndlessNode(int id) { Id = new NodeId(id); }
            public override bool BlocksChildren => true;
            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Forever(null);
        }

        private sealed class DirectSubGraphNode : SubGraphNode
        {
            private IMotionGraphView _target;
            public DirectSubGraphNode(int id, IMotionGraphView target) { Id = new NodeId(id); _target = target; }
            public void Retarget(IMotionGraphView t) { _target = t; }
            protected override IMotionGraphView ResolveGraph() => _target;
        }

        private static MotionGraphIndex Build(
            MotionNodeBase[] nodes = null, NodeLink[] links = null, TriggerDeclaration[] triggers = null)
        {
            return new MotionGraphIndex("g", nodes, links, triggers, null);
        }

        private static bool Has(IReadOnlyList<MotionGraphIssue> issues, MotionIssueLevel level, string fragment)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Level == level && issues[i].Message.Contains(fragment))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void NullGraph_YieldsNothing()
        {
            Assert.That(MotionGraphValidator.Validate(null), Is.Empty);
        }

        [Test]
        public void HealthyGraph_HasNoIssues()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1), new PlainNode(2) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            Assert.That(MotionGraphValidator.Validate(Build(nodes, links, triggers)), Is.Empty);
        }

        [Test]
        public void LinkToMissingNode_IsError()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(99)) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Error, "#99"), Is.True);
        }

        [Test]
        public void TriggerEntryToMissingNode_IsError()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(42)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, null, triggers)),
                MotionIssueLevel.Error, "Start"), Is.True);
        }

        [Test]
        public void LinkCycle_IsError()
        {
            // NodeRun은 자식을 재귀로 펼치므로 간선 순환은 깊이 상한에서 잘린다.
            var nodes = new MotionNodeBase[] { new PlainNode(1), new PlainNode(2) };
            var links = new[]
            {
                new NodeLink(new NodeId(1), new NodeId(2)),
                new NodeLink(new NodeId(2), new NodeId(1)),
            };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Error, "cycle"), Is.True);
        }

        [Test]
        public void SharedChild_IsNotACycle()
        {
            // 두 부모가 같은 자식을 가리키는 다이아몬드는 순환이 아니다.
            var nodes = new MotionNodeBase[] { new PlainNode(1), new PlainNode(2), new PlainNode(3) };
            var links = new[]
            {
                new NodeLink(new NodeId(1), new NodeId(2)),
                new NodeLink(new NodeId(1), new NodeId(3)),
                new NodeLink(new NodeId(2), new NodeId(3)),
            };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Error, "cycle"), Is.False);
        }

        [Test]
        public void SubGraphCycle_IsError()
        {
            var sub = new DirectSubGraphNode(2, null);
            var nodes = new MotionNodeBase[] { new PlainNode(1), sub };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            MotionGraphIndex graph = Build(nodes, links, triggers);
            sub.Retarget(graph);

            Assert.That(Has(MotionGraphValidator.Validate(graph),
                MotionIssueLevel.Error, "sub-graph"), Is.True);
        }

        [Test]
        public void EndlessNodeWithChildren_IsWarning()
        {
            // 이 배선은 오류를 내지 않고 조용히 아무 일도 하지 않는다. 사람이 찾기 가장 어렵다.
            var nodes = new MotionNodeBase[] { new EndlessNode(1), new PlainNode(2) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };
            var triggers = new[] { new TriggerDeclaration("Loop", new NodeId(1)) };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Warning, "never run"), Is.True);
        }

        [Test]
        public void UnreachableNode_IsWarning()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1), new PlainNode(2) };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(1)) };

            IReadOnlyList<MotionGraphIssue> issues = MotionGraphValidator.Validate(Build(nodes, null, triggers));

            Assert.That(Has(issues, MotionIssueLevel.Warning, "#2"), Is.True);
            Assert.That(Has(issues, MotionIssueLevel.Warning, "#1"), Is.False, "진입 노드는 도달 가능하다");
        }

        [Test]
        public void TriggerWithoutEntry_IsWarning()
        {
            var nodes = new MotionNodeBase[] { new PlainNode(1) };
            var triggers = new[]
            {
                new TriggerDeclaration("Start", new NodeId(1)),
                new TriggerDeclaration("Click", NodeId.None),
            };

            Assert.That(Has(MotionGraphValidator.Validate(Build(nodes, null, triggers)),
                MotionIssueLevel.Warning, "Click"), Is.True);
        }

        [Test]
        public void LoopWithEndlessNode_IsFine()
        {
            var nodes = new MotionNodeBase[] { new EndlessNode(1) };
            var triggers = new[] { new TriggerDeclaration("Loop", new NodeId(1)) };

            Assert.That(MotionGraphValidator.Validate(Build(nodes, null, triggers)), Is.Empty);
        }

        [Test]
        public void LoopWithInfiniteRepeat_IsFine()
        {
            var repeat = new RepeatNode { Id = new NodeId(1), Count = RepeatNode.Infinite };
            var nodes = new MotionNodeBase[] { repeat, new PlainNode(2) };
            var links = new[] { new NodeLink(new NodeId(1), new NodeId(2)) };
            var triggers = new[] { new TriggerDeclaration("Loop", new NodeId(1)) };

            Assert.That(MotionGraphValidator.Validate(Build(nodes, links, triggers)), Is.Empty);
        }

        [Test]
        public void HasErrors_DistinguishesLevels()
        {
            var warningOnly = new List<MotionGraphIssue>
            {
                new MotionGraphIssue(MotionIssueLevel.Warning, NodeId.None, "w"),
            };
            var withError = new List<MotionGraphIssue>
            {
                new MotionGraphIssue(MotionIssueLevel.Warning, NodeId.None, "w"),
                new MotionGraphIssue(MotionIssueLevel.Error, NodeId.None, "e"),
            };

            Assert.That(MotionGraphValidator.HasErrors(warningOnly), Is.False);
            Assert.That(MotionGraphValidator.HasErrors(withError), Is.True);
        }

        [Test]
        public void Validate_ClearsTargetList()
        {
            var into = new List<MotionGraphIssue> { new MotionGraphIssue(MotionIssueLevel.Info, NodeId.None, "stale") };
            MotionGraphValidator.Validate(Build(), into);

            Assert.That(into, Is.Empty);
        }

        private static MotionGraphIssue First(
            IReadOnlyList<MotionGraphIssue> issues, MotionIssueLevel level, string fragment)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Level == level && issues[i].Message.Contains(fragment))
                {
                    return issues[i];
                }
            }

            return new MotionGraphIssue(MotionIssueLevel.Info, NodeId.None, "<none>");
        }

        [Test]
        public void LinkCycle_BlamesANodeOnTheCycle()
        {
            // 도구가 틀린 곳을 가리키면 없는 것보다 나쁘다. 순환에 속하지 않은 노드에서
            // 탐색을 시작했다고 해서 그 노드에 오류 배지가 붙으면 안 된다.
            //
            // 5 -> 1 -> 2 -> 1. 순환은 {1, 2}이고 5는 순환 밖이다.
            var nodes = new MotionNodeBase[]
            {
                new PlainNode(5), new PlainNode(1), new PlainNode(2),
            };
            var links = new[]
            {
                new NodeLink(new NodeId(5), new NodeId(1)),
                new NodeLink(new NodeId(1), new NodeId(2)),
                new NodeLink(new NodeId(2), new NodeId(1)),
            };
            var triggers = new[] { new TriggerDeclaration("Start", new NodeId(5)) };

            MotionGraphIssue issue = First(
                MotionGraphValidator.Validate(Build(nodes, links, triggers)),
                MotionIssueLevel.Error, "cycle");

            Assert.That(issue.Node, Is.Not.EqualTo(new NodeId(5)),
                "순환 밖의 노드를 범인으로 지목하면 안 된다");
            Assert.That(issue.Node.Value, Is.AnyOf(1, 2));
        }

        [Test]
        public void FiniteLoop_IsInfoNotWarning()
        {
            // 게임 코드가 매번 Fire("Loop")를 직접 부르는 그래프도 있다. 그 경우
            // 유한 Loop가 정상이므로 경고로 올리면 거짓 양성이 된다. 거짓 경고가 쌓이면
            // 사람이 경고 전체를 무시하게 된다.
            var nodes = new MotionNodeBase[] { new PlainNode(1) };
            var triggers = new[] { new TriggerDeclaration("Loop", new NodeId(1)) };

            IReadOnlyList<MotionGraphIssue> issues = MotionGraphValidator.Validate(Build(nodes, null, triggers));

            Assert.That(Has(issues, MotionIssueLevel.Info, "Loop"), Is.True);
            Assert.That(Has(issues, MotionIssueLevel.Warning, "Loop"), Is.False);
        }
    }
}
