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
            CheckTriggers(graph, ids, into);
            CheckBlockedChildren(graph, ids, into);
            CheckLinkCycles(graph, ids, into);
            CheckReachability(graph, ids, into);
            CheckSubGraphCycles(graph, into);
            GraphSlotCollector.Collect(graph, into);
            if (!IsFinite(graph, MotionRuntime.EndTrigger))
                into.Add(new MotionGraphIssue(MotionIssueLevel.Error, graph.GetEntry(MotionRuntime.EndTrigger),
                    "trigger 'End' must finish; it contains a loop or cyclic path"));
        }

        public static bool IsFinite(IMotionGraphView graph, string trigger)
        {
            if (graph == null) return true;
            return IsFinite(graph, graph.GetEntry(trigger), new HashSet<(IMotionGraphView, NodeId)>());
        }
        private static bool IsFinite(IMotionGraphView graph, NodeId id, HashSet<(IMotionGraphView, NodeId)> path)
        {
            if (!id.IsValid) return true;
            var key = (graph, id);
            if (!path.Add(key)) return false;
            var node = graph.GetNode(id);
            var finite = node == null || (!node.BlocksChildren && !(node is RepeatNode repeat && repeat.Count < 0));
            if (finite && node is SubGraphNode sub && sub.PeekGraph() != null)
                finite = IsFinite(sub.PeekGraph(), sub.PeekGraph().GetEntry(sub.EntryTrigger), path);
            if (finite) foreach (var child in graph.GetChildren(id))
                if (!IsFinite(graph, child, path)) { finite = false; break; }
            path.Remove(key);
            return finite;
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

        /// <summary>
        /// 트리거 규칙. 진입점이 <see cref="TriggerNode"/> 자신이 되면서 예전 규칙 둘
        /// ("없는 노드를 가리킨다" · "진입점이 비었다")은 <b>성립할 수 없게 되었다</b>.
        /// 대신 노드로 옮겨서 생긴 새 실수 셋을 잡는다.
        /// </summary>
        private static void CheckTriggers(
            IMotionGraphView graph, IReadOnlyList<NodeId> ids, List<MotionGraphIssue> into)
        {
            bool sawTriggerNode = false;

            for (int i = 0; i < ids.Count; i++)
            {
                var trigger = graph.GetNode(ids[i]) as TriggerNode;
                if (trigger == null)
                {
                    continue;
                }

                sawTriggerNode = true;

                if (string.IsNullOrWhiteSpace(trigger.TriggerName))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, ids[i],
                        "this trigger node has no name, so nothing can fire it"));
                }
            }

            // 트리거 노드로 들어오는 간선. 만든 사람은 "이 연출 뒤에 저 트리거가 이어진다"고
            // 읽지만 실제로는 이어지지 않는다. 오류도 나지 않아 찾기 어렵다.
            for (int i = 0; i < ids.Count; i++)
            {
                IReadOnlyList<NodeId> children = graph.GetChildren(ids[i]);
                for (int c = 0; c < children.Count; c++)
                {
                    if (graph.GetNode(children[c]) is TriggerNode)
                    {
                        into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, children[c],
                            "a link points into this trigger node, but a trigger is an entry point " +
                            "and is only reached by firing it by name"));
                    }
                }
            }

            if (!sawTriggerNode && ids.Count > 0)
            {
                into.Add(new MotionGraphIssue(MotionIssueLevel.Warning, NodeId.None,
                    "this graph has no trigger node, so nothing can play it. add one from the palette."));
            }

            CheckLoopRepeats(graph, into);
        }

        /// <summary>
        /// <c>Loop</c>가 한 번 돌고 멈추는지. 규칙 자체는 예전과 같고 진입점을 찾는
        /// 방법만 바뀌었다.
        ///
        /// Warning이 아니라 Info인 이유 — 게임 코드가 매번 <c>Fire("Loop")</c>를 직접
        /// 부르는 그래프도 있고, 그때는 유한 Loop가 정상이다. 거짓 경고가 쌓이면
        /// 사람이 경고 전체를 무시하게 되므로 확신할 수 없는 규칙은 올리지 않는다.
        /// </summary>
        private static void CheckLoopRepeats(IMotionGraphView graph, List<MotionGraphIssue> into)
        {
            IReadOnlyList<TriggerDeclaration> triggers = graph.Triggers;
            if (triggers == null)
            {
                return;
            }

            for (int i = 0; i < triggers.Count; i++)
            {
                TriggerDeclaration decl = triggers[i];
                if (decl == null || decl.Name != MotionRuntime.LoopTrigger)
                {
                    continue;
                }

                if (!LoopRepeats(graph, decl.Entry))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Info, decl.Entry,
                        "trigger 'Loop' runs once and stops. if the game does not re-fire it every " +
                        "time, wrap it in Repeat or use a node that keeps going such as Float or Bounce"));
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
                NodeId culprit;
                if (FindCycle(graph, ids[i], state, out culprit))
                {
                    into.Add(new MotionGraphIssue(MotionIssueLevel.Error, culprit,
                        "the links form a cycle through " + culprit + "; execution would expand without end"));
                    return;
                }
            }
        }

        /// <summary>
        /// 순환을 찾고 <b>순환에 실제로 속한</b> 노드를 <paramref name="culprit"/>에 담는다.
        ///
        /// 탐색을 시작한 노드를 범인으로 지목하면 안 된다. <c>5 -&gt; 1 -&gt; 2 -&gt; 1</c>에서
        /// 5부터 훑기 시작했다는 이유로 5에 오류 배지가 붙으면, 사람은 5를 아무리 들여다봐도
        /// 문제를 찾지 못한다. 도구가 틀린 곳을 가리키는 것은 도구가 없는 것보다 나쁘다.
        /// </summary>
        private static bool FindCycle(
            IMotionGraphView graph, NodeId id, Dictionary<int, int> state, out NodeId culprit)
        {
            culprit = NodeId.None;

            int mark;
            if (state.TryGetValue(id.Value, out mark))
            {
                if (mark == 1)
                {
                    // 경로 위에서 다시 만났다. 이 노드가 순환의 일부다.
                    culprit = id;
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
                if (FindCycle(graph, children[i], state, out culprit))
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
