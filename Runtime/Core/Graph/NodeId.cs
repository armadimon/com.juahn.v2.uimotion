using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 그래프 안에서 노드 하나를 가리키는 식별자.
    ///
    /// 0은 "없음"으로 예약한다. 그래서 <c>default(NodeId)</c>와 직렬화되지 않은 필드가
    /// 자동으로 <see cref="None"/>이 된다.
    /// </summary>
    [Serializable]
    public readonly struct NodeId : IEquatable<NodeId>
    {
        /// <summary>어떤 노드도 가리키지 않는 값.</summary>
        public static readonly NodeId None = default;

        private readonly int _value;

        public NodeId(int value)
        {
            _value = value;
        }

        /// <summary>원시 정수값. 직렬화와 진단에만 쓴다.</summary>
        public int Value => _value;

        /// <summary>실제 노드를 가리키는가.</summary>
        public bool IsValid => _value > 0;

        public bool Equals(NodeId other) => _value == other._value;

        public override bool Equals(object obj) => obj is NodeId other && Equals(other);

        public override int GetHashCode() => _value;

        public override string ToString() => IsValid ? "#" + _value : "#none";

        public static bool operator ==(NodeId a, NodeId b) => a._value == b._value;

        public static bool operator !=(NodeId a, NodeId b) => a._value != b._value;
    }
}
