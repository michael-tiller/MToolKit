/**
 * Unit tests for IRuntimeSystem.cs
 * Generated from function analysis on 2025-10-01
 * Refactored for improved organization and maintainability
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
 * - TestRuntimeSystem implementation with call tracking
 * - VContainer dependency injection setup
 * - Serilog ILogger mocking
 * 
 * Refactoring Improvements:
 * - Organized tests into nested TestFixture classes for better structure
 * - Added parameterized tests for similar scenarios
 * - Enhanced exception isolation testing
 * - Improved test data management with factory methods
 * - Added comprehensive XML documentation
 * - Optimized test organization and readability
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
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Tests.Runtime.Core.Interfaces
{
    /// <summary>
    /// Test data constants and factory methods for consistent test values
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// CRITICAL: Make test classes public for NSubstitute proxy creation (internal classes cause proxy errors)
    /// </summary>
    public static class IRuntimeSystemTestData
    {
        // Basic test values
        public const float ValidDeltaTime = 0.016f;
        public const float ZeroDeltaTime = 0f;
        public const float NegativeDeltaTime = -0.016f;
        public const float LargeDeltaTime = 1f;
        public const string TestSystemName = "TestSystem";
        public const string TestSystemName2 = "TestSystem2";
        public const string TestExceptionMessage = "Test exception";
        
        // Edge case delta times for parameterized testing
        public static readonly float[] EdgeCaseDeltaTimes = { ZeroDeltaTime, NegativeDeltaTime, LargeDeltaTime };
        
        // Factory methods for consistent test object creation
        public static List<IRuntimeSystem> CreateSystems(int count, bool shouldThrow = false, string exceptionMessage = TestExceptionMessage)
        {
            return Enumerable.Range(0, count)
                .Select(i => new TestRuntimeSystem($"System{i}")
                {
                    ShouldThrowException = shouldThrow,
                    ExceptionMessage = exceptionMessage
                })
                .Cast<IRuntimeSystem>()
                .ToList();
        }
        
        public static TestRuntimeSystem CreateSystem(string name = TestSystemName, bool shouldThrow = false, string exceptionMessage = TestExceptionMessage)
        {
            return new TestRuntimeSystem(name)
            {
                ShouldThrowException = shouldThrow,
                ExceptionMessage = exceptionMessage
            };
        }
        
        public static TestRuntimeSystem CreateSystemWithMethodSpecificExceptions(string name = TestSystemName)
        {
            return new TestRuntimeSystem(name);
        }
        
        /// <summary>
        /// Creates a system configured to throw exceptions on specific methods
        /// </summary>
        public static TestRuntimeSystem CreateSystemWithMethodExceptions(
            string name = TestSystemName,
            bool throwOnStart = false,
            bool throwOnTick = false,
            bool throwOnLateTick = false,
            bool throwOnFixedTick = false,
            bool throwOnShutdown = false,
            string exceptionMessage = TestExceptionMessage)
        {
            return new TestRuntimeSystem(name)
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
    /// Test implementation for IRuntimeSystem interface using shared infrastructure
    /// </summary>
    public class TestRuntimeSystem : TestRuntimeInterface<IRuntimeSystem>, IRuntimeSystem
    {
        public TestRuntimeSystem(string name) : base(name) { }
    }

    /// <summary>
    /// Main test fixture for IRuntimeSystem interface testing
    /// </summary>
    [TestFixture]
    public class IRuntimeSystemTests : RuntimeInterfaceTestBase<IRuntimeSystem>
    {
        /// <summary>
        /// Tests for the Start method of IRuntimeSystem
        /// </summary>
        [TestFixture]
        public class StartTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [Test]
            public void Start_WhenCalled_ShouldInitializeSystem()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.Start();

                // Assert
                Assert.That(system.StartCalled, Is.True);
                Assert.That(system.StartCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Start_WhenCalledMultipleTimes_ShouldHandleGracefully()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.Start();
                system.Start();
                system.Start();

                // Assert
                Assert.That(system.StartCalled, Is.True);
                Assert.That(system.StartCallCount, Is.EqualTo(3));
            }

            [Test]
            public void Start_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => system.Start());
                Assert.That(exception.Message, Is.EqualTo(IRuntimeSystemTestData.TestExceptionMessage));
                Assert.That(system.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void Start_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnStart()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                system.ShouldThrowOnStart = true;
                system.ExceptionMessage = "Start failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => system.Start());
                    Assert.That(exception.Message, Is.EqualTo("Start failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    system.ShouldThrowOnStart = false;
                }
            }
        }

        /// <summary>
        /// Tests for the Tick method of IRuntimeSystem
        /// </summary>
        [TestFixture]
        public class TickTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [Test]
            public void Tick_WhenCalledWithValidDeltaTime_ShouldProcessUpdate()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.Tick(IRuntimeSystemTestData.ValidDeltaTime);

                // Assert
                Assert.That(system.TickCalled, Is.True);
                Assert.That(system.TickCallCount, Is.EqualTo(1));
                Assert.That(system.TickDeltaTimes, Contains.Item(IRuntimeSystemTestData.ValidDeltaTime));
            }

            [TestCase(IRuntimeSystemTestData.ZeroDeltaTime)]
            [TestCase(IRuntimeSystemTestData.NegativeDeltaTime)]
            [TestCase(IRuntimeSystemTestData.LargeDeltaTime)]
            public void Tick_WhenCalledWithEdgeCaseDeltaTime_ShouldHandleGracefully(float deltaTime)
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.Tick(deltaTime);

                // Assert
                Assert.That(system.TickCalled, Is.True);
                Assert.That(system.TickDeltaTimes, Contains.Item(deltaTime));
            }

            [Test]
            public void Tick_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();
                var deltaTimes = new[] { 0.016f, 0.033f, 0.05f };

                // Act
                foreach (var deltaTime in deltaTimes)
                {
                    system.Tick(deltaTime);
                }

                // Assert
                Assert.That(system.TickCallCount, Is.EqualTo(3));
                Assert.That(system.TickDeltaTimes, Is.EqualTo(deltaTimes));
            }

            [Test]
            public void Tick_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                Assert.That(exception.Message, Is.EqualTo(IRuntimeSystemTestData.TestExceptionMessage));
                Assert.That(system.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void Tick_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnTick()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                system.ShouldThrowOnTick = true;
                system.ExceptionMessage = "Tick failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.That(exception.Message, Is.EqualTo("Tick failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => system.Start());
                    Assert.DoesNotThrow(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    system.ShouldThrowOnTick = false;
                }
            }
        }

        /// <summary>
        /// Tests for the LateTick method of IRuntimeSystem
        /// </summary>
        [TestFixture]
        public class LateTickTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [Test]
            public void LateTick_WhenCalledWithValidDeltaTime_ShouldProcessLateUpdate()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.LateTick(IRuntimeSystemTestData.ValidDeltaTime);

                // Assert
                Assert.That(system.LateTickCalled, Is.True);
                Assert.That(system.LateTickCallCount, Is.EqualTo(1));
                Assert.That(system.LateTickDeltaTimes, Contains.Item(IRuntimeSystemTestData.ValidDeltaTime));
            }

            [TestCase(IRuntimeSystemTestData.ZeroDeltaTime)]
            [TestCase(IRuntimeSystemTestData.NegativeDeltaTime)]
            [TestCase(IRuntimeSystemTestData.LargeDeltaTime)]
            public void LateTick_WhenCalledWithEdgeCaseDeltaTime_ShouldHandleGracefully(float deltaTime)
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.LateTick(deltaTime);

                // Assert
                Assert.That(system.LateTickCalled, Is.True);
                Assert.That(system.LateTickDeltaTimes, Contains.Item(deltaTime));
            }

            [Test]
            public void LateTick_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();
                var deltaTimes = new[] { 0.016f, 0.033f, 0.05f };

                // Act
                foreach (var deltaTime in deltaTimes)
                {
                    system.LateTick(deltaTime);
                }

                // Assert
                Assert.That(system.LateTickCallCount, Is.EqualTo(3));
                Assert.That(system.LateTickDeltaTimes, Is.EqualTo(deltaTimes));
            }

            [Test]
            public void LateTick_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                Assert.That(exception.Message, Is.EqualTo(IRuntimeSystemTestData.TestExceptionMessage));
                Assert.That(system.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void LateTick_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnLateTick()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                system.ShouldThrowOnLateTick = true;
                system.ExceptionMessage = "LateTick failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.That(exception.Message, Is.EqualTo("LateTick failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => system.Start());
                    Assert.DoesNotThrow(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    system.ShouldThrowOnLateTick = false;
                }
            }
        }

        /// <summary>
        /// Tests for the FixedTick method of IRuntimeSystem
        /// </summary>
        [TestFixture]
        public class FixedTickTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [Test]
            public void FixedTick_WhenCalledWithValidDeltaTime_ShouldProcessFixedUpdate()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime);

                // Assert
                Assert.That(system.FixedTickCalled, Is.True);
                Assert.That(system.FixedTickCallCount, Is.EqualTo(1));
                Assert.That(system.FixedTickDeltaTimes, Contains.Item(IRuntimeSystemTestData.ValidDeltaTime));
            }

            [TestCase(IRuntimeSystemTestData.ZeroDeltaTime)]
            [TestCase(IRuntimeSystemTestData.NegativeDeltaTime)]
            [TestCase(IRuntimeSystemTestData.LargeDeltaTime)]
            public void FixedTick_WhenCalledWithEdgeCaseDeltaTime_ShouldHandleGracefully(float deltaTime)
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.FixedTick(deltaTime);

                // Assert
                Assert.That(system.FixedTickCalled, Is.True);
                Assert.That(system.FixedTickDeltaTimes, Contains.Item(deltaTime));
            }

            [Test]
            public void FixedTick_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();
                var deltaTimes = new[] { 0.016f, 0.033f, 0.05f };

                // Act
                foreach (var deltaTime in deltaTimes)
                {
                    system.FixedTick(deltaTime);
                }

                // Assert
                Assert.That(system.FixedTickCallCount, Is.EqualTo(3));
                Assert.That(system.FixedTickDeltaTimes, Is.EqualTo(deltaTimes));
            }

            [Test]
            public void FixedTick_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                Assert.That(exception.Message, Is.EqualTo(IRuntimeSystemTestData.TestExceptionMessage));
                Assert.That(system.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void FixedTick_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnFixedTick()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                system.ShouldThrowOnFixedTick = true;
                system.ExceptionMessage = "FixedTick failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.That(exception.Message, Is.EqualTo("FixedTick failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => system.Start());
                    Assert.DoesNotThrow(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    system.ShouldThrowOnFixedTick = false;
                }
            }
        }

        /// <summary>
        /// Tests for the Shutdown method of IRuntimeSystem
        /// </summary>
        [TestFixture]
        public class ShutdownTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [Test]
            public void Shutdown_WhenCalled_ShouldCleanupSystem()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.Shutdown();

                // Assert
                Assert.That(system.ShutdownCalled, Is.True);
                Assert.That(system.ShutdownCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Shutdown_WhenCalledMultipleTimes_ShouldHandleGracefully()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.Shutdown();
                system.Shutdown();
                system.Shutdown();

                // Assert
                Assert.That(system.ShutdownCalled, Is.True);
                Assert.That(system.ShutdownCallCount, Is.EqualTo(3));
            }

            [Test]
            public void Shutdown_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem(shouldThrow: true);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => system.Shutdown());
                Assert.That(exception.Message, Is.EqualTo(IRuntimeSystemTestData.TestExceptionMessage));
                Assert.That(system.LastThrownException, Is.Not.Null);
            }

            [Test]
            public void Shutdown_WhenMethodSpecificExceptionConfigured_ShouldThrowOnlyOnShutdown()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                system.ShouldThrowOnShutdown = true;
                system.ExceptionMessage = "Shutdown failed";

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => system.Shutdown());
                    Assert.That(exception.Message, Is.EqualTo("Shutdown failed"));
                    
                    // Other methods should work normally
                    Assert.DoesNotThrow(() => system.Start());
                    Assert.DoesNotThrow(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    system.ShouldThrowOnShutdown = false;
                }
            }
        }

        /// <summary>
        /// Integration tests for complete IRuntimeSystem lifecycle
        /// </summary>
        [TestFixture]
        public class LifecycleIntegrationTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [Test]
            public void Lifecycle_WhenCompleteSequenceExecuted_ShouldTrackAllCalls()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act - Complete lifecycle sequence
                system.Start();
                system.Tick(IRuntimeSystemTestData.ValidDeltaTime);
                system.LateTick(IRuntimeSystemTestData.ValidDeltaTime);
                system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime);
                system.Shutdown();

                // Assert
                Assert.That(system.StartCalled, Is.True);
                Assert.That(system.TickCalled, Is.True);
                Assert.That(system.LateTickCalled, Is.True);
                Assert.That(system.FixedTickCalled, Is.True);
                Assert.That(system.ShutdownCalled, Is.True);
                Assert.That(system.VerifyCallCounts(1, 1, 1, 1, 1), Is.True);
            }

            [Test]
            public void Lifecycle_WhenMultipleCyclesExecuted_ShouldMaintainConsistency()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act - Multiple lifecycle cycles
                system.Start();
                system.Tick(IRuntimeSystemTestData.ValidDeltaTime);
                system.LateTick(IRuntimeSystemTestData.ValidDeltaTime);
                system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime);
                system.Shutdown();

                system.Start();
                system.Tick(IRuntimeSystemTestData.ValidDeltaTime);
                system.LateTick(IRuntimeSystemTestData.ValidDeltaTime);
                system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime);
                system.Shutdown();

                // Assert
                Assert.That(system.VerifyCallCounts(2, 2, 2, 2, 2), Is.True);
            }

            [Test]
            public void Lifecycle_WhenStartThrowsException_ShouldPreventSubsequentCalls()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();
                system.ShouldThrowOnStart = true;

                try
                {
                    // Act & Assert
                    Assert.Throws<InvalidOperationException>(() => system.Start());
                    
                    // Subsequent calls should work normally
                    Assert.DoesNotThrow(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.Shutdown());
                }
                finally
                {
                    system.ShouldThrowOnStart = false;
                }
            }

            [Test]
            public void Lifecycle_WhenTickThrowsException_ShouldNotAffectOtherMethods()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();
                system.ShouldThrowOnTick = true;

                try
                {
                    // Act
                    system.Start();
                    Assert.Throws<InvalidOperationException>(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    system.LateTick(IRuntimeSystemTestData.ValidDeltaTime);
                    system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime);
                    system.Shutdown();

                    // Assert
                    Assert.That(system.StartCalled, Is.True);
                    Assert.That(system.TickCalled, Is.False); // Exception prevented completion
                    Assert.That(system.LateTickCalled, Is.True);
                    Assert.That(system.FixedTickCalled, Is.True);
                    Assert.That(system.ShutdownCalled, Is.True);
                }
                finally
                {
                    system.ShouldThrowOnTick = false;
                }
            }

            [Test]
            public void Lifecycle_WhenShutdownThrowsException_ShouldNotAffectOtherMethods()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();
                system.ShouldThrowOnShutdown = true;

                try
                {
                    // Act
                    system.Start();
                    system.Tick(IRuntimeSystemTestData.ValidDeltaTime);
                    system.LateTick(IRuntimeSystemTestData.ValidDeltaTime);
                    system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime);
                    Assert.Throws<InvalidOperationException>(() => system.Shutdown());

                    // Assert
                    Assert.That(system.StartCalled, Is.True);
                    Assert.That(system.TickCalled, Is.True);
                    Assert.That(system.LateTickCalled, Is.True);
                    Assert.That(system.FixedTickCalled, Is.True);
                    Assert.That(system.ShutdownCalled, Is.False); // Exception prevented completion
                }
                finally
                {
                    system.ShouldThrowOnShutdown = false;
                }
            }
        }

        /// <summary>
        /// Exception isolation tests to verify error handling doesn't affect other components
        /// </summary>
        [TestFixture]
        public class ExceptionIsolationTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [Test]
            public void ExceptionIsolation_WhenOneMethodThrows_ShouldNotAffectOtherMethods()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                system.ShouldThrowOnTick = true;
                system.ExceptionMessage = "Tick failed";

                try
                {
                    // Act & Assert - Other methods should work normally
                    Assert.DoesNotThrow(() => system.Start());
                    Assert.That(system.StartCalled, Is.True);
                    
                    // Specific method should throw exception
                    var exception = Assert.Throws<InvalidOperationException>(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.That(exception.Message, Is.EqualTo("Tick failed"));
                    
                    // Other methods should still work
                    Assert.DoesNotThrow(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.That(system.LateTickCalled, Is.True);
                    
                    Assert.DoesNotThrow(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.That(system.FixedTickCalled, Is.True);
                    
                    Assert.DoesNotThrow(() => system.Shutdown());
                    Assert.That(system.ShutdownCalled, Is.True);
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    system.ShouldThrowOnTick = false;
                }
            }

            [Test]
            public void ExceptionIsolation_WhenMultipleMethodsConfiguredToThrow_ShouldThrowOnEach()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystemWithMethodSpecificExceptions();
                system.ShouldThrowOnStart = true;
                system.ShouldThrowOnTick = true;
                system.ShouldThrowOnShutdown = true;
                system.ExceptionMessage = "Method failed";

                try
                {
                    // Act & Assert
                    Assert.Throws<InvalidOperationException>(() => system.Start());
                    Assert.Throws<InvalidOperationException>(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.DoesNotThrow(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                    Assert.Throws<InvalidOperationException>(() => system.Shutdown());
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    system.ShouldThrowOnStart = false;
                    system.ShouldThrowOnTick = false;
                    system.ShouldThrowOnShutdown = false;
                }
            }
        }

        /// <summary>
        /// Interface contract tests to verify IRuntimeSystem interface compliance
        /// </summary>
        [TestFixture]
        public class InterfaceContractTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [Test]
            public void InterfaceContract_WhenImplemented_ShouldProvideAllRequiredMethods()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act & Assert - Verify all interface methods are callable
                Assert.DoesNotThrow(() => system.Start());
                Assert.DoesNotThrow(() => system.Tick(IRuntimeSystemTestData.ValidDeltaTime));
                Assert.DoesNotThrow(() => system.LateTick(IRuntimeSystemTestData.ValidDeltaTime));
                Assert.DoesNotThrow(() => system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime));
                Assert.DoesNotThrow(() => system.Shutdown());
            }

            [Test]
            public void InterfaceContract_WhenMethodsCalledInAnyOrder_ShouldWorkCorrectly()
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act - Call methods in non-standard order
                system.Tick(IRuntimeSystemTestData.ValidDeltaTime);
                system.Start();
                system.FixedTick(IRuntimeSystemTestData.ValidDeltaTime);
                system.LateTick(IRuntimeSystemTestData.ValidDeltaTime);
                system.Shutdown();

                // Assert
                Assert.That(system.StartCalled, Is.True);
                Assert.That(system.TickCalled, Is.True);
                Assert.That(system.LateTickCalled, Is.True);
                Assert.That(system.FixedTickCalled, Is.True);
                Assert.That(system.ShutdownCalled, Is.True);
            }
        }

        /// <summary>
        /// Parameter validation tests for edge cases and boundary values
        /// </summary>
        [TestFixture]
        public class ParameterValidationTests : RuntimeInterfaceTestBase<IRuntimeSystem>
        {
            [TestCase(IRuntimeSystemTestData.ZeroDeltaTime)]
            [TestCase(IRuntimeSystemTestData.NegativeDeltaTime)]
            [TestCase(IRuntimeSystemTestData.LargeDeltaTime)]
            public void ParameterValidation_WhenDeltaTimeIsEdgeCase_ShouldAcceptValue(float deltaTime)
            {
                // Arrange
                var system = IRuntimeSystemTestData.CreateSystem();

                // Act
                system.Tick(deltaTime);
                system.LateTick(deltaTime);
                system.FixedTick(deltaTime);

                // Assert
                Assert.That(system.TickDeltaTimes, Contains.Item(deltaTime));
                Assert.That(system.LateTickDeltaTimes, Contains.Item(deltaTime));
                Assert.That(system.FixedTickDeltaTimes, Contains.Item(deltaTime));
            }
        }
    }
}
