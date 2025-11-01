/**
 * Unit tests for IGameRuntime.cs
 * Generated from function analysis on 2025-10-01
 * Framework: Unity Test Framework with NUnit
 * 
 * Test Coverage:
 * - Interface contract testing with mock implementations
 * - Lifecycle method testing (Start, Tick, LateTick, FixedTick, Shutdown)
 * - Parameter validation and edge case testing
 * - Call order verification and state tracking
 * - Exception handling and error scenarios
 * - Method-specific exception control and isolation testing
 * 
 * Mock Dependencies:
 * - TestGameRuntime implementation with call tracking
 * - VContainer dependency injection setup
 * - Serilog ILogger mocking
 * 
 * Shared Infrastructure:
 * - Generic test base class for identical interface contracts
 * - Reusable test patterns for IRuntimeSystem and IGameRuntime
 * - Type-safe testing with generics
 * - Consistent test patterns across both interfaces
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using UnityEngine.TestTools;
using Serilog;
using NSubstitute;
using ILogger = Serilog.ILogger;
using MToolKit.Tests.Runtime.Core;
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Tests.Runtime.Core.Interfaces
{
    /// <summary>
    /// Generic test implementation for runtime interfaces with identical contracts
    /// CRITICAL: Supports granular exception control for precise testing scenarios
    /// </summary>
    public class TestRuntimeInterface<T> where T : class
    {
        public string Name { get; }
        public bool StartCalled { get; private set; }
        public int StartCallCount { get; private set; }
        public bool TickCalled { get; private set; }
        public int TickCallCount { get; private set; }
        public List<float> TickDeltaTimes { get; private set; } = new List<float>();
        public bool LateTickCalled { get; private set; }
        public int LateTickCallCount { get; private set; }
        public List<float> LateTickDeltaTimes { get; private set; } = new List<float>();
        public bool FixedTickCalled { get; private set; }
        public int FixedTickCallCount { get; private set; }
        public List<float> FixedTickDeltaTimes { get; private set; } = new List<float>();
        public bool ShutdownCalled { get; private set; }
        public int ShutdownCallCount { get; private set; }
        public Exception LastThrownException { get; private set; }
        
        // Global exception control
        public bool ShouldThrowException { get; set; }
        public string ExceptionMessage { get; set; } = "Test exception";
        
        // Method-specific exception control for precise testing
        public bool ShouldThrowOnStart { get; set; }
        public bool ShouldThrowOnShutdown { get; set; }
        public bool ShouldThrowOnTick { get; set; }
        public bool ShouldThrowOnLateTick { get; set; }
        public bool ShouldThrowOnFixedTick { get; set; }

        public TestRuntimeInterface(string name)
        {
            Name = name;
        }

        public void Start()
        {
            StartCallCount++;
            if (ShouldThrowException || ShouldThrowOnStart)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            StartCalled = true;
        }

        public void Tick(float deltaTime)
        {
            TickCallCount++;
            TickDeltaTimes.Add(deltaTime);
            if (ShouldThrowException || ShouldThrowOnTick)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            TickCalled = true;
        }

        public void LateTick(float deltaTime)
        {
            LateTickCallCount++;
            LateTickDeltaTimes.Add(deltaTime);
            if (ShouldThrowException || ShouldThrowOnLateTick)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            LateTickCalled = true;
        }

        public void FixedTick(float deltaTime)
        {
            FixedTickCallCount++;
            FixedTickDeltaTimes.Add(deltaTime);
            if (ShouldThrowException || ShouldThrowOnFixedTick)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            FixedTickCalled = true;
        }

        public void Shutdown()
        {
            ShutdownCallCount++;
            if (ShouldThrowException || ShouldThrowOnShutdown)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            ShutdownCalled = true;
        }
        
        public void ResetCallCounts()
        {
            StartCallCount = 0;
            TickCallCount = 0;
            LateTickCallCount = 0;
            FixedTickCallCount = 0;
            ShutdownCallCount = 0;
            TickDeltaTimes.Clear();
            LateTickDeltaTimes.Clear();
            FixedTickDeltaTimes.Clear();
            LastThrownException = null;
            
            // Reset exception behavior
            ShouldThrowException = false;
            ShouldThrowOnStart = false;
            ShouldThrowOnShutdown = false;
            ShouldThrowOnTick = false;
            ShouldThrowOnLateTick = false;
            ShouldThrowOnFixedTick = false;
            ExceptionMessage = "Test exception";
        }
        
        public bool VerifyCallCounts(int expectedStart = 0, int expectedTick = 0, int expectedLateTick = 0, int expectedFixedTick = 0, int expectedShutdown = 0)
        {
            return StartCallCount == expectedStart &&
                   TickCallCount == expectedTick &&
                   LateTickCallCount == expectedLateTick &&
                   FixedTickCallCount == expectedFixedTick &&
                   ShutdownCallCount == expectedShutdown;
        }
    }

    /// <summary>
    /// Test implementation for IGameRuntime interface
    /// </summary>
    public class TestGameRuntime : TestRuntimeInterface<IGameRuntime>, IGameRuntime
    {
        public TestGameRuntime(string name) : base(name) { }
    }

    /// <summary>
    /// Test data constants and factory methods for IGameRuntime testing
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// CRITICAL: Make test classes public for NSubstitute proxy creation (internal classes cause proxy errors)
    /// </summary>
    public static class IGameRuntimeTestData
    {
        // Basic test values
        public const float ValidDeltaTime = 0.016f;
        public const float ZeroDeltaTime = 0f;
        public const float NegativeDeltaTime = -0.016f;
        public const float LargeDeltaTime = 1f;
        public const string TestGameRuntimeName = "TestGameRuntime";
        public const string TestGameRuntimeName2 = "TestGameRuntime2";
        public const string TestExceptionMessage = "Test exception";
        
        // Edge case delta times for parameterized testing
        public static readonly float[] EdgeCaseDeltaTimes = { ZeroDeltaTime, NegativeDeltaTime, LargeDeltaTime };
        
        // Factory methods for consistent test object creation
        public static List<IGameRuntime> CreateGameRuntimes(int count, bool shouldThrow = false, string exceptionMessage = TestExceptionMessage)
        {
            return Enumerable.Range(0, count)
                .Select(i => new TestGameRuntime($"GameRuntime{i}")
                {
                    ShouldThrowException = shouldThrow,
                    ExceptionMessage = exceptionMessage
                })
                .Cast<IGameRuntime>()
                .ToList();
        }
        
        public static TestGameRuntime CreateGameRuntime(string name = TestGameRuntimeName, bool shouldThrow = false, string exceptionMessage = TestExceptionMessage)
        {
            return new TestGameRuntime(name)
            {
                ShouldThrowException = shouldThrow,
                ExceptionMessage = exceptionMessage
            };
        }
        
        public static TestGameRuntime CreateGameRuntimeWithMethodSpecificExceptions(string name = TestGameRuntimeName)
        {
            return new TestGameRuntime(name);
        }
        
        /// <summary>
        /// Creates a game runtime configured to throw exceptions on specific methods
        /// </summary>
        public static TestGameRuntime CreateGameRuntimeWithMethodExceptions(
            string name = TestGameRuntimeName,
            bool throwOnStart = false,
            bool throwOnTick = false,
            bool throwOnLateTick = false,
            bool throwOnFixedTick = false,
            bool throwOnShutdown = false,
            string exceptionMessage = TestExceptionMessage)
        {
            return new TestGameRuntime(name)
            {
                ShouldThrowOnStart = throwOnStart,
                ShouldThrowOnTick = throwOnTick,
                ShouldThrowOnLateTick = throwOnLateTick,
                ShouldThrowOnFixedTick = throwOnFixedTick,
                ShouldThrowOnShutdown = throwOnShutdown,
                ExceptionMessage = exceptionMessage
            };
        }
    }

    /// <summary>
    /// Generic base test fixture for runtime interfaces with identical contracts
    /// </summary>
    [TestFixture]
    public abstract class RuntimeInterfaceTestBase<T> where T : class
    {
        protected ContainerBuilder _containerBuilder;
        protected IObjectResolver _resolver;
        protected ILogger _mockLogger;
        
        [SetUp]
        public void Setup()
        {
            _containerBuilder = new ContainerBuilder();
            SetupTestContainer();
            _resolver = _containerBuilder.Build();
        }
        
        [TearDown]
        public void TearDown()
        {
            _resolver?.Dispose();
        }
        
        /// <summary>
        /// Sets up the VContainer test container with common mocks and dependencies
        /// </summary>
        protected virtual void SetupTestContainer()
        {
            // Register mock logger with Serilog alias to avoid Unity conflicts
            // Using MockLogger instead of NSubstitute for IL2CPP compatibility (no Reflection.Emit)
            _mockLogger = new MockLogger();
            _containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
        }
        
        /// <summary>
        /// Helper method to create isolated VContainer instance per test
        /// CRITICAL: Prevents registration conflicts between tests
        /// </summary>
        protected U CreateTestInstanceWithDependencies<U>(params object[] dependencies) where U : class
        {
            // Create fresh container builder for this test
            var testContainerBuilder = new ContainerBuilder();
            
            // Register common dependencies
            testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            
            // Register test-specific dependencies
            foreach (var dependency in dependencies)
            {
                testContainerBuilder.RegisterInstance(dependency).As(dependency.GetType());
            }
            
            // CRITICAL: Always register the class being tested
            testContainerBuilder.Register<U>(Lifetime.Singleton);
            
            // Build and resolve
            var testResolver = testContainerBuilder.Build();
            var instance = testResolver.Resolve<U>();
            
            // Store for cleanup
            _resolver?.Dispose();
            _resolver = testResolver;
            
            return instance;
        }
    }

    /// <summary>
    /// Main test fixture for IGameRuntime interface testing
    /// </summary>
    [TestFixture]
    public class IGameRuntimeTests : RuntimeInterfaceTestBase<IGameRuntime>
    {
        /// <summary>
        /// Tests for the Start method of IGameRuntime
        /// </summary>
        [TestFixture]
        public class StartTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [Test]
            public void Start_WhenCalled_ShouldInitializeGameRuntime()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.Start();

                // Assert
                Assert.That(gameRuntime.StartCalled, Is.True);
                Assert.That(gameRuntime.StartCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Start_WhenCalledMultipleTimes_ShouldHandleGracefully()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.Start();
                gameRuntime.Start();
                gameRuntime.Start();

                // Assert
                Assert.That(gameRuntime.StartCalled, Is.True);
                Assert.That(gameRuntime.StartCallCount, Is.EqualTo(3));
            }

            [Test]
            public void Start_WhenGameRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());
                Assert.That(exception.Message, Is.EqualTo(IGameRuntimeTestData.TestExceptionMessage));
                Assert.That(gameRuntime.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void Start_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnStart()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntimeWithMethodSpecificExceptions();
                gameRuntime.ShouldThrowOnStart = true;
                gameRuntime.ExceptionMessage = "Start failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());
                    Assert.That(exception.Message, Is.EqualTo("Start failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    gameRuntime.ShouldThrowOnStart = false;
                }
            }
        }

        /// <summary>
        /// Tests for the Tick method of IGameRuntime
        /// </summary>
        [TestFixture]
        public class TickTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [Test]
            public void Tick_WhenCalledWithValidDeltaTime_ShouldProcessUpdate()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime);

                // Assert
                Assert.That(gameRuntime.TickCalled, Is.True);
                Assert.That(gameRuntime.TickCallCount, Is.EqualTo(1));
                Assert.That(gameRuntime.TickDeltaTimes, Contains.Item(IGameRuntimeTestData.ValidDeltaTime));
            }

            [TestCase(IGameRuntimeTestData.ZeroDeltaTime)]
            [TestCase(IGameRuntimeTestData.NegativeDeltaTime)]
            [TestCase(IGameRuntimeTestData.LargeDeltaTime)]
            public void Tick_WhenCalledWithEdgeCaseDeltaTime_ShouldHandleGracefully(float deltaTime)
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.Tick(deltaTime);

                // Assert
                Assert.That(gameRuntime.TickCalled, Is.True);
                Assert.That(gameRuntime.TickDeltaTimes, Contains.Item(deltaTime));
            }

            [Test]
            public void Tick_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();
                var deltaTimes = new[] { 0.016f, 0.033f, 0.05f };

                // Act
                foreach (var deltaTime in deltaTimes)
                {
                    gameRuntime.Tick(deltaTime);
                }

                // Assert
                Assert.That(gameRuntime.TickCallCount, Is.EqualTo(3));
                Assert.That(gameRuntime.TickDeltaTimes, Is.EqualTo(deltaTimes));
            }

            [Test]
            public void Tick_WhenGameRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                Assert.That(exception.Message, Is.EqualTo(IGameRuntimeTestData.TestExceptionMessage));
                Assert.That(gameRuntime.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void Tick_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnTick()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntimeWithMethodSpecificExceptions();
                gameRuntime.ShouldThrowOnTick = true;
                gameRuntime.ExceptionMessage = "Tick failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.That(exception.Message, Is.EqualTo("Tick failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => gameRuntime.Start());
                    Assert.DoesNotThrow(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    gameRuntime.ShouldThrowOnTick = false;
                }
            }
        }

        /// <summary>
        /// Tests for the LateTick method of IGameRuntime
        /// </summary>
        [TestFixture]
        public class LateTickTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [Test]
            public void LateTick_WhenCalledWithValidDeltaTime_ShouldProcessLateUpdate()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime);

                // Assert
                Assert.That(gameRuntime.LateTickCalled, Is.True);
                Assert.That(gameRuntime.LateTickCallCount, Is.EqualTo(1));
                Assert.That(gameRuntime.LateTickDeltaTimes, Contains.Item(IGameRuntimeTestData.ValidDeltaTime));
            }

            [TestCase(IGameRuntimeTestData.ZeroDeltaTime)]
            [TestCase(IGameRuntimeTestData.NegativeDeltaTime)]
            [TestCase(IGameRuntimeTestData.LargeDeltaTime)]
            public void LateTick_WhenCalledWithEdgeCaseDeltaTime_ShouldHandleGracefully(float deltaTime)
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.LateTick(deltaTime);

                // Assert
                Assert.That(gameRuntime.LateTickCalled, Is.True);
                Assert.That(gameRuntime.LateTickDeltaTimes, Contains.Item(deltaTime));
            }

            [Test]
            public void LateTick_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();
                var deltaTimes = new[] { 0.016f, 0.033f, 0.05f };

                // Act
                foreach (var deltaTime in deltaTimes)
                {
                    gameRuntime.LateTick(deltaTime);
                }

                // Assert
                Assert.That(gameRuntime.LateTickCallCount, Is.EqualTo(3));
                Assert.That(gameRuntime.LateTickDeltaTimes, Is.EqualTo(deltaTimes));
            }

            [Test]
            public void LateTick_WhenGameRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                Assert.That(exception.Message, Is.EqualTo(IGameRuntimeTestData.TestExceptionMessage));
                Assert.That(gameRuntime.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void LateTick_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnLateTick()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntimeWithMethodSpecificExceptions();
                gameRuntime.ShouldThrowOnLateTick = true;
                gameRuntime.ExceptionMessage = "LateTick failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.That(exception.Message, Is.EqualTo("LateTick failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => gameRuntime.Start());
                    Assert.DoesNotThrow(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    gameRuntime.ShouldThrowOnLateTick = false;
                }
            }
        }

        /// <summary>
        /// Tests for the FixedTick method of IGameRuntime
        /// </summary>
        [TestFixture]
        public class FixedTickTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [Test]
            public void FixedTick_WhenCalledWithValidDeltaTime_ShouldProcessFixedUpdate()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime);

                // Assert
                Assert.That(gameRuntime.FixedTickCalled, Is.True);
                Assert.That(gameRuntime.FixedTickCallCount, Is.EqualTo(1));
                Assert.That(gameRuntime.FixedTickDeltaTimes, Contains.Item(IGameRuntimeTestData.ValidDeltaTime));
            }

            [TestCase(IGameRuntimeTestData.ZeroDeltaTime)]
            [TestCase(IGameRuntimeTestData.NegativeDeltaTime)]
            [TestCase(IGameRuntimeTestData.LargeDeltaTime)]
            public void FixedTick_WhenCalledWithEdgeCaseDeltaTime_ShouldHandleGracefully(float deltaTime)
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.FixedTick(deltaTime);

                // Assert
                Assert.That(gameRuntime.FixedTickCalled, Is.True);
                Assert.That(gameRuntime.FixedTickDeltaTimes, Contains.Item(deltaTime));
            }

            [Test]
            public void FixedTick_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();
                var deltaTimes = new[] { 0.016f, 0.033f, 0.05f };

                // Act
                foreach (var deltaTime in deltaTimes)
                {
                    gameRuntime.FixedTick(deltaTime);
                }

                // Assert
                Assert.That(gameRuntime.FixedTickCallCount, Is.EqualTo(3));
                Assert.That(gameRuntime.FixedTickDeltaTimes, Is.EqualTo(deltaTimes));
            }

            [Test]
            public void FixedTick_WhenGameRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                Assert.That(exception.Message, Is.EqualTo(IGameRuntimeTestData.TestExceptionMessage));
                Assert.That(gameRuntime.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void FixedTick_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnFixedTick()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntimeWithMethodSpecificExceptions();
                gameRuntime.ShouldThrowOnFixedTick = true;
                gameRuntime.ExceptionMessage = "FixedTick failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.That(exception.Message, Is.EqualTo("FixedTick failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => gameRuntime.Start());
                    Assert.DoesNotThrow(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    gameRuntime.ShouldThrowOnFixedTick = false;
                }
            }
        }

        /// <summary>
        /// Tests for the Shutdown method of IGameRuntime
        /// </summary>
        [TestFixture]
        public class ShutdownTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [Test]
            public void Shutdown_WhenCalled_ShouldCleanupGameRuntime()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.Shutdown();

                // Assert
                Assert.That(gameRuntime.ShutdownCalled, Is.True);
                Assert.That(gameRuntime.ShutdownCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Shutdown_WhenCalledMultipleTimes_ShouldHandleGracefully()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.Shutdown();
                gameRuntime.Shutdown();
                gameRuntime.Shutdown();

                // Assert
                Assert.That(gameRuntime.ShutdownCalled, Is.True);
                Assert.That(gameRuntime.ShutdownCallCount, Is.EqualTo(3));
            }

            [Test]
            public void Shutdown_WhenGameRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Shutdown());
                Assert.That(exception.Message, Is.EqualTo(IGameRuntimeTestData.TestExceptionMessage));
                Assert.That(gameRuntime.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void Shutdown_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnShutdown()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntimeWithMethodSpecificExceptions();
                gameRuntime.ShouldThrowOnShutdown = true;
                gameRuntime.ExceptionMessage = "Shutdown failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Shutdown());
                    Assert.That(exception.Message, Is.EqualTo("Shutdown failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => gameRuntime.Start());
                    Assert.DoesNotThrow(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    gameRuntime.ShouldThrowOnShutdown = false;
                }
            }
        }

        /// <summary>
        /// Integration tests for complete IGameRuntime lifecycle
        /// </summary>
        [TestFixture]
        public class LifecycleIntegrationTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [Test]
            public void Lifecycle_WhenCompleteSequenceExecuted_ShouldTrackAllCalls()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act - Complete lifecycle sequence
                gameRuntime.Start();
                gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.Shutdown();

                // Assert
                Assert.That(gameRuntime.StartCalled, Is.True);
                Assert.That(gameRuntime.TickCalled, Is.True);
                Assert.That(gameRuntime.LateTickCalled, Is.True);
                Assert.That(gameRuntime.FixedTickCalled, Is.True);
                Assert.That(gameRuntime.ShutdownCalled, Is.True);
                Assert.That(gameRuntime.VerifyCallCounts(1, 1, 1, 1, 1), Is.True);
            }

            [Test]
            public void Lifecycle_WhenMultipleCyclesExecuted_ShouldMaintainConsistency()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act - Multiple lifecycle cycles
                gameRuntime.Start();
                gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.Shutdown();

                gameRuntime.Start();
                gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.Shutdown();

                // Assert
                Assert.That(gameRuntime.VerifyCallCounts(2, 2, 2, 2, 2), Is.True);
            }

            [Test]
            public void Lifecycle_WhenStartThrowsException_ShouldPreventSubsequentCalls()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();
                gameRuntime.ShouldThrowOnStart = true;

                try
                {
                    // Act & Assert
                    Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());
                    
                    // Subsequent calls should work normally
                    Assert.DoesNotThrow(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.Shutdown());
                }
                finally
                {
                    gameRuntime.ShouldThrowOnStart = false;
                }
            }

            [Test]
            public void Lifecycle_WhenTickThrowsException_ShouldNotAffectOtherMethods()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();
                gameRuntime.ShouldThrowOnTick = true;

                try
                {
                    // Act
                    gameRuntime.Start();
                    Assert.Throws<InvalidOperationException>(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime);
                    gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime);
                    gameRuntime.Shutdown();

                    // Assert
                    Assert.That(gameRuntime.StartCalled, Is.True);
                    Assert.That(gameRuntime.TickCalled, Is.False); // Exception prevented completion
                    Assert.That(gameRuntime.LateTickCalled, Is.True);
                    Assert.That(gameRuntime.FixedTickCalled, Is.True);
                    Assert.That(gameRuntime.ShutdownCalled, Is.True);
                }
                finally
                {
                    gameRuntime.ShouldThrowOnTick = false;
                }
            }

            [Test]
            public void Lifecycle_WhenShutdownThrowsException_ShouldNotAffectOtherMethods()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();
                gameRuntime.ShouldThrowOnShutdown = true;

                try
                {
                    // Act
                    gameRuntime.Start();
                    gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime);
                    gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime);
                    gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime);
                    Assert.Throws<InvalidOperationException>(() => gameRuntime.Shutdown());

                    // Assert
                    Assert.That(gameRuntime.StartCalled, Is.True);
                    Assert.That(gameRuntime.TickCalled, Is.True);
                    Assert.That(gameRuntime.LateTickCalled, Is.True);
                    Assert.That(gameRuntime.FixedTickCalled, Is.True);
                    Assert.That(gameRuntime.ShutdownCalled, Is.False); // Exception prevented completion
                }
                finally
                {
                    gameRuntime.ShouldThrowOnShutdown = false;
                }
            }
        }

        /// <summary>
        /// Exception isolation tests to verify error handling doesn't affect other components
        /// </summary>
        [TestFixture]
        public class ExceptionIsolationTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [Test]
            public void ExceptionIsolation_WhenOneMethodThrows_ShouldNotAffectOtherMethods()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntimeWithMethodSpecificExceptions();
                gameRuntime.ShouldThrowOnTick = true;
                gameRuntime.ExceptionMessage = "Tick failed";

                try
                {
                    // Act & Assert - Other methods should work normally
                    Assert.DoesNotThrow(() => gameRuntime.Start());
                    Assert.That(gameRuntime.StartCalled, Is.True);
                    
                    // Specific method should throw exception
                    var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.That(exception.Message, Is.EqualTo("Tick failed"));
                    
                    // Other methods should still work
                    Assert.DoesNotThrow(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.That(gameRuntime.LateTickCalled, Is.True);
                    
                    Assert.DoesNotThrow(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.That(gameRuntime.FixedTickCalled, Is.True);
                    
                    Assert.DoesNotThrow(() => gameRuntime.Shutdown());
                    Assert.That(gameRuntime.ShutdownCalled, Is.True);
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    gameRuntime.ShouldThrowOnTick = false;
                }
            }

            [Test]
            public void ExceptionIsolation_WhenMultipleMethodsConfiguredToThrow_ShouldThrowOnEach()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntimeWithMethodSpecificExceptions();
                gameRuntime.ShouldThrowOnStart = true;
                gameRuntime.ShouldThrowOnTick = true;
                gameRuntime.ShouldThrowOnShutdown = true;
                gameRuntime.ExceptionMessage = "Method failed";

                try
                {
                    // Act & Assert
                    Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());
                    Assert.Throws<InvalidOperationException>(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                    Assert.Throws<InvalidOperationException>(() => gameRuntime.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    gameRuntime.ShouldThrowOnStart = false;
                    gameRuntime.ShouldThrowOnTick = false;
                    gameRuntime.ShouldThrowOnShutdown = false;
                }
            }
        }

        /// <summary>
        /// Interface contract tests to verify IGameRuntime interface compliance
        /// </summary>
        [TestFixture]
        public class InterfaceContractTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [Test]
            public void InterfaceContract_WhenImplemented_ShouldProvideAllRequiredMethods()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act & Assert - Verify all interface methods are callable
                Assert.DoesNotThrow(() => gameRuntime.Start());
                Assert.DoesNotThrow(() => gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime));
                Assert.DoesNotThrow(() => gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime));
                Assert.DoesNotThrow(() => gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime));
                Assert.DoesNotThrow(() => gameRuntime.Shutdown());
            }

            [Test]
            public void InterfaceContract_WhenMethodsCalledInAnyOrder_ShouldWorkCorrectly()
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act - Call methods in non-standard order
                gameRuntime.Tick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.Start();
                gameRuntime.FixedTick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.LateTick(IGameRuntimeTestData.ValidDeltaTime);
                gameRuntime.Shutdown();

                // Assert
                Assert.That(gameRuntime.StartCalled, Is.True);
                Assert.That(gameRuntime.TickCalled, Is.True);
                Assert.That(gameRuntime.LateTickCalled, Is.True);
                Assert.That(gameRuntime.FixedTickCalled, Is.True);
                Assert.That(gameRuntime.ShutdownCalled, Is.True);
            }
        }

        /// <summary>
        /// Parameter validation tests for edge cases and boundary values
        /// </summary>
        [TestFixture]
        public class ParameterValidationTests : RuntimeInterfaceTestBase<IGameRuntime>
        {
            [TestCase(IGameRuntimeTestData.ZeroDeltaTime)]
            [TestCase(IGameRuntimeTestData.NegativeDeltaTime)]
            [TestCase(IGameRuntimeTestData.LargeDeltaTime)]
            public void ParameterValidation_WhenDeltaTimeIsEdgeCase_ShouldAcceptValue(float deltaTime)
            {
                // Arrange
                var gameRuntime = IGameRuntimeTestData.CreateGameRuntime();

                // Act
                gameRuntime.Tick(deltaTime);
                gameRuntime.LateTick(deltaTime);
                gameRuntime.FixedTick(deltaTime);

                // Assert
                Assert.That(gameRuntime.TickDeltaTimes, Contains.Item(deltaTime));
                Assert.That(gameRuntime.LateTickDeltaTimes, Contains.Item(deltaTime));
                Assert.That(gameRuntime.FixedTickDeltaTimes, Contains.Item(deltaTime));
            }
        }
    }
}
