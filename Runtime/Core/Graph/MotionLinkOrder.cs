using System.Collections.Generic;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 자식 순서와 전역 간선 배열 인덱스 사이를 옮긴다.
    ///
    /// <b>왜 변환이 필요한가</b> — 같은 부모에서 나가는 간선의 순서가 곧 <c>Sequence</c>의
    /// 실행 순서인데, 간선은 한 배열에 부모 구분 없이 평면으로 저장된다. 그래서 화면의
    /// "이 부모의 두 번째 자식"과 저장 구조의 인덱스가 일치하지 않는다.
    ///
    /// <b>왜 코어에 있는가</b> — 이 계산이 틀리면 실행 순서가 조용히 바뀐다. 오류도 경고도
    /// 나지 않고 연출만 이상해지므로 사람이 알아채기 어렵다. 순수 로직이라
    /// <c>dotnet test</c>로 덮을 수 있고, 덮을 가치가 있다.
    /// </summary>
    public static class MotionLinkOrder
    {
        /// <summary>
        /// <paramref name="parent"/>에서 나가는 간선들이 배열의 몇 번째인지 순서대로 모은다.
        /// <paramref name="into"/>는 먼저 비운다.
        /// </summary>
        public static void IndicesOf(IReadOnlyList<NodeLink> links, NodeId parent, List<int> into)
        {
            if (into == null)
            {
                return;
            }

            into.Clear();

            if (links == null)
            {
                return;
            }

            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].From == parent)
                {
                    into.Add(i);
                }
            }
        }

        /// <summary>
        /// 부모의 자식 순서에서 <paramref name="childFrom"/>번째를 <paramref name="childTo"/>번째로
        /// 옮기려면 전역 배열의 어느 인덱스를 어디로 보내야 하는지 계산한다.
        ///
        /// 결과는 <c>RemoveAt(globalFrom)</c> 다음 <c>Insert(globalTo, link)</c>에 그대로 쓴다
        /// (<c>MotionGraph.MoveLink</c>의 동작이 정확히 그것이다).
        ///
        /// <b>한 번의 이동으로 충분하다.</b> 목적지 인덱스가 "뽑아낸 뒤"를 기준으로 해석되므로
        /// 위로 옮기든 아래로 옮기든 보정이 필요 없다. 두 번 나눠 부르면 첫 호출이 배열을
        /// 재배치해 두 번째 인덱스가 어긋난다.
        ///
        /// 범위를 벗어나거나 그런 부모가 없으면 false.
        /// </summary>
        public static bool Resolve(
            IReadOnlyList<NodeLink> links, NodeId parent, int childFrom, int childTo,
            out int globalFrom, out int globalTo)
        {
            globalFrom = -1;
            globalTo = -1;

            var indices = new List<int>();
            IndicesOf(links, parent, indices);

            if (indices.Count == 0)
            {
                return false;
            }

            if (childFrom < 0 || childFrom >= indices.Count || childTo < 0 || childTo >= indices.Count)
            {
                return false;
            }

            globalFrom = indices[childFrom];
            globalTo = indices[childTo];
            return true;
        }
    }
}
