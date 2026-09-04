using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class NodeIdTests
    {
        [Test]
        public void Default_IsNone()
        {
            Assert.That(default(NodeId), Is.EqualTo(NodeId.None));
            Assert.That(NodeId.None.IsValid, Is.False);
        }

        [Test]
        public void PositiveValue_IsValid()
        {
            Assert.That(new NodeId(1).IsValid, Is.True);
            Assert.That(new NodeId(7).Value, Is.EqualTo(7));
        }

        [Test]
        public void ZeroOrNegative_IsNotValid()
        {
            Assert.That(new NodeId(0).IsValid, Is.False);
            Assert.That(new NodeId(-3).IsValid, Is.False);
        }

        [Test]
        public void SameValue_AreEqual()
        {
            Assert.That(new NodeId(4), Is.EqualTo(new NodeId(4)));
            Assert.That(new NodeId(4).GetHashCode(), Is.EqualTo(new NodeId(4).GetHashCode()));
        }

        [Test]
        public void DifferentValue_AreNotEqual()
        {
            Assert.That(new NodeId(4), Is.Not.EqualTo(new NodeId(5)));
        }

        [Test]
        public void EqualityOperator_MatchesEquals()
        {
            // NUnit의 Is.EqualTo는 Equals를 부르지 operator ==를 부르지 않는다.
            // 연산자가 반전돼 있어도 다른 테스트는 통과하므로 직접 검증한다.
            Assert.That(new NodeId(4) == new NodeId(4), Is.True);
            Assert.That(new NodeId(4) == new NodeId(5), Is.False);
            Assert.That(new NodeId(4) != new NodeId(5), Is.True);
            Assert.That(new NodeId(4) != new NodeId(4), Is.False);
        }

        [Test]
        public void ToString_ShowsIdOrNone()
        {
            Assert.That(new NodeId(7).ToString(), Is.EqualTo("#7"));
            Assert.That(NodeId.None.ToString(), Is.EqualTo("#none"));
        }

        [Test]
        public void ValueField_IsPublicAndAssignable()
        {
            // Unity 직렬화는 public 필드만 자동으로 잡는다. private이나 readonly면
            // 그래프 에셋에 저장된 노드 연결이 전부 0으로 리셋된다.
            // 이 테스트는 그 제약이 코드에 남아 있는지를 지킨다.
            var id = new NodeId(1);
            id.Value = 9;
            Assert.That(id.Value, Is.EqualTo(9));
            Assert.That(id.IsValid, Is.True);
        }
    }
}
