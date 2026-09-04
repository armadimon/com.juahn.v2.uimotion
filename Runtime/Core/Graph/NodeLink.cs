using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드에서 노드로 가는 간선 하나. 실행 흐름만 나른다 — 값은 흐르지 않는다.
    ///
    /// <b>왜 중첩 배열이 아니라 평면 목록인가</b> — 그래프는 <c>NodeLink[]</c> 하나로 저장된다.
    /// 자식 배열을 노드마다 중첩해 두면 YAML에서 노드 하나를 지울 때 파일 전체가 밀려
    /// 머지 충돌이 난다. 평면 목록은 줄 단위로 움직인다.
    ///
    /// <b>순서가 의미를 갖는다</b> — 같은 부모에서 나가는 간선들의 배열 순서가 곧
    /// <c>Sequence</c>의 실행 순서다. 에디터가 이 순서를 유지할 책임을 진다.
    ///
    /// <see cref="NodeId"/>와 같은 이유로 필드가 public이고 readonly가 아니다.
    /// </summary>
    [Serializable]
    public struct NodeLink : IEquatable<NodeLink>
    {
        public NodeId From;
        public NodeId To;

        public NodeLink(NodeId from, NodeId to)
        {
            From = from;
            To = to;
        }

        /// <summary>양 끝이 모두 실제 노드를 가리키는가.</summary>
        public bool IsValid => From.IsValid && To.IsValid;

        public bool Equals(NodeLink other) => From == other.From && To == other.To;

        public override bool Equals(object obj) => obj is NodeLink other && Equals(other);

        public override int GetHashCode() => (From.Value * 397) ^ To.Value;

        public override string ToString() => From + " -> " + To;

        public static bool operator ==(NodeLink a, NodeLink b) => a.Equals(b);

        public static bool operator !=(NodeLink a, NodeLink b) => !a.Equals(b);
    }
}
