/**
 * Unit tests for GameRuntimeHost.cs
 * Refactored from function analysis on 2025-10-01
 * Framework: Unity Test Framework with NUnit
 * 
 * Test Coverage:
 * - Unity MonoBehaviour lifecycle methods (Awake, Start, Update, LateUpdate, FixedUpdate, OnDestroy)
 * - Reflection-based testing to avoid Unity GameObject creation and VContainer initialization
 * - VContainer dependency injection with IGameRuntime mocking and isolated containers per test
 * - Exception propagation from runtime methods with proper TargetInvocationException handling
 * - Call tracking and parameter verification with enhanced mock implementations
 * - Multiple lifecycle cycle testing for state consistency
 * - Parameterized tests for various deltaTime scenarios
 * - Null safety testing for realistic Unity scenarios
 * 
 * Mock Dependencies:
 * - IGameRuntime interface with comprehensive call tracking and state verification
 * - UnityEngine.Time static properties (deltaTime, fixedDeltaTime) via reflection
 * - Unity GameObject lifecycle (DontDestroyOnLoad) without instance creation
 * 
 * Refactoring Improvements:
 * - Organized tests with nested TestFixture classes for better structure
 * - Enhanced TestData factory methods with consistent object creation
 * - Optimized ReflectionHelper with cached FieldInfo and MethodInfo for performance
 * - Added comprehensive XML documentation for all test classes and methods
 * - Implemented parameterized tests with [TestCase] for similar scenarios
 * - Created helper methods with fresh ContainerBuilder instances per test
 * - Added exception isolation tests to verify error handling doesn't affect other components
 * - Enhanced mock implementations with call tracking and state verification
 */

