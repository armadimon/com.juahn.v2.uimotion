using System;
using System.Collections.Generic;
using System.Reflection;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class SlotIntrospectorTests
    {
        // 코어는 요구 타입을 검사하지 않고 나르기만 하므로 아무 타입이나 써도 된다.
        private sealed class OneSlotNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Target = new SlotRef("Icon");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class SelfOnlyNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Target = SlotRef.Self;

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class UntypedSlotNode : MotionEffectNode
        {
            public SlotRef Target = new SlotRef("Bare");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class EmptySlotNode : MotionEffectNode
        {
            public SlotRef Target;

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class TwoSlotNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef First = new SlotRef("A");
            [MotionSlot(typeof(int))] public SlotRef Second = new SlotRef("B");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private abstract class BaseWithSlot : MotionEffectNode
        {
            [MotionSlot(typeof(string))] public SlotRef Inherited = new SlotRef("FromBase");
        }

        private sealed class DerivedWithSlot : BaseWithSlot
        {
            [MotionSlot(typeof(int))] public SlotRef Own = new SlotRef("FromDerived");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private sealed class ConflictingTypeNode : MotionEffectNode
        {
            [MotionSlot(typeof(int))] public SlotRef Target = new SlotRef("Icon");

            protected override IMotionHandle OnPlay(IMotionContext ctx) => MotionHandle.Completed;
        }

        private static List<SlotDeclaration> Collect(params MotionNodeBase[] nodes)
        {
            var into = new List<SlotDeclaration>();
            SlotIntrospector.Collect(nodes, into);
            return into;
        }

        [Test]
        public void NullNodes_YieldsEmpty()
        {
            var into = new List<SlotDeclaration>();
            SlotIntrospector.Collect(null, into);
            Assert.That(into, Is.Empty);
        }

        [Test]
        public void SingleSlot_IsCollectedWithType()
        {
            List<SlotDeclaration> slots = Collect(new OneSlotNode());

            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].Name, Is.EqualTo("Icon"));
            Assert.That(slots[0].RequiredType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void SelfSlot_IsNotDeclared()
        {
            // Self는 예약 슬롯이라 언제나 플레이어 자신이다. 인스펙터에 뜨면 안 된다.
            Assert.That(Collect(new SelfOnlyNode()), Is.Empty);
        }

        [Test]
        public void EmptyName_IsSkipped()
        {
            Assert.That(Collect(new EmptySlotNode()), Is.Empty);
        }

        [Test]
        public void MissingAttribute_YieldsNullType()
        {
            List<SlotDeclaration> slots = Collect(new UntypedSlotNode());

            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].Name, Is.EqualTo("Bare"));
            Assert.That(slots[0].RequiredType, Is.Null);
        }

        [Test]
        public void NullNodeInArray_IsSkipped()
        {
            // 결손 노드. 타입이 사라진 그래프를 열면 SerializeReference가 null을 남긴다.
            List<SlotDeclaration> slots = Collect(null, new OneSlotNode(), null);

            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].Name, Is.EqualTo("Icon"));
        }

        [Test]
        public void DuplicateName_IsCollapsed_FirstTypeWins()
        {
            List<SlotDeclaration> slots = Collect(new OneSlotNode(), new ConflictingTypeNode());

            Assert.That(slots.Count, Is.EqualTo(1));
            Assert.That(slots[0].RequiredType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void FieldOrder_IsPreserved()
        {
            List<SlotDeclaration> slots = Collect(new TwoSlotNode());

            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].Name, Is.EqualTo("A"));
            Assert.That(slots[1].Name, Is.EqualTo("B"));
        }

        [Test]
        public void InheritedSlot_IsCollected_BaseFirst()
        {
            // 순서가 결정적이어야 인스펙터의 슬롯 목록이 리로드마다 흔들리지 않는다.
            List<SlotDeclaration> slots = Collect(new DerivedWithSlot());

            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[0].Name, Is.EqualTo("FromBase"));
            Assert.That(slots[1].Name, Is.EqualTo("FromDerived"));
        }

        [Test]
        public void NodeOrder_IsPreserved()
        {
            List<SlotDeclaration> slots = Collect(new ConflictingTypeNode(), new TwoSlotNode());

            Assert.That(slots[0].Name, Is.EqualTo("Icon"));
            Assert.That(slots[1].Name, Is.EqualTo("A"));
            Assert.That(slots[2].Name, Is.EqualTo("B"));
        }

        [Test]
        public void Collect_ClearsTargetList()
        {
            var into = new List<SlotDeclaration> { new SlotDeclaration("stale") };
            SlotIntrospector.Collect(new MotionNodeBase[] { new OneSlotNode() }, into);

            Assert.That(into.Count, Is.EqualTo(1));
            Assert.That(into[0].Name, Is.EqualTo("Icon"));
        }

        [Test]
        public void RepeatedCalls_AreStable()
        {
            // 타입별 리플렉션 결과를 캐시하므로 두 번째 호출이 첫 번째와 달라지면 안 된다.
            List<SlotDeclaration> first = Collect(new DerivedWithSlot());
            List<SlotDeclaration> second = Collect(new DerivedWithSlot());

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Name, Is.EqualTo(first[i].Name));
                Assert.That(second[i].RequiredType, Is.EqualTo(first[i].RequiredType));
            }
        }

        [Test]
        public void GetSlotFields_ExposesFieldsForEditorRebinding()
        {
            // 에디터가 슬롯 이름을 바꿔 쓰려면 FieldInfo가 필요하다.
            FieldInfo[] fields = SlotIntrospector.GetSlotFields(typeof(TwoSlotNode));

            Assert.That(fields.Length, Is.EqualTo(2));
            Assert.That(fields[0].Name, Is.EqualTo("First"));
            Assert.That(fields[1].Name, Is.EqualTo("Second"));
        }

        [Test]
        public void GetSlotFields_NullType_ReturnsEmpty()
        {
            Assert.That(SlotIntrospector.GetSlotFields(null), Is.Empty);
        }
    }
}
