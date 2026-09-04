using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 서브그래프 참조에 순환이 있는지 검사한다. 에디터가 저장 전에 부른다.
    ///
    /// 같은 서브그래프를 여러 곳에서 쓰는 것(다이아몬드)은 순환이 아니다 —
    /// 그게 재사용의 목적이다. 방문 중인 <b>경로</b>에 다시 나타날 때만 순환이다.
    /// </summary>
    public static class GraphCycleDetector
    {
        public static bool HasCycle(IMotionGraphView root)
        {
            if (root == null)
            {
                return false;
            }

            var onPath = new List<IMotionGraphView>();
            return Visit(root, onPath);
        }

        private static bool Visit(IMotionGraphView graph, List<IMotionGraphView> onPath)
        {
            for (int i = 0; i < onPath.Count; i++)
            {
                if (ReferenceEquals(onPath[i], graph))
                {
                    return true;
                }
            }

            onPath.Add(graph);

            List<IMotionGraphView> targets = CollectSubGraphs(graph);
            for (int i = 0; i < targets.Count; i++)
            {
                if (Visit(targets[i], onPath))
                {
                    return true;
                }
            }

            onPath.RemoveAt(onPath.Count - 1);
            return false;
        }

        private static List<IMotionGraphView> CollectSubGraphs(IMotionGraphView graph)
        {
            var targets = new List<IMotionGraphView>();

            // 노드 id는 1부터 순서대로다. null이 나오면 끝이다.
            for (int i = 1; ; i++)
            {
                MotionNodeBase node = graph.GetNode(new NodeId(i));
                if (node == null)
                {
                    break;
                }

                var sub = node as SubGraphNode;
                if (sub == null)
                {
                    continue;
                }

                IMotionGraphView target = sub.PeekGraph();
                if (target != null)
                {
                    targets.Add(target);
                }
            }

            return targets;
        }
    }
}
