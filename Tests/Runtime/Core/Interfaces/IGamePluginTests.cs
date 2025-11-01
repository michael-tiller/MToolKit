using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using VContainer;
using MToolKit.Runtime.Core.Interfaces;
using VContainer.Diagnostics;

namespace MToolKit.Tests.Runtime.Core.Interfaces
{
    /// <summary>
    /// Comprehensive unit tests for the IGamePlugin interface.
    /// Tests plugin registration behavior, interface contracts, and edge cases.
    /// </summary>
    [TestFixture]
    public sealed class IGamePluginTests
    {
        private TestGamePlugin _plugin;
        private MockContainerBuilder _mockBuilder;

        [SetUp]
        public void SetUp()
        {
            _plugin = new TestGamePlugin();
            _mockBuilder = new MockContainerBuilder();
        }

        [TearDown]
        public void TearDown()
        {
            _plugin?.Dispose();
            _mockBuilder?.Dispose();
            _plugin = null;
            _mockBuilder = null;
        }

        #region Register Method Tests

        [Test]
        [Category("Registration")]
        public void Register_WithValidBuilder_ShouldNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.Register(_mockBuilder));
        }

        [Test]
        [Category("Validation")]
        public void Register_WithNullBuilder_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => _plugin.Register(null));
            Assert.AreEqual("builder", exception.ParamName);
        }

        [Test]
        [Category("Registration")]
        public void Register_ShouldCallBuilderMethods()
        {
            // Act
            _plugin.Register(_mockBuilder);

            // Assert
            Assert.IsTrue(_mockBuilder.RegisterCalled, "Register method should be called");
            Assert.IsTrue(_mockBuilder.RegisterInstanceCalled, "RegisterInstance method should be called");
        }

        [Test]
        [Category("Registration")]
        public void Register_MultipleCalls_ShouldWorkCorrectly()
        {
            // Act
            _plugin.Register(_mockBuilder);
            _plugin.Register(_mockBuilder);

            // Assert
            Assert.AreEqual(2, _mockBuilder.RegisterCallCount, "Register should be called twice");
            Assert.AreEqual(2, _mockBuilder.RegisterInstanceCallCount, "RegisterInstance should be called twice");
        }

        #endregion

        #region Interface Contract Tests

        [Test]
        [Category("Contract")]
        public void IGamePlugin_ShouldBeAssignableFromInterface()
        {
            // Assert
            Assert.IsInstanceOf<IGamePlugin>(_plugin);
        }

        [Test]
        [Category("Contract")]
        public void IGamePlugin_ShouldHaveRegisterMethod()
        {
            // Arrange
            var interfaceType = typeof(IGamePlugin);

            // Act
            var method = interfaceType.GetMethod("Register");

            // Assert
            Assert.IsNotNull(method, "Register method should exist");
            Assert.AreEqual(typeof(void), method.ReturnType, "Register should return void");
            Assert.AreEqual(1, method.GetParameters().Length, "Register should have exactly one parameter");
            Assert.AreEqual(typeof(IContainerBuilder), method.GetParameters()[0].ParameterType, "Parameter should be IContainerBuilder");
        }

        [Test]
        [Category("Contract")]
        public void IGamePlugin_ShouldBePublicInterface()
        {
            // Assert
            Assert.IsTrue(typeof(IGamePlugin).IsPublic, "IGamePlugin should be public");
            Assert.IsTrue(typeof(IGamePlugin).IsInterface, "IGamePlugin should be an interface");
        }

        #endregion

        #region Implementation Tests

        [Test]
        [Category("Implementation")]
        public void TestGamePlugin_ShouldImplementIGamePlugin()
        {
            // Assert
            Assert.IsInstanceOf<IGamePlugin>(_plugin);
        }

        [Test]
        [Category("Implementation")]
        public void TestGamePlugin_RegisterShouldBeCallable()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.Register(_mockBuilder));
        }

        #endregion

        #region Edge Cases and Error Handling

        [Test]
        [Category("EdgeCases")]
        public void Register_WithDisposedBuilder_ShouldHandleGracefully()
        {
            // Arrange
            _mockBuilder.Dispose();

            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.Register(_mockBuilder), "Should handle disposed builder gracefully");
        }

        [Test]
        [Category("EdgeCases")]
        public void Register_WithBuilderHavingNullServices_ShouldNotThrow()
        {
            // Arrange
            _mockBuilder.Services = null;

            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.Register(_mockBuilder), "Should handle null services gracefully");
        }

        [Test]
        [Category("EdgeCases")]
        public void Register_WithBuilderThrowingException_ShouldPropagateException()
        {
            // Arrange
            _mockBuilder.ShouldThrowOnRegister = true;

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _plugin.Register(_mockBuilder), "Should propagate builder exceptions");
        }

        #endregion

        #region Performance Tests

        [Test]
        [Category("Performance")]
        public void Register_PerformanceTest_ShouldCompleteQuickly()
        {
            // Arrange
            const int iterations = 1000;
            var stopwatch = Stopwatch.StartNew();

            // Act
            for (int i = 0; i < iterations; i++)
            {
                _plugin.Register(_mockBuilder);
            }
            stopwatch.Stop();

            // Assert
            Assert.Less(stopwatch.ElapsedMilliseconds, 1000, 
                $"Register should complete within 1 second for {iterations} calls. Actual: {stopwatch.ElapsedMilliseconds}ms");
        }

        [Test]
        [Category("Performance")]
        public void Register_MemoryUsage_ShouldNotLeak()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            for (int i = 0; i < 100; i++)
            {
                _plugin.Register(_mockBuilder);
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Assert
            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;
            Assert.Less(memoryIncrease, 1024 * 1024, // Less than 1MB increase
                $"Memory usage should not increase significantly. Increase: {memoryIncrease} bytes");
        }

        #endregion

        #region Mock Classes and Test Utilities

        /// <summary>
        /// Mock implementation of IGamePlugin for testing.
        /// Simulates typical plugin registration behavior.
        /// </summary>
        private sealed class TestGamePlugin : IGamePlugin, IDisposable
        {
            private bool _disposed;

            public void Register(IContainerBuilder builder)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(TestGamePlugin));

                if (builder == null)
                    throw new ArgumentNullException(nameof(builder));

                // Cast to our mock to access tracking properties
                if (builder is MockContainerBuilder mockBuilder)
                {
                    // Simulate typical plugin registration
                    mockBuilder.Register<TestService>(Lifetime.Singleton);
                    mockBuilder.RegisterInstance(new TestService());
                }
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// Mock implementation of IContainerBuilder for testing.
        /// Implements the minimal interface required for testing.
        /// </summary>
        private sealed class MockContainerBuilder : IContainerBuilder, IDisposable
        {
            private bool _disposed;
            private bool _shouldThrowOnRegister;
            private readonly List<RegistrationBuilder> _registrations = new();

            public bool RegisterCalled { get; private set; }
            public bool RegisterInstanceCalled { get; private set; }
            public int RegisterCallCount { get; private set; }
            public int RegisterInstanceCallCount { get; private set; }
            public object Services { get; set; } = new object();
            public bool ShouldThrowOnRegister 
            { 
                get => _shouldThrowOnRegister; 
                set => _shouldThrowOnRegister = value; 
            }

            // Required IContainerBuilder properties with correct types
            public object ApplicationOrigin { get; set; } = "Test";
            public DiagnosticsCollector Diagnostics { get; set; }
            public int Count => _registrations.Count;
            public RegistrationBuilder this[int index] 
            { 
                get => _registrations[index]; 
                set => _registrations[index] = value; 
            }

            public void Register<T>(Lifetime lifetime) where T : class
            {
                if (_disposed)
                    return;

                if (_shouldThrowOnRegister)
                    throw new InvalidOperationException("Mock builder configured to throw");

                RegisterCalled = true;
                RegisterCallCount++;
            }

            // Explicit interface implementation to avoid constraint conflicts
            T IContainerBuilder.Register<T>(T instance)
            {
                RegisterInstance(instance);
                return instance;
            }

            public void RegisterInstance<T>(T instance) where T : class
            {
                if (_disposed)
                    return;

                if (_shouldThrowOnRegister)
                    throw new InvalidOperationException("Mock builder configured to throw");

                RegisterInstanceCalled = true;
                RegisterInstanceCallCount++;
            }

            public void RegisterInstance<T>(T instance, Lifetime lifetime) where T : class
            {
                if (_disposed)
                    return;

                if (_shouldThrowOnRegister)
                    throw new InvalidOperationException("Mock builder configured to throw");

                RegisterInstanceCalled = true;
                RegisterInstanceCallCount++;
            }

            public bool Exists(Type type, bool includeInterface, bool includeInherited)
            {
                return false;
            }

            public void RegisterBuildCallback(Action<IObjectResolver> callback)
            {
                // Mock implementation - no-op
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _disposed = true;
                    Services = null;
                    _registrations.Clear();
                }
            }
        }

        /// <summary>
        /// Simple test service for registration testing.
        /// </summary>
        private sealed class TestService
        {
            public string Name { get; set; } = "TestService";
            public DateTime CreatedAt { get; } = DateTime.UtcNow;
        }

        /// <summary>
        /// Mock DiagnosticsCollector for testing.
        /// </summary>
        private sealed class MockDiagnosticsCollector
        {
            public string Name => "MockDiagnosticsCollector";
        }

        #endregion
    }
}