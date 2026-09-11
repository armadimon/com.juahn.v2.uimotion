using System;
using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>서브그래프를 포함해 슬롯을 수집한다. 에셋 순환과 타입 충돌을 함께 진단한다.</summary>
    public static class GraphSlotCollector
    {
        public static IReadOnlyList<SlotDeclaration> Collect(IMotionGraphView graph, List<MotionGraphIssue> issues = null)
        {
            var slots = new Dictionary<string, SlotDeclaration>(StringComparer.Ordinal);
            Visit(graph, slots, new HashSet<IMotionGraphView>(), new HashSet<IMotionGraphView>(), issues);
            return new List<SlotDeclaration>(slots.Values);
        }
        private static void Visit(IMotionGraphView graph, Dictionary<string, SlotDeclaration> slots,
            HashSet<IMotionGraphView> path, HashSet<IMotionGraphView> visited, List<MotionGraphIssue> issues)
        {
            if (graph == null) return;
            if (path.Contains(graph))
            {
                issues?.Add(new MotionGraphIssue(MotionIssueLevel.Error, NodeId.None, "sub-graph references form a cycle")); return;
            }
            if (!visited.Add(graph)) return;
            path.Add(graph);
            foreach (var id in graph.NodeIds)
            {
                var node = graph.GetNode(id);
                foreach (var slot in SlotIntrospector.Collect(new[] { node }))
                {
                    if (!slots.TryGetValue(slot.Name, out var previous)) { slots.Add(slot.Name, slot); continue; }
                    var a = previous.RequiredType; var b = slot.RequiredType;
                    if (a == null || (b != null && a.IsAssignableFrom(b))) slots[slot.Name] = slot;
                    else if (b != null && !b.IsAssignableFrom(a))
                        issues?.Add(new MotionGraphIssue(MotionIssueLevel.Error, id,
                            "slot '" + slot.Name + "' has incompatible types " + a.Name + " and " + b.Name));
                }
                if (node is SubGraphNode sub)
                {
                    var inner = sub.PeekGraph();
                    if (inner == null || !inner.GetEntry(sub.EntryTrigger).IsValid)
                        issues?.Add(new MotionGraphIssue(MotionIssueLevel.Error, id, "sub-graph target or entry trigger is missing"));
                    Visit(inner, slots, path, visited, issues);
                }
            }
            path.Remove(graph);
        }
    }
}
