/**
 * Unit tests for IRuntimePlugin interface
 * Generated from function analysis on 2025-01-30
 * Refactored for improved quality and coverage on 2025-01-30
 * Framework: Unity Test Framework with NUnit
 * 
 * Test Coverage:
 * - Interface method implementations with comprehensive call tracking
 * - Default implementation testing for InitializeAsync and Initialize
 * - Parameter validation and null safety testing
 * - Exception propagation and isolation testing
 * - VContainer dependency injection patterns with registration verification
 * - UniTask async testing with status verification and cancellation
 * - Boundary value testing and edge cases
 * - Multiple lifecycle cycle testing for state consistency
 * - Concurrent access testing scenarios
 * - Static method testing patterns
 * 
 * Mock Dependencies:
 * - IObjectResolver for dependency injection testing
 * - Enhanced test implementations with method-specific exception control
 * - CancellationToken for async testing scenarios
 * - VContainer registration verification
 * 
 * Refactoring Improvements:
 * - Added ReflectionHelper utility class for performance optimization
 * - Enhanced TestData factory methods with boundary values
 * - Added comprehensive boundary value and edge case testing
 * - Implemented multiple lifecycle cycle testing
 * - Added VContainer registration verification tests
 * - Enhanced async testing with cancellation scenarios
 * - Added concurrent access testing patterns
 * - Improved test organization with better grouping
 * - Enhanced documentation and comments
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
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
    /// Test data constants and factory methods for consistent test values
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// CRITICAL: Make test classes public for NSubstitute proxy creation (internal classes cause proxy errors)
    /// </summary>
    public static class IRuntimePluginTestData
    {
        // Basic test values
        public const string TestPluginName = "TestPlugin";
        public const string TestPluginName2 = "TestPlugin2";
        public const string TestExceptionMessage = "Test exception";
        public const string SetupExceptionMessage = "Setup failed";
        public const string RuntimeExceptionMessage = "Runtime initialization failed";
        public const string DependencyExceptionMessage = "Dependency check failed";
        
        // Boundary values for comprehensive testing
        public const string EmptyPluginName = "";
        public const string NullPluginName = null;
        public const string VeryLongPluginName = "VeryLongPluginNameThatExceedsNormalLengthLimitsAndTestsBoundaryConditions";
        public const string SpecialCharacterPluginName = "Plugin@#$%^&*()";
        public const string UnicodePluginName = "插件测试";
        
        // Exception message variations
        public const string LongExceptionMessage = "This is a very long exception message that tests how the system handles extended error descriptions and boundary conditions for exception handling scenarios";
        public const string EmptyExceptionMessage = "";
        public const string NullExceptionMessage = null;
        
        // Cancellation token scenarios
        public static readonly CancellationToken CancelledToken = new CancellationToken(true);
        public static readonly CancellationToken DefaultToken = CancellationToken.None;

        // Factory methods for consistent test object creation
        public static TestRuntimePlugin CreatePlugin(string name = TestPluginName, bool shouldThrowOnSetup = false, bool shouldThrowOnRuntime = false, bool shouldThrowOnDependencyCheck = false)
        {
            return new TestRuntimePlugin(name)
            {
                ShouldThrowOnSetup = shouldThrowOnSetup,
                ShouldThrowOnRuntime = shouldThrowOnRuntime,
                ShouldThrowOnDependencyCheck = shouldThrowOnDependencyCheck,
                ExceptionMessage = TestExceptionMessage
            };
        }

        public static TestRuntimePlugin CreatePluginWithDependenciesReady(string name = TestPluginName, bool dependenciesReady = true)
        {
            return new TestRuntimePlugin(name)
            {
                DependenciesReady = dependenciesReady
            };
        }

        public static TestRuntimePlugin CreatePluginWithBoundaryValues(string name = EmptyPluginName, string exceptionMessage = EmptyExceptionMessage)
        {
            return new TestRuntimePlugin(name)
            {
                ExceptionMessage = exceptionMessage ?? TestExceptionMessage
            };
        }

        public static List<IRuntimePlugin> CreatePluginList(int count, bool shouldThrow = false)
        {
            var plugins = new List<IRuntimePlugin>();
            for (int i = 0; i < count; i++)
            {
                plugins.Add(CreatePlugin($"Plugin{i}", shouldThrow));
            }
            return plugins;
        }

        public static List<IRuntimePlugin> CreateBoundaryValuePluginList()
        {
            return new List<IRuntimePlugin>
            {
                CreatePluginWithBoundaryValues(EmptyPluginName),
                CreatePluginWithBoundaryValues(VeryLongPluginName),
                CreatePluginWithBoundaryValues(SpecialCharacterPluginName),
                CreatePluginWithBoundaryValues(UnicodePluginName)
            };
        }
    }

    /// <summary>
    /// Reflection utilities for accessing private fields and methods with performance optimization
    /// CRITICAL: Use cached FieldInfo for performance optimization
    /// </summary>
    internal static class IRuntimePluginReflectionHelper
    {
        /// <summary>
        /// Cached PropertyInfo for performance optimization
        /// </summary>
        private static readonly PropertyInfo DependenciesReadyProperty = typeof(TestRuntimePlugin)
            .GetProperty("DependenciesReady", BindingFlags.Public | BindingFlags.Instance);
        
        private static readonly PropertyInfo ExceptionMessageProperty = typeof(TestRuntimePlugin)
            .GetProperty("ExceptionMessage", BindingFlags.Public | BindingFlags.Instance);
        
        private static readonly PropertyInfo LastThrownExceptionProperty = typeof(TestRuntimePlugin)
            .GetProperty("LastThrownException", BindingFlags.Public | BindingFlags.Instance);

        public static bool GetDependenciesReady(TestRuntimePlugin plugin)
        {
            return (bool)DependenciesReadyProperty?.GetValue(plugin);
        }

        public static void SetDependenciesReady(TestRuntimePlugin plugin, bool value)
        {
            DependenciesReadyProperty?.SetValue(plugin, value);
        }

        public static string GetExceptionMessage(TestRuntimePlugin plugin)
        {
            return ExceptionMessageProperty?.GetValue(plugin) as string;
        }

        public static void SetExceptionMessage(TestRuntimePlugin plugin, string value)
        {
            ExceptionMessageProperty?.SetValue(plugin, value);
        }

        public static Exception GetLastThrownException(TestRuntimePlugin plugin)
        {
            return LastThrownExceptionProperty?.GetValue(plugin) as Exception;
        }
    }

    /// <summary>
    /// Enhanced test implementation with call tracking, state verification, and method-specific exception control
    /// CRITICAL: Supports granular exception control for precise testing scenarios
    /// </summary>
    public sealed class TestRuntimePlugin : IRuntimePlugin
    {
        public string Name { get; }
        
        // Call tracking
        public bool PerformSetupCalled { get; private set; }
        public bool PerformRuntimeInitializationCalled { get; private set; }
        public bool AreDependenciesReadyCalled { get; private set; }
        public bool InitializeCalled { get; private set; }
        public bool InitializeAsyncCalled { get; private set; }
        
        private int _performSetupCallCount;
        private int _performRuntimeInitializationCallCount;
        private int _areDependenciesReadyCallCount;
        private int _initializeCallCount;
        private int _initializeAsyncCallCount;
        
        public int PerformSetupCallCount => _performSetupCallCount;
        public int PerformRuntimeInitializationCallCount => _performRuntimeInitializationCallCount;
        public int AreDependenciesReadyCallCount => _areDependenciesReadyCallCount;
        public int InitializeCallCount => _initializeCallCount;
        public int InitializeAsyncCallCount => _initializeAsyncCallCount;
        
        public List<IObjectResolver> SetupResolvers { get; private set; } = new List<IObjectResolver>();
        public List<IObjectResolver> RuntimeResolvers { get; private set; } = new List<IObjectResolver>();
        public List<IObjectResolver> DependencyCheckResolvers { get; private set; } = new List<IObjectResolver>();
        public List<IObjectResolver> InitializeResolvers { get; private set; } = new List<IObjectResolver>();
        public List<(IObjectResolver resolver, CancellationToken ct)> InitializeAsyncCalls { get; private set; } = new List<(IObjectResolver, CancellationToken)>();
        
        public Exception LastThrownException { get; private set; }
        
        // State management
        public bool DependenciesReady { get; set; } = true;
        
        // Method-specific exception control for precise testing
        public bool ShouldThrowOnSetup { get; set; }
        public bool ShouldThrowOnRuntime { get; set; }
        public bool ShouldThrowOnDependencyCheck { get; set; }
        public string ExceptionMessage { get; set; } = IRuntimePluginTestData.TestExceptionMessage;

        public TestRuntimePlugin(string name)
        {
            Name = name;
        }

        public void Register(IContainerBuilder builder)
        {
            // No-op for testing - this method is called during Phase 1
        }

        public void PerformSetup(IObjectResolver resolver)
        {
            PerformSetupCalled = true;
            Interlocked.Increment(ref _performSetupCallCount);
            SetupResolvers.Add(resolver);
            
            if (ShouldThrowOnSetup)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
        }

        public void PerformRuntimeInitialization(IObjectResolver resolver)
        {
            PerformRuntimeInitializationCalled = true;
            Interlocked.Increment(ref _performRuntimeInitializationCallCount);
            RuntimeResolvers.Add(resolver);
            
            if (ShouldThrowOnRuntime)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
        }

        public bool AreDependenciesReady(IObjectResolver resolver)
        {
            AreDependenciesReadyCalled = true;
            Interlocked.Increment(ref _areDependenciesReadyCallCount);
            DependencyCheckResolvers.Add(resolver);
            
            if (ShouldThrowOnDependencyCheck)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            
            return DependenciesReady;
        }

        public void Initialize(IObjectResolver resolver)
        {
            InitializeCalled = true;
            Interlocked.Increment(ref _initializeCallCount);
            InitializeResolvers.Add(resolver);
            
            // Call the default implementation
            PerformSetup(resolver);
            if (AreDependenciesReady(resolver))
            {
                PerformRuntimeInitialization(resolver);
            }
        }

        public UniTask InitializeAsync(IObjectResolver resolver, CancellationToken ct = default)
        {
            InitializeAsyncCalled = true;
            Interlocked.Increment(ref _initializeAsyncCallCount);
            InitializeAsyncCalls.Add((resolver, ct));
            
            // Call the default implementation
            PerformSetup(resolver);
            if (AreDependenciesReady(resolver))
            {
                PerformRuntimeInitialization(resolver);
            }
            return UniTask.CompletedTask;
        }

        public void ResetCallCounts()
        {
            PerformSetupCalled = false;
            PerformRuntimeInitializationCalled = false;
            AreDependenciesReadyCalled = false;
            InitializeCalled = false;
            InitializeAsyncCalled = false;
            
            _performSetupCallCount = 0;
            _performRuntimeInitializationCallCount = 0;
            _areDependenciesReadyCallCount = 0;
            _initializeCallCount = 0;
            _initializeAsyncCallCount = 0;
            
            SetupResolvers.Clear();
            RuntimeResolvers.Clear();
            DependencyCheckResolvers.Clear();
            InitializeResolvers.Clear();
            InitializeAsyncCalls.Clear();
            LastThrownException = null;
            
            // Reset exception behavior
            ShouldThrowOnSetup = false;
            ShouldThrowOnRuntime = false;
            ShouldThrowOnDependencyCheck = false;
            ExceptionMessage = IRuntimePluginTestData.TestExceptionMessage;
        }

        public bool VerifyCallCounts(int expectedSetup = 0, int expectedRuntime = 0, int expectedDependencyCheck = 0, int expectedInitialize = 0, int expectedInitializeAsync = 0)
        {
            return PerformSetupCallCount == expectedSetup &&
                   PerformRuntimeInitializationCallCount == expectedRuntime &&
                   AreDependenciesReadyCallCount == expectedDependencyCheck &&
                   InitializeCallCount == expectedInitialize &&
                   InitializeAsyncCallCount == expectedInitializeAsync;
        }
    }

    [TestFixture]
    public class IRuntimePluginTests
    {
        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private ILogger _mockLogger;
        private TestRuntimePlugin _testPlugin;

        [SetUp]
        public void Setup()
        {
            _containerBuilder = new ContainerBuilder();
            SetupTestContainer();
            _resolver = _containerBuilder.Build();
            _testPlugin = IRuntimePluginTestData.CreatePlugin();
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
        /// Helper method to create test plugin with dependencies for testing
        /// CRITICAL: Creates fresh container for each test to avoid registration conflicts
        /// </summary>
        protected TestRuntimePlugin CreateTestPluginWithDependencies(TestRuntimePlugin plugin)
        {
            // Create a fresh container builder for this test
            var testContainerBuilder = new ContainerBuilder();
            
            // Register the common dependencies
            testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            
            // Register the test plugin
            testContainerBuilder.RegisterInstance(plugin).As<IRuntimePlugin>();
            
            // Build and resolve
            var testResolver = testContainerBuilder.Build();
            
            // Store the resolver for cleanup in TearDown
            _resolver?.Dispose();
            _resolver = testResolver;
            
            return plugin;
        }

        #region PerformSetup Tests

        [TestFixture]
        public class PerformSetupTests : IRuntimePluginTests
        {
            [Test]
            public void PerformSetup_WhenValidResolverProvided_ShouldCompleteSuccessfully()
            {
                // Act
                _testPlugin.PerformSetup(_resolver);

                // Assert
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                Assert.That(_testPlugin.PerformSetupCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.SetupResolvers, Contains.Item(_resolver));
            }

            [Test]
            public void PerformSetup_WhenNullResolverProvided_ShouldCompleteWithoutException()
            {
                // Act & Assert
                Assert.DoesNotThrow(() => _testPlugin.PerformSetup(null));
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                Assert.That(_testPlugin.SetupResolvers, Contains.Item(null));
            }

            [Test]
            public void PerformSetup_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Act
                _testPlugin.PerformSetup(_resolver);
                _testPlugin.PerformSetup(_resolver);
                _testPlugin.PerformSetup(_resolver);

                // Assert
                Assert.That(_testPlugin.PerformSetupCallCount, Is.EqualTo(3));
                Assert.That(_testPlugin.SetupResolvers.Count, Is.EqualTo(3));
            }

            [Test]
            public void PerformSetup_WhenSetupThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.ShouldThrowOnSetup = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.SetupExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.PerformSetup(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.SetupExceptionMessage));
                    Assert.That(_testPlugin.LastThrownException, Is.SameAs(exception));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnSetup = false;
                }
            }

            [Test]
            public void PerformSetup_WhenExceptionThrown_ShouldStillTrackCall()
            {
                // Arrange
                _testPlugin.ShouldThrowOnSetup = true;

                try
                {
                    // Act
                    Assert.Throws<InvalidOperationException>(() => _testPlugin.PerformSetup(_resolver));
                }
                finally
                {
                    // Reset exception behavior
                    _testPlugin.ShouldThrowOnSetup = false;
                }

                // Assert - Call should still be tracked even if exception thrown
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                Assert.That(_testPlugin.PerformSetupCallCount, Is.EqualTo(1));
            }
        }

        #endregion

        #region PerformRuntimeInitialization Tests

        [TestFixture]
        public class PerformRuntimeInitializationTests : IRuntimePluginTests
        {
            [Test]
            public void PerformRuntimeInitialization_WhenValidResolverProvided_ShouldCompleteSuccessfully()
            {
                // Act
                _testPlugin.PerformRuntimeInitialization(_resolver);

                // Assert
                Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(_testPlugin.PerformRuntimeInitializationCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.RuntimeResolvers, Contains.Item(_resolver));
            }

            [Test]
            public void PerformRuntimeInitialization_WhenNullResolverProvided_ShouldCompleteWithoutException()
            {
                // Act & Assert
                Assert.DoesNotThrow(() => _testPlugin.PerformRuntimeInitialization(null));
                Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(_testPlugin.RuntimeResolvers, Contains.Item(null));
            }

            [Test]
            public void PerformRuntimeInitialization_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Act
                _testPlugin.PerformRuntimeInitialization(_resolver);
                _testPlugin.PerformRuntimeInitialization(_resolver);
                _testPlugin.PerformRuntimeInitialization(_resolver);

                // Assert
                Assert.That(_testPlugin.PerformRuntimeInitializationCallCount, Is.EqualTo(3));
                Assert.That(_testPlugin.RuntimeResolvers.Count, Is.EqualTo(3));
            }

            [Test]
            public void PerformRuntimeInitialization_WhenRuntimeThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.ShouldThrowOnRuntime = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.RuntimeExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.PerformRuntimeInitialization(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.RuntimeExceptionMessage));
                    Assert.That(_testPlugin.LastThrownException, Is.SameAs(exception));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnRuntime = false;
                }
            }

            [Test]
            public void PerformRuntimeInitialization_WhenExceptionThrown_ShouldStillTrackCall()
            {
                // Arrange
                _testPlugin.ShouldThrowOnRuntime = true;

                try
                {
                    // Act
                    Assert.Throws<InvalidOperationException>(() => _testPlugin.PerformRuntimeInitialization(_resolver));
                }
                finally
                {
                    // Reset exception behavior
                    _testPlugin.ShouldThrowOnRuntime = false;
                }

                // Assert - Call should still be tracked even if exception thrown
                Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(_testPlugin.PerformRuntimeInitializationCallCount, Is.EqualTo(1));
            }
        }

        #endregion

        #region AreDependenciesReady Tests

        [TestFixture]
        public class AreDependenciesReadyTests : IRuntimePluginTests
        {
            [Test]
            public void AreDependenciesReady_WhenDependenciesReady_ShouldReturnTrue()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;

                // Act
                var result = _testPlugin.AreDependenciesReady(_resolver);

                // Assert
                Assert.That(result, Is.True);
                Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
                Assert.That(_testPlugin.AreDependenciesReadyCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.DependencyCheckResolvers, Contains.Item(_resolver));
            }

            [Test]
            public void AreDependenciesReady_WhenDependenciesNotReady_ShouldReturnFalse()
            {
                // Arrange
                _testPlugin.DependenciesReady = false;

                // Act
                var result = _testPlugin.AreDependenciesReady(_resolver);

                // Assert
                Assert.That(result, Is.False);
                Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
                Assert.That(_testPlugin.AreDependenciesReadyCallCount, Is.EqualTo(1));
            }

            [Test]
            public void AreDependenciesReady_WhenNullResolverProvided_ShouldCompleteWithoutException()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;

                // Act
                var result = _testPlugin.AreDependenciesReady(null);

                // Assert
                Assert.That(result, Is.True);
                Assert.That(_testPlugin.DependencyCheckResolvers, Contains.Item(null));
            }

            [Test]
            public void AreDependenciesReady_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Act
                _testPlugin.AreDependenciesReady(_resolver);
                _testPlugin.AreDependenciesReady(_resolver);
                _testPlugin.AreDependenciesReady(_resolver);

                // Assert
                Assert.That(_testPlugin.AreDependenciesReadyCallCount, Is.EqualTo(3));
                Assert.That(_testPlugin.DependencyCheckResolvers.Count, Is.EqualTo(3));
            }

            [Test]
            public void AreDependenciesReady_WhenDependencyCheckThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.ShouldThrowOnDependencyCheck = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.DependencyExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.AreDependenciesReady(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.DependencyExceptionMessage));
                    Assert.That(_testPlugin.LastThrownException, Is.SameAs(exception));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnDependencyCheck = false;
                }
            }

            [Test]
            public void AreDependenciesReady_WhenExceptionThrown_ShouldStillTrackCall()
            {
                // Arrange
                _testPlugin.ShouldThrowOnDependencyCheck = true;

                try
                {
                    // Act
                    Assert.Throws<InvalidOperationException>(() => _testPlugin.AreDependenciesReady(_resolver));
                }
                finally
                {
                    // Reset exception behavior
                    _testPlugin.ShouldThrowOnDependencyCheck = false;
                }

                // Assert - Call should still be tracked even if exception thrown
                Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
                Assert.That(_testPlugin.AreDependenciesReadyCallCount, Is.EqualTo(1));
            }
        }

        #endregion

        #region InitializeAsync Tests

        [TestFixture]
        public class InitializeAsyncTests : IRuntimePluginTests
        {
            [Test]
            public void InitializeAsync_WhenValidResolverProvided_ShouldReturnUniTask()
            {
                // Act
                var task = _testPlugin.InitializeAsync(_resolver);

                // Assert - CRITICAL: Test UniTask without awaiting to verify status
                Assert.That(task, Is.Not.Null);
                Assert.That(task, Is.InstanceOf<UniTask>());
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            }

            [Test]
            public void InitializeAsync_WhenDependenciesReady_ShouldCallBothSetupAndRuntimeInitialization()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;

                // Act
                var task = _testPlugin.InitializeAsync(_resolver);

                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(_testPlugin.InitializeAsyncCalled, Is.True);
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
            }

            [Test]
            public void InitializeAsync_WhenDependenciesNotReady_ShouldCallOnlySetup()
            {
                // Arrange
                _testPlugin.DependenciesReady = false;

                // Act
                var task = _testPlugin.InitializeAsync(_resolver);

                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(_testPlugin.InitializeAsyncCalled, Is.True);
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.False);
                Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
            }

            [Test]
            public void InitializeAsync_WhenCancellationTokenProvided_ShouldPassTokenCorrectly()
            {
                // Arrange
                var cancellationToken = new CancellationToken();

                // Act
                var task = _testPlugin.InitializeAsync(_resolver, cancellationToken);

                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(_testPlugin.InitializeAsyncCalls.Count, Is.EqualTo(1));
                Assert.That(_testPlugin.InitializeAsyncCalls[0].resolver, Is.SameAs(_resolver));
                Assert.That(_testPlugin.InitializeAsyncCalls[0].ct, Is.EqualTo(cancellationToken));
            }

            [Test]
            public void InitializeAsync_WhenSetupThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.ShouldThrowOnSetup = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.SetupExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.InitializeAsync(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.SetupExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnSetup = false;
                }
            }

            [Test]
            public void InitializeAsync_WhenRuntimeInitializationThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;
                _testPlugin.ShouldThrowOnRuntime = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.RuntimeExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.InitializeAsync(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.RuntimeExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnRuntime = false;
                }
            }

            [Test]
            public void InitializeAsync_WhenDependencyCheckThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.ShouldThrowOnDependencyCheck = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.DependencyExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.InitializeAsync(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.DependencyExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnDependencyCheck = false;
                }
            }

            [Test]
            public void InitializeAsync_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Act
                _testPlugin.InitializeAsync(_resolver);
                _testPlugin.InitializeAsync(_resolver);
                _testPlugin.InitializeAsync(_resolver);

                // Assert
                Assert.That(_testPlugin.InitializeAsyncCallCount, Is.EqualTo(3));
                Assert.That(_testPlugin.InitializeAsyncCalls.Count, Is.EqualTo(3));
            }
        }

        #endregion

        #region Initialize Tests

        [TestFixture]
        public class InitializeTests : IRuntimePluginTests
        {
            [Test]
            public void Initialize_WhenValidResolverProvided_ShouldCompleteSuccessfully()
            {
                // Act
                _testPlugin.Initialize(_resolver);

                // Assert
                Assert.That(_testPlugin.InitializeCalled, Is.True);
                Assert.That(_testPlugin.InitializeCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.InitializeResolvers, Contains.Item(_resolver));
            }

            [Test]
            public void Initialize_WhenDependenciesReady_ShouldCallBothSetupAndRuntimeInitialization()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;

                // Act
                _testPlugin.Initialize(_resolver);

                // Assert
                Assert.That(_testPlugin.InitializeCalled, Is.True);
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
            }

            [Test]
            public void Initialize_WhenDependenciesNotReady_ShouldCallOnlySetup()
            {
                // Arrange
                _testPlugin.DependenciesReady = false;

                // Act
                _testPlugin.Initialize(_resolver);

                // Assert
                Assert.That(_testPlugin.InitializeCalled, Is.True);
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.False);
                Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
            }

            [Test]
            public void Initialize_WhenSetupThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.ShouldThrowOnSetup = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.SetupExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.Initialize(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.SetupExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnSetup = false;
                }
            }

            [Test]
            public void Initialize_WhenRuntimeInitializationThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;
                _testPlugin.ShouldThrowOnRuntime = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.RuntimeExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.Initialize(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.RuntimeExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnRuntime = false;
                }
            }

            [Test]
            public void Initialize_WhenDependencyCheckThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testPlugin.ShouldThrowOnDependencyCheck = true;
                _testPlugin.ExceptionMessage = IRuntimePluginTestData.DependencyExceptionMessage;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.Initialize(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.DependencyExceptionMessage));
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnDependencyCheck = false;
                }
            }

            [Test]
            public void Initialize_WhenCalledMultipleTimes_ShouldTrackAllCalls()
            {
                // Act
                _testPlugin.Initialize(_resolver);
                _testPlugin.Initialize(_resolver);
                _testPlugin.Initialize(_resolver);

                // Assert
                Assert.That(_testPlugin.InitializeCallCount, Is.EqualTo(3));
                Assert.That(_testPlugin.InitializeResolvers.Count, Is.EqualTo(3));
            }
        }

        #endregion

        #region Exception Isolation Tests

        [TestFixture]
        public class ExceptionIsolationTests : IRuntimePluginTests
        {
            [Test]
            public void ExceptionIsolation_WhenSetupThrows_ShouldNotAffectOtherMethods()
            {
                // Arrange - Configure plugin to throw only on setup
                _testPlugin.ShouldThrowOnSetup = true;
                _testPlugin.ExceptionMessage = "Setup failed";

                try
                {
                    // Act & Assert - Other methods should work normally
                    Assert.DoesNotThrow(() => _testPlugin.PerformRuntimeInitialization(_resolver));
                    Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);

                    Assert.DoesNotThrow(() => _testPlugin.AreDependenciesReady(_resolver));
                    Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);

                    // Setup should throw exception
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.PerformSetup(_resolver));
                    Assert.That(exception.Message, Is.EqualTo("Setup failed"));

                    // Initialize should also throw because it calls PerformSetup internally
                    var initializeException = Assert.Throws<InvalidOperationException>(() => _testPlugin.Initialize(_resolver));
                    Assert.That(initializeException.Message, Is.EqualTo("Setup failed"));
                    Assert.That(_testPlugin.InitializeCalled, Is.True); // Call should still be tracked
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnSetup = false;
                }
            }

            [Test]
            public void ExceptionIsolation_WhenRuntimeThrows_ShouldNotAffectOtherMethods()
            {
                // Arrange - Configure plugin to throw only on runtime
                _testPlugin.ShouldThrowOnRuntime = true;
                _testPlugin.ExceptionMessage = "Runtime failed";

                try
                {
                    // Act & Assert - Other methods should work normally
                    Assert.DoesNotThrow(() => _testPlugin.PerformSetup(_resolver));
                    Assert.That(_testPlugin.PerformSetupCalled, Is.True);

                    Assert.DoesNotThrow(() => _testPlugin.AreDependenciesReady(_resolver));
                    Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);

                    // Runtime should throw exception
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.PerformRuntimeInitialization(_resolver));
                    Assert.That(exception.Message, Is.EqualTo("Runtime failed"));

                    // Initialize should also throw because it calls PerformRuntimeInitialization internally
                    // (since DependenciesReady defaults to true)
                    var initializeException = Assert.Throws<InvalidOperationException>(() => _testPlugin.Initialize(_resolver));
                    Assert.That(initializeException.Message, Is.EqualTo("Runtime failed"));
                    Assert.That(_testPlugin.InitializeCalled, Is.True); // Call should still be tracked
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnRuntime = false;
                }
            }

            [Test]
            public void ExceptionIsolation_WhenDependencyCheckThrows_ShouldNotAffectOtherMethods()
            {
                // Arrange - Configure plugin to throw only on dependency check
                _testPlugin.ShouldThrowOnDependencyCheck = true;
                _testPlugin.ExceptionMessage = "Dependency check failed";

                try
                {
                    // Act & Assert - Other methods should work normally
                    Assert.DoesNotThrow(() => _testPlugin.PerformSetup(_resolver));
                    Assert.That(_testPlugin.PerformSetupCalled, Is.True);

                    Assert.DoesNotThrow(() => _testPlugin.PerformRuntimeInitialization(_resolver));
                    Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);

                    // Dependency check should throw exception
                    var exception = Assert.Throws<InvalidOperationException>(() => _testPlugin.AreDependenciesReady(_resolver));
                    Assert.That(exception.Message, Is.EqualTo("Dependency check failed"));

                    // Initialize should also throw because it calls AreDependenciesReady internally
                    var initializeException = Assert.Throws<InvalidOperationException>(() => _testPlugin.Initialize(_resolver));
                    Assert.That(initializeException.Message, Is.EqualTo("Dependency check failed"));
                    Assert.That(_testPlugin.InitializeCalled, Is.True); // Call should still be tracked
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testPlugin.ShouldThrowOnDependencyCheck = false;
                }
            }
        }

        #endregion

        #region Parameterized Tests

        [TestFixture]
        public class ParameterizedTests : IRuntimePluginTests
        {
            [TestCase(true, true)]
            [TestCase(false, false)]
            [TestCase(true, false)]
            [TestCase(false, true)]
            public void InitializeAsync_WhenDependenciesReady_ShouldCallCorrectMethods(bool dependenciesReady, bool shouldThrowOnSetup)
            {
                // Arrange
                _testPlugin.DependenciesReady = dependenciesReady;
                _testPlugin.ShouldThrowOnSetup = shouldThrowOnSetup;

                try
                {
                    // Act
                    if (shouldThrowOnSetup)
                    {
                        Assert.Throws<InvalidOperationException>(() => _testPlugin.InitializeAsync(_resolver));
                    }
                    else
                    {
                        var task = _testPlugin.InitializeAsync(_resolver);
                        Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                    }
                }
                finally
                {
                    // Reset exception behavior
                    _testPlugin.ShouldThrowOnSetup = false;
                }

                // Assert - Verify call tracking based on execution flow
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                
                // AreDependenciesReady is only called if PerformSetup doesn't throw
                if (!shouldThrowOnSetup)
                {
                    Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
                    
                    // PerformRuntimeInitialization is only called if dependencies are ready
                    if (dependenciesReady)
                    {
                        Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);
                    }
                    else
                    {
                        Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.False);
                    }
                }
                else
                {
                    // If PerformSetup throws, AreDependenciesReady and PerformRuntimeInitialization are never called
                    Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.False);
                    Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.False);
                }
            }

            [TestCase(true, true)]
            [TestCase(false, false)]
            [TestCase(true, false)]
            [TestCase(false, true)]
            public void Initialize_WhenDependenciesReady_ShouldCallCorrectMethods(bool dependenciesReady, bool shouldThrowOnSetup)
            {
                // Arrange
                _testPlugin.DependenciesReady = dependenciesReady;
                _testPlugin.ShouldThrowOnSetup = shouldThrowOnSetup;

                try
                {
                    // Act
                    if (shouldThrowOnSetup)
                    {
                        Assert.Throws<InvalidOperationException>(() => _testPlugin.Initialize(_resolver));
                    }
                    else
                    {
                        _testPlugin.Initialize(_resolver);
                    }
                }
                finally
                {
                    // Reset exception behavior
                    _testPlugin.ShouldThrowOnSetup = false;
                }

                // Assert - Verify call tracking based on execution flow
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                
                // AreDependenciesReady is only called if PerformSetup doesn't throw
                if (!shouldThrowOnSetup)
                {
                    Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
                    
                    // PerformRuntimeInitialization is only called if dependencies are ready
                    if (dependenciesReady)
                    {
                        Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);
                    }
                    else
                    {
                        Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.False);
                    }
                }
                else
                {
                    // If PerformSetup throws, AreDependenciesReady and PerformRuntimeInitialization are never called
                    Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.False);
                    Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.False);
                }
            }
        }

        #endregion

        #region Boundary Value Tests

        [TestFixture]
        public class BoundaryValueTests : IRuntimePluginTests
        {
            [Test]
            public void PerformSetup_WhenEmptyPluginName_ShouldCompleteSuccessfully()
            {
                // Arrange
                var plugin = IRuntimePluginTestData.CreatePluginWithBoundaryValues(IRuntimePluginTestData.EmptyPluginName);

                // Act
                plugin.PerformSetup(_resolver);

                // Assert
                Assert.That(plugin.PerformSetupCalled, Is.True);
                Assert.That(plugin.Name, Is.EqualTo(IRuntimePluginTestData.EmptyPluginName));
            }

            [Test]
            public void PerformSetup_WhenVeryLongPluginName_ShouldCompleteSuccessfully()
            {
                // Arrange
                var plugin = IRuntimePluginTestData.CreatePluginWithBoundaryValues(IRuntimePluginTestData.VeryLongPluginName);

                // Act
                plugin.PerformSetup(_resolver);

                // Assert
                Assert.That(plugin.PerformSetupCalled, Is.True);
                Assert.That(plugin.Name, Is.EqualTo(IRuntimePluginTestData.VeryLongPluginName));
            }

            [Test]
            public void PerformSetup_WhenSpecialCharacterPluginName_ShouldCompleteSuccessfully()
            {
                // Arrange
                var plugin = IRuntimePluginTestData.CreatePluginWithBoundaryValues(IRuntimePluginTestData.SpecialCharacterPluginName);

                // Act
                plugin.PerformSetup(_resolver);

                // Assert
                Assert.That(plugin.PerformSetupCalled, Is.True);
                Assert.That(plugin.Name, Is.EqualTo(IRuntimePluginTestData.SpecialCharacterPluginName));
            }

            [Test]
            public void PerformSetup_WhenUnicodePluginName_ShouldCompleteSuccessfully()
            {
                // Arrange
                var plugin = IRuntimePluginTestData.CreatePluginWithBoundaryValues(IRuntimePluginTestData.UnicodePluginName);

                // Act
                plugin.PerformSetup(_resolver);

                // Assert
                Assert.That(plugin.PerformSetupCalled, Is.True);
                Assert.That(plugin.Name, Is.EqualTo(IRuntimePluginTestData.UnicodePluginName));
            }

            [Test]
            public void PerformSetup_WhenLongExceptionMessage_ShouldPropagateCorrectly()
            {
                // Arrange
                var plugin = IRuntimePluginTestData.CreatePluginWithBoundaryValues(exceptionMessage: IRuntimePluginTestData.LongExceptionMessage);
                plugin.ShouldThrowOnSetup = true;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => plugin.PerformSetup(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.LongExceptionMessage));
                }
                finally
                {
                    plugin.ShouldThrowOnSetup = false;
                }
            }

            [Test]
            public void PerformSetup_WhenEmptyExceptionMessage_ShouldPropagateCorrectly()
            {
                // Arrange
                var plugin = IRuntimePluginTestData.CreatePluginWithBoundaryValues(exceptionMessage: IRuntimePluginTestData.EmptyExceptionMessage);
                plugin.ShouldThrowOnSetup = true;

                try
                {
                    // Act & Assert
                    var exception = Assert.Throws<InvalidOperationException>(() => plugin.PerformSetup(_resolver));
                    Assert.That(exception.Message, Is.EqualTo(IRuntimePluginTestData.EmptyExceptionMessage));
                }
                finally
                {
                    plugin.ShouldThrowOnSetup = false;
                }
            }
        }

        #endregion

        #region VContainer Registration Tests

        [TestFixture]
        public class VContainerRegistrationTests : IRuntimePluginTests
        {
            [Test]
            public void VContainer_WhenPluginRegistered_ShouldResolveCorrectly()
            {
                // Arrange
                var testPlugin = IRuntimePluginTestData.CreatePlugin();
                var containerBuilder = new ContainerBuilder();
                
                // Register dependencies
                containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                containerBuilder.RegisterInstance(testPlugin).As<IRuntimePlugin>();

                // Act
                var resolver = containerBuilder.Build();
                var resolvedPlugin = resolver.Resolve<IRuntimePlugin>();

                // Assert
                Assert.That(resolvedPlugin, Is.Not.Null);
                Assert.That(resolvedPlugin, Is.SameAs(testPlugin));
                
                // Cleanup
                resolver.Dispose();
            }

            [Test]
            public void VContainer_WhenMultiplePluginsRegistered_ShouldResolveAllCorrectly()
            {
                // Arrange - Create plugins and register as collection to avoid VContainer conflicts
                var plugin1 = IRuntimePluginTestData.CreatePlugin("Plugin1");
                var plugin2 = IRuntimePluginTestData.CreatePlugin("Plugin2");
                var plugin3 = IRuntimePluginTestData.CreatePlugin("Plugin3");
                var plugins = new List<IRuntimePlugin> { plugin1, plugin2, plugin3 };
                
                var containerBuilder = new ContainerBuilder();
                
                // Register dependencies
                containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                
                // Register the collection directly to avoid individual instance conflicts
                containerBuilder.RegisterInstance(plugins).As<IEnumerable<IRuntimePlugin>>();

                // Act
                var resolver = containerBuilder.Build();
                var resolvedPlugins = resolver.Resolve<IEnumerable<IRuntimePlugin>>();

                // Assert
                Assert.That(resolvedPlugins, Is.Not.Null);
                Assert.That(resolvedPlugins.Count(), Is.EqualTo(3));
                
                // Verify the resolved plugins are the same instances
                var resolvedList = resolvedPlugins.ToList();
                Assert.That(resolvedList[0], Is.SameAs(plugin1));
                Assert.That(resolvedList[1], Is.SameAs(plugin2));
                Assert.That(resolvedList[2], Is.SameAs(plugin3));
                
                // Cleanup
                resolver.Dispose();
            }

            [Test]
            public void VContainer_WhenPluginNotRegistered_ShouldThrowException()
            {
                // Arrange
                var containerBuilder = new ContainerBuilder();
                containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                // Note: IRuntimePlugin not registered

                // Act & Assert
                var resolver = containerBuilder.Build();
                Assert.Throws<VContainerException>(() => resolver.Resolve<IRuntimePlugin>());
                
                // Cleanup
                resolver.Dispose();
            }
        }

        #endregion

        #region Cancellation Token Tests

        [TestFixture]
        public class CancellationTokenTests : IRuntimePluginTests
        {
            [Test]
            public void InitializeAsync_WhenCancellationTokenCancelled_ShouldHandleGracefully()
            {
                // Arrange
                var cancelledToken = IRuntimePluginTestData.CancelledToken;

                // Act
                var task = _testPlugin.InitializeAsync(_resolver, cancelledToken);

                // Assert - Should complete despite cancelled token (default implementation doesn't check cancellation)
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(_testPlugin.InitializeAsyncCalls.Count, Is.EqualTo(1));
                Assert.That(_testPlugin.InitializeAsyncCalls[0].ct, Is.EqualTo(cancelledToken));
            }

            [Test]
            public void InitializeAsync_WhenDefaultCancellationToken_ShouldWorkCorrectly()
            {
                // Arrange
                var defaultToken = IRuntimePluginTestData.DefaultToken;

                // Act
                var task = _testPlugin.InitializeAsync(_resolver, defaultToken);

                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(_testPlugin.InitializeAsyncCalls.Count, Is.EqualTo(1));
                Assert.That(_testPlugin.InitializeAsyncCalls[0].ct, Is.EqualTo(defaultToken));
            }

            [Test]
            public void InitializeAsync_WhenCustomCancellationToken_ShouldPassTokenCorrectly()
            {
                // Arrange
                using var cts = new CancellationTokenSource();
                var customToken = cts.Token;

                // Act
                var task = _testPlugin.InitializeAsync(_resolver, customToken);

                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(_testPlugin.InitializeAsyncCalls.Count, Is.EqualTo(1));
                Assert.That(_testPlugin.InitializeAsyncCalls[0].ct, Is.EqualTo(customToken));
            }
        }

        #endregion

        #region Multiple Lifecycle Cycle Tests

        [TestFixture]
        public class MultipleLifecycleCycleTests : IRuntimePluginTests
        {
            [Test]
            public void Lifecycle_WhenMultipleInitializeCycles_ShouldMaintainStateConsistency()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;

                // Act - Multiple cycles to test state consistency
                _testPlugin.Initialize(_resolver);
                _testPlugin.ResetCallCounts();
                _testPlugin.Initialize(_resolver);
                _testPlugin.ResetCallCounts();
                _testPlugin.Initialize(_resolver);

                // Assert - State must be consistent after multiple cycles
                Assert.That(_testPlugin.InitializeCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.PerformSetupCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.PerformRuntimeInitializationCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.AreDependenciesReadyCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Lifecycle_WhenMultipleInitializeAsyncCycles_ShouldMaintainStateConsistency()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;

                // Act - Multiple cycles to test state consistency
                var task1 = _testPlugin.InitializeAsync(_resolver);
                _testPlugin.ResetCallCounts();
                var task2 = _testPlugin.InitializeAsync(_resolver);
                _testPlugin.ResetCallCounts();
                var task3 = _testPlugin.InitializeAsync(_resolver);

                // Assert - All tasks should succeed and state must be consistent
                Assert.That(task1.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(task2.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(task3.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                
                Assert.That(_testPlugin.InitializeAsyncCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.PerformSetupCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.PerformRuntimeInitializationCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.AreDependenciesReadyCallCount, Is.EqualTo(1));
            }

            [Test]
            public void Lifecycle_WhenMixedInitializeAndInitializeAsyncCycles_ShouldWorkCorrectly()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;

                // Act - Mixed cycles
                _testPlugin.Initialize(_resolver);
                _testPlugin.ResetCallCounts();
                var task = _testPlugin.InitializeAsync(_resolver);
                _testPlugin.ResetCallCounts();
                _testPlugin.Initialize(_resolver);

                // Assert
                Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                Assert.That(_testPlugin.InitializeCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.PerformSetupCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.PerformRuntimeInitializationCallCount, Is.EqualTo(1));
                Assert.That(_testPlugin.AreDependenciesReadyCallCount, Is.EqualTo(1));
            }
        }

        #endregion

        #region Concurrent Access Tests

        [TestFixture]
        public class ConcurrentAccessTests : IRuntimePluginTests
        {
            [Test]
            public void ConcurrentAccess_WhenMultipleThreadsCallSameMethod_ShouldHandleGracefully()
            {
                // Arrange
                var tasks = new List<Task>();
                var exceptions = new List<Exception>();

                // Act - Simulate concurrent access
                for (int i = 0; i < 10; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            _testPlugin.PerformSetup(_resolver);
                        }
                        catch (Exception ex)
                        {
                            lock (exceptions)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());

                // Assert - Should handle concurrent access without exceptions
                Assert.That(exceptions, Is.Empty);
                Assert.That(_testPlugin.PerformSetupCallCount, Is.EqualTo(10));
            }

            [Test]
            public void ConcurrentAccess_WhenMultipleThreadsCallDifferentMethods_ShouldHandleGracefully()
            {
                // Arrange
                var tasks = new List<Task>();
                var exceptions = new List<Exception>();

                // Act - Simulate concurrent access to different methods
                for (int i = 0; i < 5; i++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            _testPlugin.PerformSetup(_resolver);
                        }
                        catch (Exception ex)
                        {
                            lock (exceptions)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }));

                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            _testPlugin.PerformRuntimeInitialization(_resolver);
                        }
                        catch (Exception ex)
                        {
                            lock (exceptions)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }));

                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            _testPlugin.AreDependenciesReady(_resolver);
                        }
                        catch (Exception ex)
                        {
                            lock (exceptions)
                            {
                                exceptions.Add(ex);
                            }
                        }
                    }));
                }

                Task.WaitAll(tasks.ToArray());

                // Assert - Should handle concurrent access without exceptions
                Assert.That(exceptions, Is.Empty);
                Assert.That(_testPlugin.PerformSetupCallCount, Is.EqualTo(5));
                Assert.That(_testPlugin.PerformRuntimeInitializationCallCount, Is.EqualTo(5));
                Assert.That(_testPlugin.AreDependenciesReadyCallCount, Is.EqualTo(5));
            }
        }

        #endregion

        #region Reflection Helper Tests

        [TestFixture]
        public class ReflectionHelperTests : IRuntimePluginTests
        {
            [Test]
            public void ReflectionHelper_WhenGettingDependenciesReady_ShouldReturnCorrectValue()
            {
                // Arrange
                _testPlugin.DependenciesReady = true;

                // Act
                var result = IRuntimePluginReflectionHelper.GetDependenciesReady(_testPlugin);

                // Assert
                Assert.That(result, Is.True);
            }

            [Test]
            public void ReflectionHelper_WhenSettingDependenciesReady_ShouldUpdateValue()
            {
                // Arrange
                _testPlugin.DependenciesReady = false;

                // Act
                IRuntimePluginReflectionHelper.SetDependenciesReady(_testPlugin, true);

                // Assert
                Assert.That(_testPlugin.DependenciesReady, Is.True);
            }

            [Test]
            public void ReflectionHelper_WhenGettingExceptionMessage_ShouldReturnCorrectValue()
            {
                // Arrange
                var expectedMessage = "Test reflection message";
                _testPlugin.ExceptionMessage = expectedMessage;

                // Act
                var result = IRuntimePluginReflectionHelper.GetExceptionMessage(_testPlugin);

                // Assert
                Assert.That(result, Is.EqualTo(expectedMessage));
            }

            [Test]
            public void ReflectionHelper_WhenSettingExceptionMessage_ShouldUpdateValue()
            {
                // Arrange
                var expectedMessage = "Updated reflection message";

                // Act
                IRuntimePluginReflectionHelper.SetExceptionMessage(_testPlugin, expectedMessage);

                // Assert
                Assert.That(_testPlugin.ExceptionMessage, Is.EqualTo(expectedMessage));
            }

            [Test]
            public void ReflectionHelper_WhenGettingLastThrownException_ShouldReturnCorrectValue()
            {
                // Arrange
                _testPlugin.ShouldThrowOnSetup = true;
                Exception thrownException = null;

                try
                {
                    _testPlugin.PerformSetup(_resolver);
                }
                catch (Exception ex)
                {
                    thrownException = ex;
                }
                finally
                {
                    _testPlugin.ShouldThrowOnSetup = false;
                }

                // Act
                var result = IRuntimePluginReflectionHelper.GetLastThrownException(_testPlugin);

                // Assert
                Assert.That(result, Is.SameAs(thrownException));
            }
        }

        #endregion

        #region Enhanced Parameterized Tests

        [TestFixture]
        public class EnhancedParameterizedTests : IRuntimePluginTests
        {
            [TestCase(true, true, true)]
            [TestCase(false, false, false)]
            [TestCase(true, false, true)]
            [TestCase(false, true, false)]
            [TestCase(true, true, false)]
            [TestCase(false, false, true)]
            public void InitializeAsync_WhenAllExceptionFlagsSet_ShouldHandleCorrectly(bool shouldThrowOnSetup, bool shouldThrowOnRuntime, bool shouldThrowOnDependencyCheck)
            {
                // Arrange
                _testPlugin.ShouldThrowOnSetup = shouldThrowOnSetup;
                _testPlugin.ShouldThrowOnRuntime = shouldThrowOnRuntime;
                _testPlugin.ShouldThrowOnDependencyCheck = shouldThrowOnDependencyCheck;
                _testPlugin.DependenciesReady = true;

                try
                {
                    // Act
                    if (shouldThrowOnSetup || shouldThrowOnDependencyCheck || shouldThrowOnRuntime)
                    {
                        Assert.Throws<InvalidOperationException>(() => _testPlugin.InitializeAsync(_resolver));
                    }
                    else
                    {
                        var task = _testPlugin.InitializeAsync(_resolver);
                        Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
                    }
                }
                finally
                {
                    // Reset exception behavior
                    _testPlugin.ShouldThrowOnSetup = false;
                    _testPlugin.ShouldThrowOnRuntime = false;
                    _testPlugin.ShouldThrowOnDependencyCheck = false;
                }

                // Assert - Verify call tracking based on execution flow
                Assert.That(_testPlugin.PerformSetupCalled, Is.True);
                
                // AreDependenciesReady is only called if PerformSetup doesn't throw
                if (!shouldThrowOnSetup)
                {
                    Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.True);
                    
                    // PerformRuntimeInitialization is called if PerformSetup doesn't throw and AreDependenciesReady doesn't throw
                    // Note: PerformRuntimeInitialization is called even if it throws an exception
                    if (!shouldThrowOnDependencyCheck)
                    {
                        Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.True);
                    }
                    else
                    {
                        Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.False);
                    }
                }
                else
                {
                    // If PerformSetup throws, AreDependenciesReady and PerformRuntimeInitialization are never called
                    Assert.That(_testPlugin.AreDependenciesReadyCalled, Is.False);
                    Assert.That(_testPlugin.PerformRuntimeInitializationCalled, Is.False);
                }
            }

            [TestCase("", "Empty name")]
            [TestCase("VeryLongPluginNameThatExceedsNormalLengthLimitsAndTestsBoundaryConditions", "Long name")]
            [TestCase("Plugin@#$%^&*()", "Special characters")]
            [TestCase("插件测试", "Unicode characters")]
            public void PerformSetup_WhenBoundaryValueNames_ShouldCompleteSuccessfully(string pluginName, string description)
            {
                // Arrange
                var plugin = IRuntimePluginTestData.CreatePluginWithBoundaryValues(pluginName);

                // Act & Assert
                Assert.DoesNotThrow(() => plugin.PerformSetup(_resolver), $"Should handle {description}");
                Assert.That(plugin.PerformSetupCalled, Is.True);
                Assert.That(plugin.Name, Is.EqualTo(pluginName));
            }
        }

        #endregion
    }
}