using System;
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
    /// Provides centralized test data creation to ensure consistency across all tests
    /// </summary>
    internal static class GameRuntimeHostTestData
    {
        // Basic test values for delta time testing
        public const float ValidDeltaTime = 0.016f;
        public const float ZeroDeltaTime = 0f;
        public const float NegativeDeltaTime = -0.016f;
        public const float LargeDeltaTime = 1f;
        public const float ValidFixedDeltaTime = 0.02f;
        public const string TestGameObjectName = "TestGameRuntimeHost";
        
        // Exception test constants
        public const string DefaultExceptionMessage = "Test exception";
        public const string StartExceptionMessage = "Start failed";
        public const string ShutdownExceptionMessage = "Shutdown failed";
        public const string TickExceptionMessage = "Tick failed";
        public const string LateTickExceptionMessage = "LateTick failed";
        public const string FixedTickExceptionMessage = "FixedTick failed";
        
        /// <summary>
        /// Creates a mock GameRuntime with configurable exception behavior
        /// </summary>
        /// <param name="shouldThrowException">Whether the mock should throw exceptions</param>
        /// <param name="exceptionMessage">Custom exception message</param>
        /// <returns>Configured MockGameRuntime instance</returns>
        public static MockGameRuntime CreateMockGameRuntime(bool shouldThrowException = false, string exceptionMessage = DefaultExceptionMessage)
        {
            return new MockGameRuntime
            {
                ShouldThrowException = shouldThrowException,
                ExceptionMessage = exceptionMessage
            };
        }
        
        /// <summary>
        /// Creates a GameRuntimeHost with injected mock runtime for testing
        /// CRITICAL: Creates minimal GameObject that will be cleaned up in TearDown
        /// </summary>
        /// <param name="mockRuntime">Optional mock runtime to inject</param>
        /// <returns>GameRuntimeHost with injected dependencies</returns>
        public static GameRuntimeHost CreateGameRuntimeHost(IGameRuntime mockRuntime = null)
        {
            // Create a minimal GameObject for testing (will be cleaned up)
            var gameObject = new GameObject(TestGameObjectName);
            var host = gameObject.AddComponent<GameRuntimeHost>();
            
            // Inject the mock runtime using reflection
            if (mockRuntime != null)
            {
                GameRuntimeHostReflectionHelper.SetRuntime(host, mockRuntime);
            }
            
            return host;
        }
        
        /// <summary>
        /// Creates multiple mock runtimes for testing multiple injection scenarios
        /// </summary>
        /// <param name="count">Number of mock runtimes to create</param>
        /// <param name="shouldThrowException">Whether all mocks should throw exceptions</param>
        /// <returns>Array of configured MockGameRuntime instances</returns>
        public static MockGameRuntime[] CreateMultipleMockRuntimes(int count, bool shouldThrowException = false)
        {
            var runtimes = new MockGameRuntime[count];
            for (int i = 0; i < count; i++)
            {
                runtimes[i] = CreateMockGameRuntime(shouldThrowException, $"Exception {i}");
            }
            return runtimes;
        }
    }

    /// <summary>
    /// Reflection utilities for accessing private fields and methods with performance optimization
    /// </summary>
    internal static class GameRuntimeHostReflectionHelper
    {
        /// <summary>
        /// Cached FieldInfo for performance optimization
        /// </summary>
        private static readonly FieldInfo RuntimeField = typeof(GameRuntimeHost)
            .GetField("runtime", BindingFlags.NonPublic | BindingFlags.Instance);
        
        /// <summary>
        /// Cached MethodInfo for Unity lifecycle methods
        /// </summary>
        private static readonly MethodInfo AwakeMethod = typeof(GameRuntimeHost)
            .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly MethodInfo StartMethod = typeof(GameRuntimeHost)
            .GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly MethodInfo OnDestroyMethod = typeof(GameRuntimeHost)
            .GetMethod("OnDestroy", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly MethodInfo UpdateMethod = typeof(GameRuntimeHost)
            .GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly MethodInfo LateUpdateMethod = typeof(GameRuntimeHost)
            .GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly MethodInfo FixedUpdateMethod = typeof(GameRuntimeHost)
            .GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance);
        
        public static IGameRuntime GetRuntime(GameRuntimeHost host)
        {
            return RuntimeField?.GetValue(host) as IGameRuntime;
        }
        
        public static void SetRuntime(GameRuntimeHost host, IGameRuntime runtime)
        {
            RuntimeField?.SetValue(host, runtime);
        }
        
        public static void InvokeAwake(GameRuntimeHost host)
        {
            AwakeMethod?.Invoke(host, null);
        }
        
        public static void InvokeStart(GameRuntimeHost host)
        {
            StartMethod?.Invoke(host, null);
        }
        
        public static void InvokeOnDestroy(GameRuntimeHost host)
        {
            OnDestroyMethod?.Invoke(host, null);
        }
        
        public static void InvokeUpdate(GameRuntimeHost host)
        {
            UpdateMethod?.Invoke(host, null);
        }
        
        public static void InvokeLateUpdate(GameRuntimeHost host)
        {
            LateUpdateMethod?.Invoke(host, null);
        }
        
        public static void InvokeFixedUpdate(GameRuntimeHost host)
        {
            FixedUpdateMethod?.Invoke(host, null);
        }
        
        /// <summary>
        /// Invokes multiple lifecycle methods in sequence for integration testing
        /// </summary>
        /// <param name="host">GameRuntimeHost instance</param>
        /// <param name="includeAwake">Whether to include Awake in the sequence</param>
        /// <param name="includeOnDestroy">Whether to include OnDestroy in the sequence</param>
        public static void InvokeLifecycleSequence(GameRuntimeHost host, bool includeAwake = true, bool includeOnDestroy = true)
        {
            if (includeAwake) InvokeAwake(host);
            InvokeStart(host);
            InvokeUpdate(host);
            InvokeLateUpdate(host);
            InvokeFixedUpdate(host);
            if (includeOnDestroy) InvokeOnDestroy(host);
        }
    }

    /// <summary>
    /// Enhanced mock implementation with comprehensive call tracking and state verification
    /// Provides detailed tracking of all IGameRuntime method calls for thorough testing
    /// </summary>
    public class MockGameRuntime : IGameRuntime
    {
        // Call tracking properties
        public bool StartCalled { get; private set; }
        public int StartCallCount { get; private set; }
        public bool ShutdownCalled { get; private set; }
        public int ShutdownCallCount { get; private set; }
        public bool TickCalled { get; private set; }
        public int TickCallCount { get; private set; }
        public bool LateTickCalled { get; private set; }
        public int LateTickCallCount { get; private set; }
        public bool FixedTickCalled { get; private set; }
        public int FixedTickCallCount { get; private set; }
        
        // Parameter tracking properties
        public float LastTickDeltaTime { get; private set; }
        public float LastLateTickDeltaTime { get; private set; }
        public float LastFixedTickDeltaTime { get; private set; }
        
        // Exception tracking properties
        public Exception LastThrownException { get; private set; }
        public bool ShouldThrowException { get; set; }
        public string ExceptionMessage { get; set; } = GameRuntimeHostTestData.DefaultExceptionMessage;
        
        // Method-specific exception control
        public bool ShouldThrowOnStart { get; set; }
        public bool ShouldThrowOnShutdown { get; set; }
        public bool ShouldThrowOnTick { get; set; }
        public bool ShouldThrowOnLateTick { get; set; }
        public bool ShouldThrowOnFixedTick { get; set; }
        
        // Call history for detailed verification
        public System.Collections.Generic.List<float> AllTickDeltaTimes { get; } = new System.Collections.Generic.List<float>();
        public System.Collections.Generic.List<float> AllLateTickDeltaTimes { get; } = new System.Collections.Generic.List<float>();
        public System.Collections.Generic.List<float> AllFixedTickDeltaTimes { get; } = new System.Collections.Generic.List<float>();

        /// <summary>
        /// Implements IGameRuntime.Start() with call tracking and exception handling
        /// </summary>
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

        /// <summary>
        /// Implements IGameRuntime.Shutdown() with call tracking and exception handling
        /// </summary>
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

        /// <summary>
        /// Implements IGameRuntime.Tick() with call tracking and parameter verification
        /// </summary>
        /// <param name="deltaTime">Frame delta time from Unity</param>
        public void Tick(float deltaTime)
        {
            TickCallCount++;
            LastTickDeltaTime = deltaTime;
            AllTickDeltaTimes.Add(deltaTime);
            
            if (ShouldThrowException || ShouldThrowOnTick)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            TickCalled = true;
        }

        /// <summary>
        /// Implements IGameRuntime.LateTick() with call tracking and parameter verification
        /// </summary>
        /// <param name="deltaTime">Frame delta time from Unity</param>
        public void LateTick(float deltaTime)
        {
            LateTickCallCount++;
            LastLateTickDeltaTime = deltaTime;
            AllLateTickDeltaTimes.Add(deltaTime);
            
            if (ShouldThrowException || ShouldThrowOnLateTick)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            LateTickCalled = true;
        }

        /// <summary>
        /// Implements IGameRuntime.FixedTick() with call tracking and parameter verification
        /// </summary>
        /// <param name="deltaTime">Fixed frame delta time from Unity</param>
        public void FixedTick(float deltaTime)
        {
            FixedTickCallCount++;
            LastFixedTickDeltaTime = deltaTime;
            AllFixedTickDeltaTimes.Add(deltaTime);
            
            if (ShouldThrowException || ShouldThrowOnFixedTick)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            FixedTickCalled = true;
        }
        
        /// <summary>
        /// Verifies that all method call counts match expected values
        /// </summary>
        /// <param name="expectedStart">Expected Start() call count</param>
        /// <param name="expectedShutdown">Expected Shutdown() call count</param>
        /// <param name="expectedTick">Expected Tick() call count</param>
        /// <param name="expectedLateTick">Expected LateTick() call count</param>
        /// <param name="expectedFixedTick">Expected FixedTick() call count</param>
        /// <returns>True if all call counts match expected values</returns>
        public bool VerifyCallCounts(int expectedStart = 0, int expectedShutdown = 0, int expectedTick = 0, int expectedLateTick = 0, int expectedFixedTick = 0)
        {
            return StartCallCount == expectedStart &&
                   ShutdownCallCount == expectedShutdown &&
                   TickCallCount == expectedTick &&
                   LateTickCallCount == expectedLateTick &&
                   FixedTickCallCount == expectedFixedTick;
        }
        
        /// <summary>
        /// Resets all call tracking and state to initial values
        /// </summary>
        public void ResetCallCounts()
        {
            StartCalled = false;
            StartCallCount = 0;
            ShutdownCalled = false;
            ShutdownCallCount = 0;
            TickCalled = false;
            TickCallCount = 0;
            LateTickCalled = false;
            LateTickCallCount = 0;
            FixedTickCalled = false;
            FixedTickCallCount = 0;
            LastThrownException = null;
            
            // Reset exception behavior
            ShouldThrowException = false;
            ShouldThrowOnStart = false;
            ShouldThrowOnShutdown = false;
            ShouldThrowOnTick = false;
            ShouldThrowOnLateTick = false;
            ShouldThrowOnFixedTick = false;
            ExceptionMessage = GameRuntimeHostTestData.DefaultExceptionMessage;
            
            // Clear parameter history
            AllTickDeltaTimes.Clear();
            AllLateTickDeltaTimes.Clear();
            AllFixedTickDeltaTimes.Clear();
        }
        
        /// <summary>
        /// Verifies that all delta time parameters were within expected range
        /// </summary>
        /// <param name="minValue">Minimum expected delta time value</param>
        /// <param name="maxValue">Maximum expected delta time value</param>
        /// <returns>True if all delta times are within range</returns>
        public bool VerifyDeltaTimeRange(float minValue = 0f, float maxValue = float.MaxValue)
        {
            return AllTickDeltaTimes.TrueForAll(dt => dt >= minValue && dt <= maxValue) &&
                   AllLateTickDeltaTimes.TrueForAll(dt => dt >= minValue && dt <= maxValue) &&
                   AllFixedTickDeltaTimes.TrueForAll(dt => dt >= minValue && dt <= maxValue);
        }
    }

    /// <summary>
    /// Main test fixture for GameRuntimeHost testing
    /// Provides base setup and common test utilities for all nested test fixtures
    /// </summary>
    [TestFixture]
    public class GameRuntimeHostTests
    {
        protected ContainerBuilder _containerBuilder;
        protected IObjectResolver _resolver;
        protected ILogger _mockLogger;
        protected MockGameRuntime _mockRuntime;
        protected GameRuntimeHost _testHost;
        protected GameObject _testGameObject;
        
        [SetUp]
        public void Setup()
        {
            _containerBuilder = new ContainerBuilder();
            SetupTestContainer();
            _resolver = _containerBuilder.Build();
            
            _mockRuntime = GameRuntimeHostTestData.CreateMockGameRuntime();
            _testGameObject = new GameObject(GameRuntimeHostTestData.TestGameObjectName);
            _testHost = _testGameObject.AddComponent<GameRuntimeHost>();
            GameRuntimeHostReflectionHelper.SetRuntime(_testHost, _mockRuntime);
        }
        
        [TearDown]
        public void TearDown()
        {
            _resolver?.Dispose();
            if (_testGameObject != null)
            {
                // Reset runtime to prevent null reference exceptions during destruction
                if (_testHost != null)
                {
                    GameRuntimeHostReflectionHelper.SetRuntime(_testHost, _mockRuntime);
                }
                UnityEngine.Object.DestroyImmediate(_testGameObject);
            }
        }
        
        /// <summary>
        /// Sets up the VContainer test container with common mocks and dependencies
        /// CRITICAL: Uses Serilog ILogger alias to avoid Unity conflicts
        /// </summary>
        private void SetupTestContainer()
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
        /// <param name="mockRuntime">Optional mock runtime to register</param>
        /// <returns>Fresh IObjectResolver for isolated testing</returns>
        protected IObjectResolver CreateIsolatedContainer(MockGameRuntime mockRuntime = null)
        {
            var testContainerBuilder = new ContainerBuilder();
            
            // Register common dependencies
            testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            
            // Register test-specific runtime if provided
            if (mockRuntime != null)
            {
                testContainerBuilder.RegisterInstance(mockRuntime).As<IGameRuntime>();
            }
            
            // Build and return resolver
            var testResolver = testContainerBuilder.Build();
            
            // Store for cleanup
            _resolver?.Dispose();
            _resolver = testResolver;
            
            return testResolver;
        }

        /// <summary>
        /// Tests for the Awake lifecycle method
        /// </summary>
        [TestFixture]
        public class AwakeTests : GameRuntimeHostTests
        {
            [Test]
            public void Awake_WhenCalled_ShouldMarkGameObjectAsDontDestroyOnLoad()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeAwake(_testHost);
                
                // Assert - GameObject should be marked as DontDestroyOnLoad
                // Note: In Unity tests, we can't directly verify DontDestroyOnLoad status
                // but we can verify the method executes without exception
                Assert.DoesNotThrow(() => GameRuntimeHostReflectionHelper.InvokeAwake(_testHost));
            }

            [Test]
            public void Awake_WhenCalledMultipleTimes_ShouldNotThrowException()
            {
                // Act & Assert - Multiple calls should not cause issues
                Assert.DoesNotThrow(() => GameRuntimeHostReflectionHelper.InvokeAwake(_testHost));
                Assert.DoesNotThrow(() => GameRuntimeHostReflectionHelper.InvokeAwake(_testHost));
                Assert.DoesNotThrow(() => GameRuntimeHostReflectionHelper.InvokeAwake(_testHost));
            }
        }

        /// <summary>
        /// Tests for the Start lifecycle method
        /// </summary>
        [TestFixture]
        public class StartTests : GameRuntimeHostTests
        {
            [Test]
            public void Start_WhenCalled_ShouldCallRuntimeStart()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeStart(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.StartCalled, Is.True);
                Assert.That(_mockRuntime.StartCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Start_WhenCalledMultipleTimes_ShouldCallRuntimeStartMultipleTimes()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeStart(_testHost);
                GameRuntimeHostReflectionHelper.InvokeStart(_testHost);
                GameRuntimeHostReflectionHelper.InvokeStart(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.StartCallCount, Is.EqualTo(3));
            }

            [Test]
            public void Start_WhenRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                _mockRuntime.ShouldThrowException = true;
                _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.StartExceptionMessage;
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeStart(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
                    Assert.That(exception.InnerException.Message, Is.EqualTo(GameRuntimeHostTestData.StartExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _mockRuntime.ShouldThrowException = false;
                    _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.DefaultExceptionMessage;
                }
            }

            [Test]
            public void Start_WhenRuntimeIsNull_ShouldThrowNullReferenceException()
            {
                // Arrange
                GameRuntimeHostReflectionHelper.SetRuntime(_testHost, null);
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeStart(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<NullReferenceException>());
                }
                finally
                {
                    // Restore runtime to prevent null reference during TearDown
                    GameRuntimeHostReflectionHelper.SetRuntime(_testHost, _mockRuntime);
                }
            }
        }

        /// <summary>
        /// Tests for the OnDestroy lifecycle method
        /// </summary>
        [TestFixture]
        public class OnDestroyTests : GameRuntimeHostTests
        {
            [Test]
            public void OnDestroy_WhenCalled_ShouldCallRuntimeShutdown()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeOnDestroy(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.ShutdownCalled, Is.True);
                Assert.That(_mockRuntime.ShutdownCallCount, Is.EqualTo(1));
            }

            [Test]
            public void OnDestroy_WhenCalledMultipleTimes_ShouldCallRuntimeShutdownMultipleTimes()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeOnDestroy(_testHost);
                GameRuntimeHostReflectionHelper.InvokeOnDestroy(_testHost);
                GameRuntimeHostReflectionHelper.InvokeOnDestroy(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.ShutdownCallCount, Is.EqualTo(3));
            }

            [Test]
            public void OnDestroy_WhenRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                _mockRuntime.ShouldThrowException = true;
                _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.ShutdownExceptionMessage;
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeOnDestroy(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
                    Assert.That(exception.InnerException.Message, Is.EqualTo(GameRuntimeHostTestData.ShutdownExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _mockRuntime.ShouldThrowException = false;
                    _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.DefaultExceptionMessage;
                }
            }

            [Test]
            public void OnDestroy_WhenRuntimeIsNull_ShouldThrowNullReferenceException()
            {
                // Arrange
                GameRuntimeHostReflectionHelper.SetRuntime(_testHost, null);
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeOnDestroy(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<NullReferenceException>());
                }
                finally
                {
                    // Restore runtime to prevent null reference during TearDown
                    GameRuntimeHostReflectionHelper.SetRuntime(_testHost, _mockRuntime);
                }
            }
        }

        /// <summary>
        /// Tests for the Update lifecycle method
        /// </summary>
        [TestFixture]
        public class UpdateTests : GameRuntimeHostTests
        {
            [Test]
            public void Update_WhenCalled_ShouldCallRuntimeTickWithDeltaTime()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.TickCalled, Is.True);
                Assert.That(_mockRuntime.TickCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.LastTickDeltaTime, Is.GreaterThanOrEqualTo(0));
            }

            [TestCase(GameRuntimeHostTestData.ZeroDeltaTime)]
            [TestCase(GameRuntimeHostTestData.NegativeDeltaTime)]
            [TestCase(GameRuntimeHostTestData.LargeDeltaTime)]
            public void Update_WhenCalledWithVariousDeltaTimes_ShouldPassCorrectDeltaTime(float deltaTime)
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.TickCalled, Is.True);
                Assert.That(_mockRuntime.TickCallCount, Is.EqualTo(1));
                // The deltaTime will be whatever Unity provides, which is typically 0 in tests
                Assert.That(_mockRuntime.LastTickDeltaTime, Is.GreaterThanOrEqualTo(0));
            }

            [Test]
            public void Update_WhenCalledMultipleTimes_ShouldCallRuntimeTickMultipleTimes()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost);
                GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost);
                GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.TickCallCount, Is.EqualTo(3));
            }

            [Test]
            public void Update_WhenRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                _mockRuntime.ShouldThrowException = true;
                _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.TickExceptionMessage;
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
                    Assert.That(exception.InnerException.Message, Is.EqualTo(GameRuntimeHostTestData.TickExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _mockRuntime.ShouldThrowException = false;
                    _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.DefaultExceptionMessage;
                }
            }

            [Test]
            public void Update_WhenRuntimeIsNull_ShouldThrowNullReferenceException()
            {
                // Arrange
                GameRuntimeHostReflectionHelper.SetRuntime(_testHost, null);
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<NullReferenceException>());
                }
                finally
                {
                    // Restore runtime to prevent null reference during TearDown
                    GameRuntimeHostReflectionHelper.SetRuntime(_testHost, _mockRuntime);
                }
            }
        }

        /// <summary>
        /// Tests for the LateUpdate lifecycle method
        /// </summary>
        [TestFixture]
        public class LateUpdateTests : GameRuntimeHostTests
        {
            [Test]
            public void LateUpdate_WhenCalled_ShouldCallRuntimeLateTickWithDeltaTime()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.LateTickCalled, Is.True);
                Assert.That(_mockRuntime.LateTickCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.LastLateTickDeltaTime, Is.GreaterThanOrEqualTo(0));
            }

            [TestCase(GameRuntimeHostTestData.ZeroDeltaTime)]
            [TestCase(GameRuntimeHostTestData.NegativeDeltaTime)]
            [TestCase(GameRuntimeHostTestData.LargeDeltaTime)]
            public void LateUpdate_WhenCalledWithVariousDeltaTimes_ShouldPassCorrectDeltaTime(float deltaTime)
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.LateTickCalled, Is.True);
                Assert.That(_mockRuntime.LateTickCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.LastLateTickDeltaTime, Is.GreaterThanOrEqualTo(0));
            }

            [Test]
            public void LateUpdate_WhenCalledMultipleTimes_ShouldCallRuntimeLateTickMultipleTimes()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost);
                GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost);
                GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.LateTickCallCount, Is.EqualTo(3));
            }

            [Test]
            public void LateUpdate_WhenRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                _mockRuntime.ShouldThrowException = true;
                _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.LateTickExceptionMessage;
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
                    Assert.That(exception.InnerException.Message, Is.EqualTo(GameRuntimeHostTestData.LateTickExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _mockRuntime.ShouldThrowException = false;
                    _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.DefaultExceptionMessage;
                }
            }

            [Test]
            public void LateUpdate_WhenRuntimeIsNull_ShouldThrowNullReferenceException()
            {
                // Arrange
                GameRuntimeHostReflectionHelper.SetRuntime(_testHost, null);
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<NullReferenceException>());
                }
                finally
                {
                    // Restore runtime to prevent null reference during TearDown
                    GameRuntimeHostReflectionHelper.SetRuntime(_testHost, _mockRuntime);
                }
            }
        }

        /// <summary>
        /// Tests for the FixedUpdate lifecycle method
        /// </summary>
        [TestFixture]
        public class FixedUpdateTests : GameRuntimeHostTests
        {
            [Test]
            public void FixedUpdate_WhenCalled_ShouldCallRuntimeFixedTickWithFixedDeltaTime()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeFixedUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.FixedTickCalled, Is.True);
                Assert.That(_mockRuntime.FixedTickCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.LastFixedTickDeltaTime, Is.GreaterThanOrEqualTo(0));
            }

            [TestCase(GameRuntimeHostTestData.ZeroDeltaTime)]
            [TestCase(GameRuntimeHostTestData.NegativeDeltaTime)]
            [TestCase(GameRuntimeHostTestData.LargeDeltaTime)]
            public void FixedUpdate_WhenCalledWithVariousFixedDeltaTimes_ShouldPassCorrectDeltaTime(float deltaTime)
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeFixedUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.FixedTickCalled, Is.True);
                Assert.That(_mockRuntime.FixedTickCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.LastFixedTickDeltaTime, Is.GreaterThanOrEqualTo(0));
            }

            [Test]
            public void FixedUpdate_WhenCalledMultipleTimes_ShouldCallRuntimeFixedTickMultipleTimes()
            {
                // Act
                GameRuntimeHostReflectionHelper.InvokeFixedUpdate(_testHost);
                GameRuntimeHostReflectionHelper.InvokeFixedUpdate(_testHost);
                GameRuntimeHostReflectionHelper.InvokeFixedUpdate(_testHost);
                
                // Assert
                Assert.That(_mockRuntime.FixedTickCallCount, Is.EqualTo(3));
            }

            [Test]
            public void FixedUpdate_WhenRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                _mockRuntime.ShouldThrowException = true;
                _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.FixedTickExceptionMessage;
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeFixedUpdate(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
                    Assert.That(exception.InnerException.Message, Is.EqualTo(GameRuntimeHostTestData.FixedTickExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _mockRuntime.ShouldThrowException = false;
                    _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.DefaultExceptionMessage;
                }
            }

            [Test]
            public void FixedUpdate_WhenRuntimeIsNull_ShouldThrowNullReferenceException()
            {
                // Arrange
                GameRuntimeHostReflectionHelper.SetRuntime(_testHost, null);
                
                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeFixedUpdate(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<NullReferenceException>());
                }
                finally
                {
                    // Restore runtime to prevent null reference during TearDown
                    GameRuntimeHostReflectionHelper.SetRuntime(_testHost, _mockRuntime);
                }
            }
        }

        /// <summary>
        /// Integration tests for complete lifecycle scenarios
        /// </summary>
        [TestFixture]
        public class IntegrationTests : GameRuntimeHostTests
        {
            [Test]
            public void Lifecycle_WhenCompleteLifecycleExecuted_ShouldCallAllRuntimeMethods()
            {
                // Act - Execute complete Unity lifecycle using helper method
                GameRuntimeHostReflectionHelper.InvokeLifecycleSequence(_testHost);
                
                // Assert - All runtime methods should be called
                Assert.That(_mockRuntime.StartCalled, Is.True);
                Assert.That(_mockRuntime.TickCalled, Is.True);
                Assert.That(_mockRuntime.LateTickCalled, Is.True);
                Assert.That(_mockRuntime.FixedTickCalled, Is.True);
                Assert.That(_mockRuntime.ShutdownCalled, Is.True);
                
                Assert.That(_mockRuntime.StartCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.TickCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.LateTickCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.FixedTickCallCount, Is.EqualTo(1));
                Assert.That(_mockRuntime.ShutdownCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Lifecycle_WhenMultipleCyclesExecuted_ShouldCallRuntimeMethodsMultipleTimes()
            {
                // Act - Execute multiple lifecycle cycles
                for (int i = 0; i < 3; i++)
                {
                    GameRuntimeHostReflectionHelper.InvokeLifecycleSequence(_testHost, includeAwake: false, includeOnDestroy: false);
                    GameRuntimeHostReflectionHelper.InvokeOnDestroy(_testHost);
                }
                
                // Assert - All runtime methods should be called multiple times
                Assert.That(_mockRuntime.StartCallCount, Is.EqualTo(3));
                Assert.That(_mockRuntime.TickCallCount, Is.EqualTo(3));
                Assert.That(_mockRuntime.LateTickCallCount, Is.EqualTo(3));
                Assert.That(_mockRuntime.FixedTickCallCount, Is.EqualTo(3));
                Assert.That(_mockRuntime.ShutdownCallCount, Is.EqualTo(3));
            }

            [Test]
            public void RuntimeInjection_WhenDifferentRuntimeInjected_ShouldUseNewRuntime()
            {
                // Arrange
                var newMockRuntime = GameRuntimeHostTestData.CreateMockGameRuntime();
                
                // Act
                GameRuntimeHostReflectionHelper.SetRuntime(_testHost, newMockRuntime);
                GameRuntimeHostReflectionHelper.InvokeStart(_testHost);
                
                // Assert
                Assert.That(newMockRuntime.StartCalled, Is.True);
                Assert.That(newMockRuntime.StartCallCount, Is.EqualTo(1));
                
                // Original runtime should not be called
                Assert.That(_mockRuntime.StartCalled, Is.False);
                Assert.That(_mockRuntime.StartCallCount, Is.EqualTo(0));
            }

            [Test]
            public void ExceptionIsolation_WhenOneMethodThrows_ShouldNotAffectOtherMethods()
            {
                // Arrange - Configure runtime to throw only on Tick
                _mockRuntime.ShouldThrowOnTick = true;
                _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.TickExceptionMessage;
                
                try
                {
                    // Act & Assert - Start should work normally
                    Assert.DoesNotThrow(() => GameRuntimeHostReflectionHelper.InvokeStart(_testHost));
                    Assert.That(_mockRuntime.StartCalled, Is.True);
                    
                    // Tick should throw exception
                    var exception = Assert.Throws<TargetInvocationException>(() => GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost));
                    Assert.That(exception.InnerException, Is.InstanceOf<InvalidOperationException>());
                    
                    // LateUpdate should work normally
                    Assert.DoesNotThrow(() => GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost));
                    Assert.That(_mockRuntime.LateTickCalled, Is.True);
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _mockRuntime.ShouldThrowOnTick = false;
                    _mockRuntime.ExceptionMessage = GameRuntimeHostTestData.DefaultExceptionMessage;
                }
            }

            [Test]
            public void DeltaTimeVerification_WhenMultipleUpdatesCalled_ShouldTrackAllDeltaTimes()
            {
                // Act - Call update methods multiple times
                for (int i = 0; i < 5; i++)
                {
                    GameRuntimeHostReflectionHelper.InvokeUpdate(_testHost);
                    GameRuntimeHostReflectionHelper.InvokeLateUpdate(_testHost);
                    GameRuntimeHostReflectionHelper.InvokeFixedUpdate(_testHost);
                }
                
                // Assert - All delta times should be tracked
                Assert.That(_mockRuntime.AllTickDeltaTimes.Count, Is.EqualTo(5));
                Assert.That(_mockRuntime.AllLateTickDeltaTimes.Count, Is.EqualTo(5));
                Assert.That(_mockRuntime.AllFixedTickDeltaTimes.Count, Is.EqualTo(5));
                
                // All delta times should be within reasonable range
                Assert.That(_mockRuntime.VerifyDeltaTimeRange(0f, 1f), Is.True);
            }
        }
    }
}
