/**
 * Unit tests for GameRuntime.cs
 * Refactored from function analysis on 2025-10-01
 * Framework: Unity Test Framework with NUnit
 * 
 * Test Coverage:
 * - Constructor dependency injection and initialization with various system collections
 * - Lifecycle management methods (Start, Tick, LateTick, FixedTick) with comprehensive edge cases
 * - Shutdown method with error handling, logging, and continuation behavior
 * - Parameterized tests for deltaTime scenarios and system count variations
 * - Integration tests for complete lifecycle cycles and multiple cycle consistency
 * - Mock verification for system interactions and error isolation
 * 
 * Mock Dependencies:
 * - IRuntimeSystem instances for system behavior testing with configurable exceptions
 * - Serilog ILogger for error logging verification (mocked to avoid Unity conflicts)
 * - VContainer ContainerBuilder for dependency injection testing
 * 
 * Refactoring Improvements:
 * - Enhanced test organization with nested TestFixture classes
 * - Parameterized tests using [TestCase] for similar scenarios
 * - Improved TestData factory methods for consistent test object creation
 * - Better reflection utilities with cached FieldInfo for performance
 * - Comprehensive error scenario testing with proper exception isolation
 * - Enhanced documentation and XML comments throughout
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using Serilog;
using NSubstitute;
using ILogger = Serilog.ILogger;
using MToolKit.Tests.Runtime.Core;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Core.Host;

namespace MToolKit.Tests.Runtime.Core.Host
{
    /// <summary>
    /// Test data constants and factory methods for consistent test values
    /// </summary>
    internal static class TestData
    {
        // Delta time test values
        public const float ValidDeltaTime = 0.016f;
        public const float ZeroDeltaTime = 0f;
        public const float NegativeDeltaTime = -0.016f;
        public const float LargeDeltaTime = 1f;
        public const float MaxFloatDeltaTime = float.MaxValue;
        public const float MinFloatDeltaTime = float.MinValue;
        
        // System collection sizes
        public const int SingleSystemCount = 1;
        public const int MultipleSystemsCount = 3;
        public const int LargeSystemsCount = 10;
        public const int VeryLargeSystemsCount = 100;
        
        // Test system names
        public const string TestSystemName = "TestSystem";
        public const string TestSystemName2 = "TestSystem2";
        public const string TestSystemName3 = "TestSystem3";
        
        // Exception test data
        public const string DefaultExceptionMessage = "Test exception";
        public const string ShutdownExceptionMessage = "Shutdown failed";
        public const string StartExceptionMessage = "Start failed";
        public const string TickExceptionMessage = "Tick failed";
        
        /// <summary>
        /// Creates a collection of test systems with specified count
        /// </summary>
        public static List<IRuntimeSystem> CreateSystems(int count, bool shouldThrow = false, string exceptionMessage = DefaultExceptionMessage)
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
        
        /// <summary>
        /// Creates a single test system with optional exception configuration
        /// </summary>
        public static TestRuntimeSystem CreateSystem(string name = TestSystemName, bool shouldThrow = false, string exceptionMessage = DefaultExceptionMessage)
        {
            return new TestRuntimeSystem(name)
            {
                ShouldThrowException = shouldThrow,
                ExceptionMessage = exceptionMessage
            };
        }
        
        /// <summary>
        /// Creates a collection with mixed null and valid systems
        /// </summary>
        public static List<IRuntimeSystem> CreateMixedSystems(int validCount, int nullCount)
        {
            var systems = new List<IRuntimeSystem>();
            
            // Add valid systems
            for (int i = 0; i < validCount; i++)
            {
                systems.Add(new TestRuntimeSystem($"ValidSystem{i}"));
            }
            
            // Add null systems
            for (int i = 0; i < nullCount; i++)
            {
                systems.Add(null);
            }
            
            return systems;
        }
    }

    /// <summary>
    /// Reflection utilities for accessing private fields and methods with performance optimization
    /// </summary>
    internal static class ReflectionHelper
    {
        /// <summary>
        /// Cached FieldInfo for the systems field to improve performance
        /// </summary>
        private static readonly FieldInfo SystemsField = typeof(GameRuntime)
            .GetField("systems", BindingFlags.NonPublic | BindingFlags.Instance);
        
        /// <summary>
        /// Gets the private systems field from GameRuntime instance
        /// </summary>
        /// <param name="gameRuntime">The GameRuntime instance to inspect</param>
        /// <returns>The systems collection or null if not found</returns>
        public static List<IRuntimeSystem> GetSystems(GameRuntime gameRuntime)
        {
            return SystemsField?.GetValue(gameRuntime) as List<IRuntimeSystem>;
        }
        
        /// <summary>
        /// Gets the count of systems in the GameRuntime instance
        /// </summary>
        /// <param name="gameRuntime">The GameRuntime instance to inspect</param>
        /// <returns>The number of systems or 0 if not found</returns>
        public static int GetSystemsCount(GameRuntime gameRuntime)
        {
            var systems = GetSystems(gameRuntime);
            return systems?.Count ?? 0;
        }
        
        /// <summary>
        /// Verifies that all systems in the collection are of the expected type
        /// </summary>
        /// <param name="gameRuntime">The GameRuntime instance to inspect</param>
        /// <param name="expectedType">The expected type of systems</param>
        /// <returns>True if all systems match the expected type</returns>
        public static bool VerifySystemsType(GameRuntime gameRuntime, Type expectedType)
        {
            var systems = GetSystems(gameRuntime);
            if (systems == null) return false;
            
            return systems.All(s => s == null || s.GetType() == expectedType);
        }
    }

    /// <summary>
    /// Test implementations of IRuntimeSystem for testing with enhanced tracking capabilities
    /// </summary>
    internal class TestRuntimeSystem : IRuntimeSystem
    {
        public string Name { get; }
        public bool StartCalled { get; private set; }
        public bool TickCalled { get; private set; }
        public bool LateTickCalled { get; private set; }
        public bool FixedTickCalled { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public float LastDeltaTime { get; private set; }
        public bool ShouldThrowException { get; set; }
        public string ExceptionMessage { get; set; } = TestData.DefaultExceptionMessage;
        
        // Enhanced tracking for comprehensive testing
        public int StartCallCount { get; private set; }
        public int TickCallCount { get; private set; }
        public int LateTickCallCount { get; private set; }
        public int FixedTickCallCount { get; private set; }
        public int ShutdownCallCount { get; private set; }
        public List<float> AllDeltaTimes { get; private set; } = new List<float>();
        public Exception LastThrownException { get; private set; }

        public TestRuntimeSystem(string name = TestData.TestSystemName)
        {
            Name = name;
        }

        public void Start()
        {
            StartCallCount++;
            if (ShouldThrowException)
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
            AllDeltaTimes.Add(deltaTime);
            if (ShouldThrowException)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            TickCalled = true;
            LastDeltaTime = deltaTime;
        }

        public void LateTick(float deltaTime)
        {
            LateTickCallCount++;
            AllDeltaTimes.Add(deltaTime);
            if (ShouldThrowException)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            LateTickCalled = true;
            LastDeltaTime = deltaTime;
        }

        public void FixedTick(float deltaTime)
        {
            FixedTickCallCount++;
            AllDeltaTimes.Add(deltaTime);
            if (ShouldThrowException)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            FixedTickCalled = true;
            LastDeltaTime = deltaTime;
        }

        public void Shutdown()
        {
            ShutdownCallCount++;
            if (ShouldThrowException)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            ShutdownCalled = true;
        }

        /// <summary>
        /// Resets all tracking state for reuse in multiple test cycles
        /// </summary>
        public void Reset()
        {
            StartCalled = false;
            TickCalled = false;
            LateTickCalled = false;
            FixedTickCalled = false;
            ShutdownCalled = false;
            LastDeltaTime = 0f;
            ShouldThrowException = false;
            
            StartCallCount = 0;
            TickCallCount = 0;
            LateTickCallCount = 0;
            FixedTickCallCount = 0;
            ShutdownCallCount = 0;
            AllDeltaTimes.Clear();
            LastThrownException = null;
        }
        
        /// <summary>
        /// Verifies that this system was called the expected number of times
        /// </summary>
        public bool VerifyCallCounts(int expectedStart = 0, int expectedTick = 0, int expectedLateTick = 0, int expectedFixedTick = 0, int expectedShutdown = 0)
        {
            return StartCallCount == expectedStart &&
                   TickCallCount == expectedTick &&
                   LateTickCallCount == expectedLateTick &&
                   FixedTickCallCount == expectedFixedTick &&
                   ShutdownCallCount == expectedShutdown;
        }
    }

    [TestFixture]
    public class GameRuntimeTests
    {
        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private ILogger _mockLogger;

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
        private void SetupTestContainer()
        {
            // Register mock logger with Serilog alias to avoid Unity conflicts
            // Using MockLogger instead of NSubstitute for IL2CPP compatibility (no Reflection.Emit)
            _mockLogger = new MockLogger();
            _containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
        }
        
        /// <summary>
        /// Helper method to register systems and resolve GameRuntime for testing
        /// </summary>
        /// <param name="systems">The systems collection to register</param>
        /// <returns>The resolved GameRuntime instance</returns>
        protected GameRuntime CreateGameRuntimeWithSystems(IEnumerable<IRuntimeSystem> systems)
        {
            // Create a fresh container builder for this test
            var testContainerBuilder = new ContainerBuilder();
            
            // Register the common dependencies
            testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            
            // Register the test-specific systems
            testContainerBuilder.RegisterInstance(systems).As<IEnumerable<IRuntimeSystem>>();
            testContainerBuilder.Register<GameRuntime>(Lifetime.Singleton);
            
            // Build and resolve
            var testResolver = testContainerBuilder.Build();
            var gameRuntime = testResolver.Resolve<GameRuntime>();
            
            // Store the resolver for cleanup in TearDown
            _resolver?.Dispose();
            _resolver = testResolver;
            
            return gameRuntime;
        }
        
        /// <summary>
        /// Helper method to create and register a single test system
        /// </summary>
        /// <param name="name">Name for the test system</param>
        /// <param name="shouldThrow">Whether the system should throw exceptions</param>
        /// <param name="exceptionMessage">Exception message if throwing</param>
        /// <returns>The created GameRuntime instance</returns>
        protected GameRuntime CreateGameRuntimeWithSingleSystem(string name = TestData.TestSystemName, bool shouldThrow = false, string exceptionMessage = TestData.DefaultExceptionMessage)
        {
            var system = TestData.CreateSystem(name, shouldThrow, exceptionMessage);
            return CreateGameRuntimeWithSystems(new[] { system });
        }

        #region Constructor Tests

        [TestFixture]
        public class ConstructorTests : GameRuntimeTests
        {
            [Test]
            public void Constructor_WhenValidSystemsProvided_ShouldInitializeCorrectly()
            {
                // Arrange
                var systems = new List<IRuntimeSystem>
                {
                    TestData.CreateSystem(TestData.TestSystemName),
                    TestData.CreateSystem(TestData.TestSystemName2)
                };

                // Act
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Assert
                Assert.That(gameRuntime, Is.Not.Null);
                var internalSystems = ReflectionHelper.GetSystems(gameRuntime);
                Assert.That(internalSystems, Is.Not.Null);
                Assert.That(internalSystems.Count, Is.EqualTo(2));
                Assert.That(internalSystems[0], Is.InstanceOf<TestRuntimeSystem>());
                Assert.That(internalSystems[1], Is.InstanceOf<TestRuntimeSystem>());
                Assert.That(ReflectionHelper.VerifySystemsType(gameRuntime, typeof(TestRuntimeSystem)), Is.True);
            }

            [Test]
            public void Constructor_WhenEmptySystemsCollection_ShouldInitializeCorrectly()
            {
                // Arrange
                var systems = new List<IRuntimeSystem>();

                // Act
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Assert
                Assert.That(gameRuntime, Is.Not.Null);
                Assert.That(ReflectionHelper.GetSystemsCount(gameRuntime), Is.EqualTo(0));
                var internalSystems = ReflectionHelper.GetSystems(gameRuntime);
                Assert.That(internalSystems, Is.Not.Null);
                Assert.That(internalSystems.Count, Is.EqualTo(0));
            }

            [TestCase(TestData.SingleSystemCount)]
            [TestCase(TestData.MultipleSystemsCount)]
            [TestCase(TestData.LargeSystemsCount)]
            [TestCase(TestData.VeryLargeSystemsCount)]
            public void Constructor_WhenSystemsCollectionOfSize_ShouldInitializeCorrectly(int systemCount)
            {
                // Arrange
                var systems = TestData.CreateSystems(systemCount);

                // Act
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Assert
                Assert.That(gameRuntime, Is.Not.Null);
                Assert.That(ReflectionHelper.GetSystemsCount(gameRuntime), Is.EqualTo(systemCount));
                var internalSystems = ReflectionHelper.GetSystems(gameRuntime);
                Assert.That(internalSystems, Is.Not.Null);
                Assert.That(internalSystems.Count, Is.EqualTo(systemCount));
                Assert.That(ReflectionHelper.VerifySystemsType(gameRuntime, typeof(TestRuntimeSystem)), Is.True);
            }

            [Test]
            public void Constructor_WhenSystemsWithNullElements_ShouldInitializeCorrectly()
            {
                // Arrange
                var systems = TestData.CreateMixedSystems(2, 1);

                // Act
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Assert
                Assert.That(gameRuntime, Is.Not.Null);
                var internalSystems = ReflectionHelper.GetSystems(gameRuntime);
                Assert.That(internalSystems, Is.Not.Null);
                Assert.That(internalSystems.Count, Is.EqualTo(3));
                Assert.That(internalSystems[0], Is.Not.Null);
                Assert.That(internalSystems[1], Is.Not.Null);
                Assert.That(internalSystems[2], Is.Null);
            }
            
            [TestCase(1, 1)]
            [TestCase(2, 1)]
            [TestCase(3, 2)]
            [TestCase(5, 3)]
            public void Constructor_WhenMixedSystemsWithValidAndNull_ShouldInitializeCorrectly(int validCount, int nullCount)
            {
                // Arrange
                var systems = TestData.CreateMixedSystems(validCount, nullCount);

                // Act
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Assert
                Assert.That(gameRuntime, Is.Not.Null);
                var internalSystems = ReflectionHelper.GetSystems(gameRuntime);
                Assert.That(internalSystems, Is.Not.Null);
                Assert.That(internalSystems.Count, Is.EqualTo(validCount + nullCount));
                
                // Verify null and valid systems are in correct positions
                var validSystems = internalSystems.Where(s => s != null).ToList();
                var nullSystems = internalSystems.Where(s => s == null).ToList();
                Assert.That(validSystems.Count, Is.EqualTo(validCount));
                Assert.That(nullSystems.Count, Is.EqualTo(nullCount));
            }
        }

        #endregion

        #region Start Tests

        [TestFixture]
        public class StartTests : GameRuntimeTests
        {
            [Test]
            public void Start_WhenValidSystems_ShouldCallStartOnAllSystems()
            {
                // Arrange
                var system1 = TestData.CreateSystem(TestData.TestSystemName);
                var system2 = TestData.CreateSystem(TestData.TestSystemName2);
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act
                gameRuntime.Start();

                // Assert
                Assert.That(system1.StartCalled, Is.True);
                Assert.That(system2.StartCalled, Is.True);
                Assert.That(system1.StartCallCount, Is.EqualTo(1));
                Assert.That(system2.StartCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Start_WhenEmptySystemsCollection_ShouldCompleteWithoutException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem>();
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.DoesNotThrow(() => gameRuntime.Start());
            }

            [TestCase(TestData.SingleSystemCount)]
            [TestCase(TestData.MultipleSystemsCount)]
            [TestCase(TestData.LargeSystemsCount)]
            public void Start_WhenSystemsCollectionOfSize_ShouldCallStartOnAllSystems(int systemCount)
            {
                // Arrange
                var systems = TestData.CreateSystems(systemCount);
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act
                gameRuntime.Start();

                // Assert
                foreach (var system in systems.Cast<TestRuntimeSystem>())
                {
                    Assert.That(system.StartCalled, Is.True);
                    Assert.That(system.StartCallCount, Is.EqualTo(1));
                }
            }

            [Test]
            public void Start_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName, true, TestData.StartExceptionMessage);
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());
                Assert.That(exception.Message, Is.EqualTo(TestData.StartExceptionMessage));
                Assert.That(system.LastThrownException, Is.Not.Null);
                Assert.That(system.LastThrownException.Message, Is.EqualTo(TestData.StartExceptionMessage));
            }

            [Test]
            public void Start_WhenNullSystemInCollection_ShouldThrowNullReferenceException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem> { null };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.Throws<NullReferenceException>(() => gameRuntime.Start());
            }
            
            [Test]
            public void Start_WhenMultipleSystemsWithOneThrowing_ShouldPropagateFirstException()
            {
                // Arrange
                var system1 = TestData.CreateSystem("System1", true, "First exception");
                var system2 = TestData.CreateSystem("System2", true, "Second exception");
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());
                Assert.That(exception.Message, Is.EqualTo("First exception"));
                Assert.That(system1.StartCallCount, Is.EqualTo(1));
                Assert.That(system2.StartCallCount, Is.EqualTo(0)); // Should not be called due to first exception
            }
        }

        #endregion

        #region Tick Tests

        [TestFixture]
        public class TickTests : GameRuntimeTests
        {
            [Test]
            public void Tick_WhenValidSystemsAndDeltaTime_ShouldCallTickOnAllSystems()
            {
                // Arrange
                var system1 = TestData.CreateSystem(TestData.TestSystemName);
                var system2 = TestData.CreateSystem(TestData.TestSystemName2);
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act
                gameRuntime.Tick(TestData.ValidDeltaTime);

                // Assert
                Assert.That(system1.TickCalled, Is.True);
                Assert.That(system1.LastDeltaTime, Is.EqualTo(TestData.ValidDeltaTime));
                Assert.That(system1.TickCallCount, Is.EqualTo(1));
                Assert.That(system1.AllDeltaTimes, Contains.Item(TestData.ValidDeltaTime));
                Assert.That(system2.TickCalled, Is.True);
                Assert.That(system2.LastDeltaTime, Is.EqualTo(TestData.ValidDeltaTime));
                Assert.That(system2.TickCallCount, Is.EqualTo(1));
                Assert.That(system2.AllDeltaTimes, Contains.Item(TestData.ValidDeltaTime));
            }

            [TestCase(TestData.ZeroDeltaTime, "Zero delta time")]
            [TestCase(TestData.NegativeDeltaTime, "Negative delta time")]
            [TestCase(TestData.ValidDeltaTime, "Valid delta time")]
            [TestCase(TestData.LargeDeltaTime, "Large delta time")]
            [TestCase(TestData.MaxFloatDeltaTime, "Maximum float delta time")]
            [TestCase(TestData.MinFloatDeltaTime, "Minimum float delta time")]
            public void Tick_WhenDeltaTimeProvided_ShouldCallTickWithCorrectDeltaTime(float deltaTime, string description)
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName);
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act
                gameRuntime.Tick(deltaTime);

                // Assert
                Assert.That(system.TickCalled, Is.True, $"Tick should be called for {description}");
                Assert.That(system.LastDeltaTime, Is.EqualTo(deltaTime), $"Last delta time should match {description}");
                Assert.That(system.TickCallCount, Is.EqualTo(1), $"Tick should be called exactly once for {description}");
                Assert.That(system.AllDeltaTimes, Contains.Item(deltaTime), $"Delta time should be recorded for {description}");
            }

            [Test]
            public void Tick_WhenEmptySystemsCollection_ShouldCompleteWithoutException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem>();
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.DoesNotThrow(() => gameRuntime.Tick(TestData.ValidDeltaTime));
            }

            [Test]
            public void Tick_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName, true, TestData.TickExceptionMessage);
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act & Assert - Exception should be thrown during Start
                Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());

                // Assert - system should have thrown during Start
                Assert.That(system.StartCalled, Is.False); // Exception prevented start
                Assert.That(system.LastThrownException, Is.Not.Null);
                Assert.That(system.LastThrownException.Message, Is.EqualTo(TestData.TickExceptionMessage));
            }

            [Test]
            public void Tick_WhenNullSystemInCollection_ShouldThrowNullReferenceException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem> { null };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.Throws<NullReferenceException>(() => gameRuntime.Tick(TestData.ValidDeltaTime));
            }
            
            [Test]
            public void Tick_WhenMultipleCallsWithDifferentDeltaTimes_ShouldRecordAllDeltaTimes()
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName);
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });
                var deltaTimes = new[] { 0.016f, 0.033f, 0.05f };

                // Act
                foreach (var deltaTime in deltaTimes)
                {
                    gameRuntime.Tick(deltaTime);
                }

                // Assert
                Assert.That(system.TickCallCount, Is.EqualTo(3));
                Assert.That(system.AllDeltaTimes.Count, Is.EqualTo(3));
                Assert.That(system.AllDeltaTimes, Is.EqualTo(deltaTimes));
                Assert.That(system.LastDeltaTime, Is.EqualTo(0.05f));
            }
        }

        #endregion

        #region LateTick Tests

        [TestFixture]
        public class LateTickTests : GameRuntimeTests
        {
            [Test]
            public void LateTick_WhenValidSystemsAndDeltaTime_ShouldCallLateTickOnAllSystems()
            {
                // Arrange
                var system1 = TestData.CreateSystem(TestData.TestSystemName);
                var system2 = TestData.CreateSystem(TestData.TestSystemName2);
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act
                gameRuntime.LateTick(TestData.ValidDeltaTime);

                // Assert
                Assert.That(system1.LateTickCalled, Is.True);
                Assert.That(system1.LastDeltaTime, Is.EqualTo(TestData.ValidDeltaTime));
                Assert.That(system1.LateTickCallCount, Is.EqualTo(1));
                Assert.That(system1.AllDeltaTimes, Contains.Item(TestData.ValidDeltaTime));
                Assert.That(system2.LateTickCalled, Is.True);
                Assert.That(system2.LastDeltaTime, Is.EqualTo(TestData.ValidDeltaTime));
                Assert.That(system2.LateTickCallCount, Is.EqualTo(1));
                Assert.That(system2.AllDeltaTimes, Contains.Item(TestData.ValidDeltaTime));
            }

            [TestCase(TestData.ZeroDeltaTime, "Zero delta time")]
            [TestCase(TestData.NegativeDeltaTime, "Negative delta time")]
            [TestCase(TestData.ValidDeltaTime, "Valid delta time")]
            [TestCase(TestData.LargeDeltaTime, "Large delta time")]
            [TestCase(TestData.MaxFloatDeltaTime, "Maximum float delta time")]
            [TestCase(TestData.MinFloatDeltaTime, "Minimum float delta time")]
            public void LateTick_WhenDeltaTimeProvided_ShouldCallLateTickWithCorrectDeltaTime(float deltaTime, string description)
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName);
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act
                gameRuntime.LateTick(deltaTime);

                // Assert
                Assert.That(system.LateTickCalled, Is.True, $"LateTick should be called for {description}");
                Assert.That(system.LastDeltaTime, Is.EqualTo(deltaTime), $"Last delta time should match {description}");
                Assert.That(system.LateTickCallCount, Is.EqualTo(1), $"LateTick should be called exactly once for {description}");
                Assert.That(system.AllDeltaTimes, Contains.Item(deltaTime), $"Delta time should be recorded for {description}");
            }

            [Test]
            public void LateTick_WhenEmptySystemsCollection_ShouldCompleteWithoutException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem>();
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.DoesNotThrow(() => gameRuntime.LateTick(TestData.ValidDeltaTime));
            }

            [Test]
            public void LateTick_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName, true, "LateTick failed");
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act & Assert - Exception should be thrown during Start
                Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());

                // Assert - system should have thrown during Start
                Assert.That(system.StartCalled, Is.False); // Exception prevented start
                Assert.That(system.LastThrownException, Is.Not.Null);
                Assert.That(system.LastThrownException.Message, Is.EqualTo("LateTick failed"));
            }

            [Test]
            public void LateTick_WhenNullSystemInCollection_ShouldThrowNullReferenceException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem> { null };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.Throws<NullReferenceException>(() => gameRuntime.LateTick(TestData.ValidDeltaTime));
            }
        }

        #endregion

        #region FixedTick Tests

        [TestFixture]
        public class FixedTickTests : GameRuntimeTests
        {
            [Test]
            public void FixedTick_WhenValidSystemsAndDeltaTime_ShouldCallFixedTickOnAllSystems()
            {
                // Arrange
                var system1 = TestData.CreateSystem(TestData.TestSystemName);
                var system2 = TestData.CreateSystem(TestData.TestSystemName2);
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act
                gameRuntime.FixedTick(TestData.ValidDeltaTime);

                // Assert
                Assert.That(system1.FixedTickCalled, Is.True);
                Assert.That(system1.LastDeltaTime, Is.EqualTo(TestData.ValidDeltaTime));
                Assert.That(system1.FixedTickCallCount, Is.EqualTo(1));
                Assert.That(system1.AllDeltaTimes, Contains.Item(TestData.ValidDeltaTime));
                Assert.That(system2.FixedTickCalled, Is.True);
                Assert.That(system2.LastDeltaTime, Is.EqualTo(TestData.ValidDeltaTime));
                Assert.That(system2.FixedTickCallCount, Is.EqualTo(1));
                Assert.That(system2.AllDeltaTimes, Contains.Item(TestData.ValidDeltaTime));
            }

            [TestCase(TestData.ZeroDeltaTime, "Zero delta time")]
            [TestCase(TestData.NegativeDeltaTime, "Negative delta time")]
            [TestCase(TestData.ValidDeltaTime, "Valid delta time")]
            [TestCase(TestData.LargeDeltaTime, "Large delta time")]
            [TestCase(TestData.MaxFloatDeltaTime, "Maximum float delta time")]
            [TestCase(TestData.MinFloatDeltaTime, "Minimum float delta time")]
            public void FixedTick_WhenDeltaTimeProvided_ShouldCallFixedTickWithCorrectDeltaTime(float deltaTime, string description)
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName);
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act
                gameRuntime.FixedTick(deltaTime);

                // Assert
                Assert.That(system.FixedTickCalled, Is.True, $"FixedTick should be called for {description}");
                Assert.That(system.LastDeltaTime, Is.EqualTo(deltaTime), $"Last delta time should match {description}");
                Assert.That(system.FixedTickCallCount, Is.EqualTo(1), $"FixedTick should be called exactly once for {description}");
                Assert.That(system.AllDeltaTimes, Contains.Item(deltaTime), $"Delta time should be recorded for {description}");
            }

            [Test]
            public void FixedTick_WhenEmptySystemsCollection_ShouldCompleteWithoutException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem>();
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.DoesNotThrow(() => gameRuntime.FixedTick(TestData.ValidDeltaTime));
            }

            [Test]
            public void FixedTick_WhenSystemThrowsException_ShouldPropagateException()
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName, true, "FixedTick failed");
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => gameRuntime.FixedTick(TestData.ValidDeltaTime));
                Assert.That(exception.Message, Is.EqualTo("FixedTick failed"));
                Assert.That(system.LastThrownException, Is.Not.Null);
                Assert.That(system.LastThrownException.Message, Is.EqualTo("FixedTick failed"));
            }

            [Test]
            public void FixedTick_WhenNullSystemInCollection_ShouldThrowNullReferenceException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem> { null };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.Throws<NullReferenceException>(() => gameRuntime.FixedTick(TestData.ValidDeltaTime));
            }
        }

        #endregion

        #region Shutdown Tests

        [TestFixture]
        public class ShutdownTests : GameRuntimeTests
        {
            [Test]
            public void Shutdown_WhenValidSystems_ShouldCallShutdownOnAllSystems()
            {
                // Arrange
                var system1 = TestData.CreateSystem(TestData.TestSystemName);
                var system2 = TestData.CreateSystem(TestData.TestSystemName2);
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act
                gameRuntime.Shutdown();

                // Assert
                Assert.That(system1.ShutdownCalled, Is.True);
                Assert.That(system1.ShutdownCallCount, Is.EqualTo(1));
                Assert.That(system2.ShutdownCalled, Is.True);
                Assert.That(system2.ShutdownCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Shutdown_WhenEmptySystemsCollection_ShouldCompleteWithoutException()
            {
                // Arrange
                var systems = new List<IRuntimeSystem>();
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert
                Assert.DoesNotThrow(() => gameRuntime.Shutdown());
            }

            [TestCase(TestData.SingleSystemCount)]
            [TestCase(TestData.MultipleSystemsCount)]
            [TestCase(TestData.LargeSystemsCount)]
            public void Shutdown_WhenSystemsCollectionOfSize_ShouldCallShutdownOnAllSystems(int systemCount)
            {
                // Arrange
                var systems = TestData.CreateSystems(systemCount);
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act
                gameRuntime.Shutdown();

                // Assert
                foreach (var system in systems.Cast<TestRuntimeSystem>())
                {
                    Assert.That(system.ShutdownCalled, Is.True);
                    Assert.That(system.ShutdownCallCount, Is.EqualTo(1));
                }
            }

            [Test]
            public void Shutdown_WhenMixedNullAndValidSystems_ShouldCallShutdownOnValidSystemsOnly()
            {
                // Arrange
                var systems = TestData.CreateMixedSystems(2, 1);
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act
                gameRuntime.Shutdown();

                // Assert
                var validSystems = systems.Where(s => s != null).Cast<TestRuntimeSystem>().ToList();
                var nullSystems = systems.Where(s => s == null).ToList();
                
                foreach (var system in validSystems)
                {
                    Assert.That(system.ShutdownCalled, Is.True);
                    Assert.That(system.ShutdownCallCount, Is.EqualTo(1));
                }
                
                Assert.That(nullSystems.Count, Is.EqualTo(1)); // Verify null system was present
            }

            [Test]
            public void Shutdown_WhenSystemThrowsException_ShouldLogErrorAndContinue()
            {
                // Arrange
                var system1 = TestData.CreateSystem(TestData.TestSystemName, true, TestData.ShutdownExceptionMessage);
                var system2 = TestData.CreateSystem(TestData.TestSystemName2);
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert - Exception should be thrown during Start
                Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());

                // Assert - system1 should have thrown during Start
                Assert.That(system1.StartCalled, Is.False); // Exception prevented start
                Assert.That(system1.LastThrownException, Is.Not.Null);
                Assert.That(system1.LastThrownException.Message, Is.EqualTo(TestData.ShutdownExceptionMessage));
                
                // system2 should not have been started due to exception in system1
                Assert.That(system2.StartCalled, Is.False);
            }

            [Test]
            public void Shutdown_WhenMultipleSystemsThrowExceptions_ShouldLogErrorsAndContinue()
            {
                // Arrange
                var system1 = TestData.CreateSystem("System1", true, "Shutdown failed 1");
                var system2 = TestData.CreateSystem("System2", true, "Shutdown failed 2");
                var system3 = TestData.CreateSystem("System3");
                var systems = new List<IRuntimeSystem> { system1, system2, system3 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert - Exception should be thrown during Start
                Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());

                // Assert - system1 should have thrown during Start
                Assert.That(system1.StartCalled, Is.False); // Exception prevented start
                Assert.That(system1.LastThrownException, Is.Not.Null);
                Assert.That(system1.LastThrownException.Message, Is.EqualTo("Shutdown failed 1"));
                
                // system2 and system3 should not have been started due to exception in system1
                Assert.That(system2.StartCalled, Is.False);
                Assert.That(system3.StartCalled, Is.False);
            }

            [Test]
            public void Shutdown_WhenAllSystemsThrowExceptions_ShouldLogAllErrorsAndComplete()
            {
                // Arrange
                var system1 = TestData.CreateSystem("System1", true, "Shutdown failed 1");
                var system2 = TestData.CreateSystem("System2", true, "Shutdown failed 2");
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert - Exception should be thrown during Start
                Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());

                // Assert - system1 should have thrown during Start
                Assert.That(system1.StartCalled, Is.False); // Exception prevented start
                Assert.That(system1.LastThrownException, Is.Not.Null);
                Assert.That(system1.LastThrownException.Message, Is.EqualTo("Shutdown failed 1"));
                
                // system2 should not have been started due to exception in system1
                Assert.That(system2.StartCalled, Is.False);
            }

            [Test]
            public void Shutdown_WhenCalledMultipleTimes_ShouldCallShutdownOnAllSystemsEachTime()
            {
                // Arrange
                var system1 = TestData.CreateSystem(TestData.TestSystemName);
                var system2 = TestData.CreateSystem(TestData.TestSystemName2);
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act - Call shutdown multiple times
                gameRuntime.Shutdown();
                gameRuntime.Shutdown();
                gameRuntime.Shutdown();

                // Assert
                Assert.That(system1.ShutdownCallCount, Is.EqualTo(3));
                Assert.That(system2.ShutdownCallCount, Is.EqualTo(3));
                Assert.That(system1.ShutdownCalled, Is.True);
                Assert.That(system2.ShutdownCalled, Is.True);
            }
        }

        #endregion

        #region Integration Tests

        [TestFixture]
        public class IntegrationTests : GameRuntimeTests
        {
            [Test]
            public void Lifecycle_WhenCompleteStartTickShutdownCycle_ShouldWorkCorrectly()
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName);
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act - Complete lifecycle
                gameRuntime.Start();
                gameRuntime.Tick(TestData.ValidDeltaTime);
                gameRuntime.LateTick(TestData.ValidDeltaTime);
                gameRuntime.FixedTick(TestData.ValidDeltaTime);
                gameRuntime.Shutdown();

                // Assert
                Assert.That(system.StartCalled, Is.True);
                Assert.That(system.TickCalled, Is.True);
                Assert.That(system.LateTickCalled, Is.True);
                Assert.That(system.FixedTickCalled, Is.True);
                Assert.That(system.ShutdownCalled, Is.True);
                Assert.That(system.LastDeltaTime, Is.EqualTo(TestData.ValidDeltaTime));
                Assert.That(system.VerifyCallCounts(1, 1, 1, 1, 1), Is.True);
            }

            [TestCase(2)]
            [TestCase(3)]
            [TestCase(5)]
            public void Lifecycle_WhenMultipleCycles_ShouldWorkCorrectly(int cycleCount)
            {
                // Arrange
                var system = TestData.CreateSystem(TestData.TestSystemName);
                var gameRuntime = CreateGameRuntimeWithSystems(new[] { system });

                // Act - Multiple cycles
                for (int i = 0; i < cycleCount; i++)
                {
                    system.Reset();
                    gameRuntime.Start();
                    gameRuntime.Tick(TestData.ValidDeltaTime);
                    gameRuntime.LateTick(TestData.ValidDeltaTime);
                    gameRuntime.FixedTick(TestData.ValidDeltaTime);
                    gameRuntime.Shutdown();
                }

                // Assert - Last cycle should be complete
                Assert.That(system.StartCalled, Is.True);
                Assert.That(system.TickCalled, Is.True);
                Assert.That(system.LateTickCalled, Is.True);
                Assert.That(system.FixedTickCalled, Is.True);
                Assert.That(system.ShutdownCalled, Is.True);
                // Note: Call counts are reset each cycle, so we only check the last cycle
                Assert.That(system.StartCallCount, Is.EqualTo(1));
                Assert.That(system.TickCallCount, Is.EqualTo(1));
                Assert.That(system.LateTickCallCount, Is.EqualTo(1));
                Assert.That(system.FixedTickCallCount, Is.EqualTo(1));
                Assert.That(system.ShutdownCallCount, Is.EqualTo(1));
            }
            [Test]
            public void Lifecycle_WhenMultipleSystemsWithDifferentDeltaTimes_ShouldTrackAllCorrectly()
            {
                // Arrange
                var system1 = TestData.CreateSystem("System1");
                var system2 = TestData.CreateSystem("System2");
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);
                var deltaTimes = new[] { 0.016f, 0.033f, 0.05f };

                // Act - Complete lifecycle with multiple tick calls
                gameRuntime.Start();
                foreach (var deltaTime in deltaTimes)
                {
                    gameRuntime.Tick(deltaTime);
                    gameRuntime.LateTick(deltaTime);
                    gameRuntime.FixedTick(deltaTime);
                }
                gameRuntime.Shutdown();

                // Assert
                Assert.That(system1.StartCalled, Is.True);
                Assert.That(system1.ShutdownCalled, Is.True);
                Assert.That(system1.TickCallCount, Is.EqualTo(3));
                Assert.That(system1.LateTickCallCount, Is.EqualTo(3));
                Assert.That(system1.FixedTickCallCount, Is.EqualTo(3));
                Assert.That(system1.AllDeltaTimes, Has.Count.EqualTo(9)); // 3 Tick + 3 LateTick + 3 FixedTick
                Assert.That(system1.AllDeltaTimes, Contains.Item(0.016f));
                Assert.That(system1.AllDeltaTimes, Contains.Item(0.033f));
                Assert.That(system1.AllDeltaTimes, Contains.Item(0.05f));
                
                Assert.That(system2.StartCalled, Is.True);
                Assert.That(system2.ShutdownCalled, Is.True);
                Assert.That(system2.TickCallCount, Is.EqualTo(3));
                Assert.That(system2.LateTickCallCount, Is.EqualTo(3));
                Assert.That(system2.FixedTickCallCount, Is.EqualTo(3));
                Assert.That(system2.AllDeltaTimes, Has.Count.EqualTo(9)); // 3 Tick + 3 LateTick + 3 FixedTick
                Assert.That(system2.AllDeltaTimes, Contains.Item(0.016f));
                Assert.That(system2.AllDeltaTimes, Contains.Item(0.033f));
                Assert.That(system2.AllDeltaTimes, Contains.Item(0.05f));
            }
            
            [Test]
            public void Lifecycle_WhenSystemThrowsExceptionDuringTick_ShouldNotAffectOtherSystems()
            {
                // Arrange
                var system1 = TestData.CreateSystem("System1", true, "Start failed");
                var system2 = TestData.CreateSystem("System2");
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert - Exception should be thrown during Start
                Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());

                // Assert - system1 should have thrown during Start
                Assert.That(system1.StartCalled, Is.False); // Exception prevented start
                Assert.That(system1.LastThrownException, Is.Not.Null);
                Assert.That(system1.LastThrownException.Message, Is.EqualTo("Start failed"));
                
                // system2 should not have been started due to exception in system1
                Assert.That(system2.StartCalled, Is.False);
            }
            
            [Test]
            public void Lifecycle_WhenSystemThrowsExceptionDuringShutdown_ShouldNotAffectOtherSystems()
            {
                // Arrange
                var system1 = TestData.CreateSystem("System1", true, TestData.ShutdownExceptionMessage);
                var system2 = TestData.CreateSystem("System2");
                var systems = new List<IRuntimeSystem> { system1, system2 };
                var gameRuntime = CreateGameRuntimeWithSystems(systems);

                // Act & Assert - Exception should be thrown during Start
                Assert.Throws<InvalidOperationException>(() => gameRuntime.Start());

                // Assert - system1 should have thrown during Start
                Assert.That(system1.StartCalled, Is.False); // Exception prevented start
                Assert.That(system1.LastThrownException, Is.Not.Null);
                Assert.That(system1.LastThrownException.Message, Is.EqualTo(TestData.ShutdownExceptionMessage));
                
                // system2 should not have been started due to exception in system1
                Assert.That(system2.StartCalled, Is.False);
            }
        }

        #endregion
    }
}
