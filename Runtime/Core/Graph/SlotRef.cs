using System;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 노드가 "무엇을 움직일지"를 가리키는 이름. 실제 오브젝트는 프리팹의 플레이어가 채운다.
    ///
    /// 이름으로 가리키는 덕분에 그래프 에셋 하나를 여러 프리팹에서 재사용할 수 있다.
    ///
    /// <see cref="NodeId"/>와 같은 이유로 <c>readonly struct</c>가 아니고 필드가 public이다 —
    /// 이 타입은 노드의 필드로 그래프 에셋에 직렬화된다.
    /// </summary>
    [Serializable]
    public struct SlotRef : IEquatable<SlotRef>
    {
        /// <summary>예약 슬롯 이름. 언제나 플레이어 자신을 가리킨다.</summary>
        public const string SelfName = "Self";

        /// <summary>플레이어 자신.</summary>
        public static readonly SlotRef Self = new SlotRef(SelfName);

        /// <summary>슬롯 이름. 직렬화 때문에 public 필드다.</summary>
        public string Name;

        public SlotRef(string name)
        {
            Name = name;
        }

        /// <summary>이름이 채워져 있는가.</summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Name);

        /// <summary>
        /// 예약 슬롯인가. <b>대소문자를 구분한다</b> — 슬롯 이름은 계층의 오브젝트 이름과
        /// 대조되므로, "self"라는 자식이 예약 슬롯을 덮어쓰면 안 된다.
        /// </summary>
        public bool IsSelf => string.Equals(Name, SelfName, StringComparison.Ordinal);

        public bool Equals(SlotRef other) => string.Equals(Name, other.Name, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is SlotRef other && Equals(other);

        public override int GetHashCode() => Name == null ? 0 : Name.GetHashCode();

        public override string ToString() => IsValid ? Name : "<unbound>";
    }
}
