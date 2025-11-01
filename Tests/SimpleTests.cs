using NUnit.Framework;

namespace MToolKit.Tests
{
    [TestFixture]
    public class SimpleTests
    {
        [Test]
        public void BasicTest_ShouldPass()
        {
            Assert.IsTrue(true);
        }

        [Test]
        public void MathTest_ShouldCalculateCorrectly()
        {
            int result = 2 + 2;
            Assert.AreEqual(4, result);
        }
    }
}
