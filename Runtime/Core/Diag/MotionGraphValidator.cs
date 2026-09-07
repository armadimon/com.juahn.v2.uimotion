using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프의 배선을 검사한다. <b>순수 로직이라 Unity 없이 테스트된다.</b>
    /// 에디터는 결과를 그리기만 한다.
    ///
    /// 여기서 잡는 것들의 공통점은 <b>런타임에 오류를 내지 않는다</b>는 것이다.
    /// 끝나지 않는 노드에 달린 자식은 조용히 실행되지 않고, 도달 불가 노드는 조용히
    /// 아무 일도 하지 않는다. 사람이 스스로 찾기 가장 어려운 종류라 도구가 잡아야 한다.
    /// </summary>
    public static class MotionGraphValidator
    {
        public static List<MotionGraphIssue> Validate(IMotionGraphView graph)
        {
            var into = new List<MotionGraphIssue>();
            Validate(graph, into);
            return into;
        }

        /// <summary><paramref name="into"/>는 먼저 비운다.</summary>
        public static void Validate(IMotionGraphView graph, List<MotionGraphIssue> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();

            if (graph == null)
            {
                return;
            }

            IReadOnlyList<NodeId> ids = graph.NodeIds;
            if (ids == null)
            {
                return;
            }

            CheckDanglingLinks(graph, ids, into);
            CheckTriggers(graph, into);
            CheckBlockedChildren(graph, ids, into);
            CheckLinkCycles(graph, ids, into);
            CheckReachability(graph, ids, into);
            CheckSubGraphCycles(graph, into);
        }

        public static bool HasErrors(IReadOnlyList<MotionGraphIssue> issues)
        {
            if (issues == null)
            {
                return false;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Level == MotionIssueLevel.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CheckDanglingLinks(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                IReadOnlyList<NodeId> children = graph.GetChildren(ids[i]);
                for (int c = 0; c < children.Count; c++)
                {
                    if (graph.GetNode(children[c]) == null)
                    {
                        into.Add(new MotionGraphIssue(MotionIssueLevel.Error, ids[i],
                            "link points at " + children[c] + " which no longer exists"));
                    }
                }
            }
        }

        private static void CheckTriggers(IMotionGraphView graph, List<MotionGraphIssue> into)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;
            if (triggers == null)
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];
                if (decl == null)
                {
                    continue;
                }

                if (!decl.Entry.IsValid)
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, NodeId.None,
                        "trigger '" + decl.Name + "' has no entry node; firing it does nothing"));
                    continue;
                }

                if (graph.GetNode(decl.Entry) == null)
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Error, NodeId.None,
                        "trigger '" + decl.Name + "' points at " + decl.Entry + " which no longer exists"));
                    continue;
                }

                if (decl.Name == MotionRuntime.LoopTrigger && !LoopRepeats(graph, decl.Entry))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, decl.Entry,
                        "trigger 'Loop' runs once and stops; wrap it in Repeat or use a node that " +
                        "keeps going such as Float or Bounce"));
                }
            }
        }

        private static void CheckBlockedChildren(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            for (int i = 0; i < ids.Count; i++)
            {
                MotionNodeBase node = graph.GetNode(ids[i]);
                if (node == null || !node.BlocksChildren)
                {
                    continue;
                }

                if (graph.GetChildren(ids[i]).Count > 0)
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, ids[i],
                        node.GetType().Name + " never finishes, so its children will never run"));
                }
            }
        }

        private static void CheckLinkCycles(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            // NodeRun은 자식을 재귀로 펼치므로 간선 순환은 무한히 자란다.
            // 깊이 상한이 프로세스를 살리지만 그래프는 의도대로 돌지 않는다.
            var state = new Dictionary<int, int>();   // 0 미방문, 1 경로 위, 2 완료

            for (int i = 0; i < ids.Count; i++)
            {
                if (FindCycle(graph, ids[i], state))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Error, ids[i],
                        "the links form a cycle; execution would expand without end"));
                    return;
                }
            }
        }

        private static bool FindCycle(IMotionGraphView graph, NodeId id, Dictionary<int, int> state)
        {
            int mark;
            if (state.TryGetValue(id.Value, out mark))
            {
                if (mark == 1)
                {
                    return true;
                }

                if (mark == 2)
                {
                    return false;
                }
            }

            state[id.Value] = 1;

            IReadOnlyList<NodeId> children = graph.GetChildren(id);
            for (int i = 0; i < children.Count; i++)
            {
                if (FindCycle(graph, children[i], state))
                {
                    return true;
                }
            }

            state[id.Value] = 2;
            return false;
        }

        private static void CheckReachability(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            var reached = new HashSet<int>();
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;

            if (triggers != null)
            {
                for (int i = 0; i < triggers.Count; i++)
                {
                    if (triggers[i] != null && triggers[i].Entry.IsValid)
                    {
                        Reach(graph, triggers[i].Entry, reached);
                    }
                }
            }

            for (int i = 0; i < ids.Count; i++)
            {
                if (!reached.Contains(ids[i].Value))
                {
                    MotionNodeBase node = graph.GetNode(ids[i]);
                    string what = node == null ? "node" : node.GetType().Name;
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, ids[i],
                        what + " " + ids[i] + " cannot be reached from any trigger; it will never run"));
                }
            }
        }

        private static void Reach(IMotionGraphView graph, NodeId id, HashSet<int> reached)
        {
            if (!reached.Add(id.Value))
            {
                return;
            }

            IReadOnlyList<NodeId> children = graph.GetChildren(id);
            for (int i = 0; i < children.Count; i++)
            {
                Reach(graph, children[i], reached);
            }
        }

        private static void CheckSubGraphCycles(IMotionGraphView graph, List<MotionGraphIssue> into)
        {
            if (GraphCycleDetector.HasCycle(graph))
            {
                into.Add(new MotionGraphIssue(MotionIssueLevel.Error, NodeId.None,
                    "sub-graph references form a cycle"));
            }
        }

        /// <summary>
        /// 이 가지가 스스로 계속 도는가. 끝나지 않는 노드나 무한 <c>Repeat</c>이 하나라도 있으면 그렇다.
        /// </summary>
        private static bool LoopRepeats(IMotionGraphView graph, NodeId entry)
        {
            var seen = new HashSet<int>();
            return LoopRepeats(graph, entry, seen);
        }

        private static bool LoopRepeats(IMotionGraphView graph, NodeId id, HashSet<int> seen)
        {
            if (!seen.Add(id.Value))
            {
                return false;
            }

            MotionNodeBase node = graph.GetNode(id);
            if (node != null)
            {
                if (node.BlocksChildren)
                {
                    return true;
                }

                var repeat = node as RepeatNode;
                if (repeat != null && repeat.Count < 0)
                {
                    return true;
                }
            }

            IReadOnlyList<NodeId> children = graph.GetChildren(id);
            for (int i = 0; i < children.Count; i++)
            {
                if (LoopRepeats(graph, children[i], seen))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
