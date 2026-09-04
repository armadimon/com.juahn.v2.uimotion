using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class SlotRefTests
    {
        [Test]
        public void Self_UsesReservedName()
        {
            Assert.That(SlotRef.Self.Name, Is.EqualTo("Self"));
            Assert.That(SlotRef.Self.IsSelf, Is.True);
        }

        [Test]
        public void Default_IsNotValid()
        {
            Assert.That(default(SlotRef).IsValid, Is.False);
        }

        [Test]
        public void EmptyName_IsNotValid()
        {
            Assert.That(new SlotRef("").IsValid, Is.False);
            Assert.That(new SlotRef("   ").IsValid, Is.False);
        }

        [Test]
        public void NamedSlot_IsValidAndNotSelf()
        {
            var slot = new SlotRef("Icon");
            Assert.That(slot.IsValid, Is.True);
            Assert.That(slot.IsSelf, Is.False);
        }

        [Test]
        public void SelfName_IsCaseSensitive()
        {
            // 슬롯 이름은 계층의 오브젝트 이름과 대조되므로 대소문자를 구분한다.
            // "self"라는 자식을 만든 사람이 Self 슬롯을 덮어쓰면 안 된다.
            Assert.That(new SlotRef("self").IsSelf, Is.False);
        }

        [Test]
        public void SameName_AreEqual()
        {
            Assert.That(new SlotRef("Panel"), Is.EqualTo(new SlotRef("Panel")));
            Assert.That(new SlotRef("Panel").GetHashCode(),
                Is.EqualTo(new SlotRef("Panel").GetHashCode()));
        }

        [Test]
        public void DifferentName_AreNotEqual()
        {
            Assert.That(new SlotRef("Panel"), Is.Not.EqualTo(new SlotRef("Icon")));
        }

        [Test]
        public void NameField_IsPublicAndAssignable()
        {
            // Unity 직렬화는 public 필드만 자동으로 잡는다. private이나 readonly면
            // 그래프 에셋에 저장된 슬롯 이름이 전부 null로 리셋된다.
            // 이 테스트는 그 제약이 코드에 남아 있는지를 지킨다.
            var slot = new SlotRef("A");
            slot.Name = "B";
            Assert.That(slot.Name, Is.EqualTo("B"));
        }

        [Test]
        public void NullName_DoesNotThrow()
        {
            var slot = new SlotRef(null);
            Assert.That(slot.IsValid, Is.False);
            Assert.That(slot.IsSelf, Is.False);
            Assert.DoesNotThrow(() => slot.GetHashCode());
            Assert.DoesNotThrow(() => slot.ToString());
        }
    }
}
