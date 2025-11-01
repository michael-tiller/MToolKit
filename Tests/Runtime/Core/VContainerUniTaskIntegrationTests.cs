using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace MToolKit.Tests.Runtime.Core
{
    /// <summary>
    /// Tests for VContainer + UniTask integration to ensure IAsyncStartable can be registered as entry points.
    /// </summary>
    [TestFixture]
    public class VContainerUniTaskIntegrationTests
    {
        private GameObject testGameObject;

        [SetUp]
        public void SetUp()
        {
            testGameObject = new GameObject("TestGameObject");
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }
        }

        [Test]
        public void IAsyncStartable_CanBeRegisteredAsEntryPoint()
        {
            // Arrange
            var builder = new ContainerBuilder();
            // Create a mock IAsyncStartable for testing registration
            
            // Act & Assert - This should not throw
            Assert.DoesNotThrow(() => builder.RegisterEntryPoint<IAsyncStartable>(resolver => 
            {
                // Return a simple mock that implements IAsyncStartable
                return new MockAsyncStartable();
            }, Lifetime.Singleton));
        }
    }

    /// <summary>
    /// Simple mock implementation of IAsyncStartable for testing registration
    /// </summary>
    public class MockAsyncStartable : IAsyncStartable
    {
        public UniTask StartAsync(CancellationToken cancellation)
        {
            return UniTask.CompletedTask;
        }
    }
}
