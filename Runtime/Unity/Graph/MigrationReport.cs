using System.Collections.Generic;
using System.Text;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 옛 형식 그래프를 옮긴 결과.
    ///
    /// <b>개수만 돌려주지 않는 이유</b> — 마이그레이션이 만들 수 있는 가장 나쁜 결과는
    /// "옮겼습니다"라고 말해 놓고 트리거가 연출에 이어지지 않은 채 남는 것이다.
    /// 그 트리거를 발사해도 아무 일이 일어나지 않는데 오류는 하나도 없다.
    /// 그래서 못 이은 것과 버린 것을 따로 들고 다닌다.
    /// </summary>
    public sealed class MigrationReport
    {
        /// <summary>트리거 노드로 옮긴 개수.</summary>
        public int Moved;

        /// <summary>트리거 노드는 만들었지만 연출에 잇지 못한 것들. 사람이 직접 이어야 한다.</summary>
        public readonly List<string> Unlinked = new List<string>();

        /// <summary>아예 옮기지 못하고 버린 것들.</summary>
        public readonly List<string> Dropped = new List<string>();

        /// <summary>사람이 손봐야 할 것이 있는가.</summary>
        public bool NeedsAttention => Unlinked.Count > 0 || Dropped.Count > 0;

        public bool DidSomething => Moved > 0 || NeedsAttention;

        /// <summary>사람이 읽을 요약. 문제가 있으면 그것까지 줄바꿈으로 이어 붙인다.</summary>
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append("트리거 ").Append(Moved).Append("개를 노드로 옮겼습니다.");

            for (int i = 0; i < Unlinked.Count; i++)
            {
                builder.Append('\n').Append("  이어지지 않음: ").Append(Unlinked[i]);
            }

            for (int i = 0; i < Dropped.Count; i++)
            {
                builder.Append('\n').Append("  버림: ").Append(Dropped[i]);
            }

            return builder.ToString();
        }
    }
}
