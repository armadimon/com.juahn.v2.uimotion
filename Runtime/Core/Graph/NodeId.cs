using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프 안에서 노드 하나를 가리키는 식별자.
    ///
    /// 0은 "없음"으로 예약한다. 그래서 <c>default(NodeId)</c>와 직렬화되지 않은 필드가
    /// 자동으로 <see cref="None"/>이 된다.
    ///
    /// <b>왜 readonly struct가 아니고 필드가 public인가</b> — Unity는 <c>readonly</c> 필드를
    /// 직렬화하지 않고, private 필드는 <c>[SerializeField]</c>가 있어야 직렬화한다.
    /// 그런데 <c>[SerializeField]</c>는 UnityEngine 타입이라 이 어셈블리에서 쓸 수 없다.
    /// 이 타입은 그래프 에셋에 저장되어야 하므로 그 제약이 캡슐화보다 우선한다.
    /// </summary>
    [Serializable]
    public struct NodeId : IEquatable<NodeId>
    {
        /// <summary>어떤 노드도 가리키지 않는 값.</summary>
        public static readonly NodeId None = default;

        /// <summary>원시 정수값. 직렬화 때문에 public 필드다 — 위 설명 참조.</summary>
        public int Value;

        public NodeId(int value)
        {
            Value = value;
        }

        /// <summary>실제 노드를 가리키는가.</summary>
        public bool IsValid => Value > 0;

        public bool Equals(NodeId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is NodeId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => IsValid ? "#" + Value : "#none";

        public static bool operator ==(NodeId a, NodeId b) => a.Value == b.Value;

        public static bool operator !=(NodeId a, NodeId b) => a.Value != b.Value;
    }
}
