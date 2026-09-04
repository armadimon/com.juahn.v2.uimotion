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
    }
}
