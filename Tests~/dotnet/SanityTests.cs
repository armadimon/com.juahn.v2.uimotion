using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class SanityTests
    {
        [Test]
        public void TestHarness_Runs()
        {
            Assert.That(2 + 2, Is.EqualTo(4));
        }
    }
}
