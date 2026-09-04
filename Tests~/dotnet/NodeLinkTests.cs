using System;
using System.Reflection;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class NodeLinkTests
    {
        [Test]
        public void Default_IsInvalid()
        {
            var link = default(NodeLink);
            Assert.That(link.IsValid, Is.False);
        }

        [Test]
        public void BothEndsValid_IsValid()
        {
            var link = new NodeLink(new NodeId(1), new NodeId(2));
            Assert.That(link.IsValid, Is.True);
        }

        [Test]
        public void MissingTo_IsInvalid()
        {
            var link = new NodeLink(new NodeId(1), NodeId.None);
            Assert.That(link.IsValid, Is.False);
        }

        [Test]
        public void SameEnds_AreEqual()
        {
            var a = new NodeLink(new NodeId(3), new NodeId(4));
            var b = new NodeLink(new NodeId(3), new NodeId(4));
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void ReversedEnds_AreNotEqual()
        {
            var a = new NodeLink(new NodeId(3), new NodeId(4));
            var b = new NodeLink(new NodeId(4), new NodeId(3));
            Assert.That(a, Is.Not.EqualTo(b));
        }

        // Unity 직렬화 제약을 못 박는다. 계획 1의 NodeId/SlotRef와 같은 이유다 —
        // readonly 필드나 private 필드는 그래프 에셋에 저장되지 않는다.
        [Test]
        public void Fields_ArePublicAndAssignable()
        {
            FieldInfo from = typeof(NodeLink).GetField("From");
            FieldInfo to = typeof(NodeLink).GetField("To");

            Assert.That(from, Is.Not.Null, "From은 public 필드여야 한다");
            Assert.That(to, Is.Not.Null, "To는 public 필드여야 한다");
            Assert.That(from.IsInitOnly, Is.False, "readonly면 Unity가 직렬화하지 않는다");
            Assert.That(to.IsInitOnly, Is.False, "readonly면 Unity가 직렬화하지 않는다");
        }

        [Test]
        public void Type_IsSerializable()
        {
            Assert.That(typeof(NodeLink).IsDefined(typeof(SerializableAttribute), false), Is.True);
        }
    }
}
