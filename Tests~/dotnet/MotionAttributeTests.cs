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
        private sealed class DocumentedNode : MotionEffectNode
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

        private sealed class DerivedNode : MotionEffectNode
        {
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
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
            var attr = (MotionNodeAttribute)Attribute.GetCustomAttribute(
                typeof(DerivedNode), typeof(MotionNodeAttribute), false);

            Assert.That(attr, Is.Null);
        }
    }
}
