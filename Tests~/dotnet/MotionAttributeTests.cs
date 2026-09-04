using System;
using System.Reflection;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionAttributeTests
    {
        [MotionNode(
            Name = "Test Node",
            Category = "Testing",
            Summary = "테스트용 노드다.",
            Sample = "TestNode")]
        private class DocumentedNode : MotionEffectNode
        {
            [MotionSlot(typeof(string))]
            public SlotRef Target = SlotRef.Self;

            [MotionParam(Label = "세기", Tooltip = "클수록 크게 움직인다.")]
            public float Amplitude = 1f;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }

        private sealed class UndocumentedNode : MotionEffectNode
        {
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }

        // DocumentedNode를 상속한다. 이래야 "파생이 부모의 [MotionNode]를 물려받는가"를
        // 실제로 검증할 수 있다. MotionEffectNode를 직접 상속하면 이 테스트는
        // Inherited 설정과 무관하게 항상 통과해 아무것도 지키지 못한다.
        private sealed class DerivedNode : DocumentedNode
        {
        }

        [Test]
        public void MotionNodeAttribute_IsReadableByReflection()
        {
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DocumentedNode), typeof(MotionNodeAttribute));

            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.Name, Is.EqualTo("Test Node"));
            Assert.That(attr.Category, Is.EqualTo("Testing"));
            Assert.That(attr.Summary, Is.EqualTo("테스트용 노드다."));
            Assert.That(attr.Sample, Is.EqualTo("TestNode"));
        }

        [Test]
        public void DocumentedNode_IsVerified()
        {
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DocumentedNode), typeof(MotionNodeAttribute));

            Assert.That(attr.IsVerified, Is.True);
        }

        [Test]
        public void NodeWithoutSample_IsNotVerified()
        {
            var attr = new MotionNodeAttribute { Name = "X", Category = "Y", Summary = "Z" };
            Assert.That(attr.IsVerified, Is.False, "작동 예시가 없으면 미검증이다");
        }

        [Test]
        public void NodeWithoutSummary_IsNotVerified()
        {
            var attr = new MotionNodeAttribute { Name = "X", Category = "Y", Sample = "S" };
            Assert.That(attr.IsVerified, Is.False, "설명이 없으면 미검증이다");
        }

        [Test]
        public void BlankSummary_IsNotVerified()
        {
            // 공백만 넣어 검사를 통과시키는 것을 막는다.
            var attr = new MotionNodeAttribute { Name = "X", Summary = "   ", Sample = "S" };
            Assert.That(attr.IsVerified, Is.False);
        }

        [Test]
        public void UndocumentedNode_HasNoAttribute()
        {
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(UndocumentedNode), typeof(MotionNodeAttribute));

            Assert.That(attr, Is.Null);
        }

        [Test]
        public void MotionSlotAttribute_CarriesRequiredType()
        {
            FieldInfo field = typeof(DocumentedNode).GetField("Target");
            var attr = (MotionSlotAttribute)Attribute.GetCustomAttribute(field, typeof(MotionSlotAttribute));

            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.RequiredType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void MotionParamAttribute_CarriesLabelAndTooltip()
        {
            FieldInfo field = typeof(DocumentedNode).GetField("Amplitude");
            var attr = (MotionParamAttribute)Attribute.GetCustomAttribute(field, typeof(MotionParamAttribute));

            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.Label, Is.EqualTo("세기"));
            Assert.That(attr.Tooltip, Is.EqualTo("클수록 크게 움직인다."));
        }

        [Test]
        public void MotionParamAttribute_HasRange_OnlyWhenMaxExceedsMin()
        {
            Assert.That(new MotionParamAttribute().HasRange, Is.False);
            Assert.That(new MotionParamAttribute { Min = 0f, Max = 1f }.HasRange, Is.True);
            Assert.That(new MotionParamAttribute { Min = 1f, Max = 1f }.HasRange, Is.False);
        }

        [Test]
        public void MotionNodeAttribute_IsNotInherited()
        {
            // 파생 노드가 부모의 설명을 물려받으면 Node Doctor가 거짓 통과를 낸다.
            // inherit 인자를 true로 줘도 null이어야 한다 - 그게 Inherited=false의 의미다.
            var withoutInherit = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DerivedNode), typeof(MotionNodeAttribute), false);
            var withInherit = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DerivedNode), typeof(MotionNodeAttribute), true);

            Assert.That(withoutInherit, Is.Null);
            Assert.That(withInherit, Is.Null,
                "Inherited=false이므로 inherit=true로 조회해도 부모의 어트리뷰트가 딸려오면 안 된다");
        }

        [Test]
        public void ParentNode_StillHasItsOwnAttribute()
        {
            // 위 테스트가 "어트리뷰트가 아예 없어서" 통과하는 게 아님을 보증한다.
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DocumentedNode), typeof(MotionNodeAttribute), true);

            Assert.That(attr, Is.Not.Null);
        }
    }
}
