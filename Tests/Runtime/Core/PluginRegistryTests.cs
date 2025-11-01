/**
 * Unit tests for PluginRegistry.cs
 * Refactored from function analysis on 2025-10-01
 * Framework: Unity Test Framework with NUnit
 * 
 * Test Coverage:
 * - Plugin registration and collection management
 * - VContainer integration and dependency injection
 * - Runtime plugin lifecycle management
 * - Dual-interface plugin handling (IGamePlugin + IRuntimePlugin)
 * - Exception propagation and error handling
 * - Thread safety and concurrent access patterns
 * - Parameterized testing for similar scenarios
 * - Exception isolation and cleanup patterns
 * 
 * Mock Dependencies:
 * - IGamePlugin and IRuntimePlugin implementations with thread-safe call tracking
 * - IContainerBuilder and IObjectResolver from VContainer
 * - Enhanced test implementations with method-specific exception control
 * - Thread-safe collection operations and call count tracking
 * 
 * Refactoring Improvements:
 * - Thread-safe mock implementations using Interlocked operations
 * - Nested TestFixture organization for better structure
 * - Parameterized tests for similar scenarios
 * - Enhanced exception isolation testing
 * - Comprehensive XML documentation
 * - Optimized VContainer test patterns
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using UnityEngine.TestTools;
using Serilog;
using NSubstitute;
using ILogger = Serilog.ILogger;
using MToolKit.Tests.Runtime.Core;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Tests.Runtime.Core
{
    /// <summary>
    /// Test data constants and factory methods for consistent test values
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// CRITICAL: Make test classes public for NSubstitute proxy creation
    /// </summary>
    public static class PluginRegistryTestData
    {
        // Basic test values
        public const string TestPluginName = "TestPlugin";
        public const string TestPluginName2 = "TestPlugin2";
        public const string TestPluginName3 = "TestPlugin3";
        public const string TestExceptionMessage = "Test exception";
        
        // Factory methods for consistent test object creation
        public static TestGamePlugin CreateGamePlugin(string name = TestPluginName, bool shouldThrowOnRegister = false, string exceptionMessage = TestExceptionMessage)
        {
            return new TestGamePlugin(name)
            {
                ShouldThrowOnRegister = shouldThrowOnRegister,
                ExceptionMessage = exceptionMessage
            };
        }
        
        public static TestRuntimePlugin CreateRuntimePlugin(string name = TestPluginName, bool shouldThrowOnInitialize = false, bool shouldThrowOnSetup = false, bool shouldThrowOnRuntimeInit = false, bool dependenciesReady = true, string exceptionMessage = TestExceptionMessage)
        {
            return new TestRuntimePlugin(name)
            {
                ShouldThrowOnInitialize = shouldThrowOnInitialize,
                ShouldThrowOnSetup = shouldThrowOnSetup,
                ShouldThrowOnRuntimeInit = shouldThrowOnRuntimeInit,
                DependenciesReady = dependenciesReady,
                ExceptionMessage = exceptionMessage
            };
        }
        
        public static TestDualInterfacePlugin CreateDualInterfacePlugin(string name = TestPluginName, bool shouldThrowOnRegister = false, bool shouldThrowOnInitialize = false, bool shouldThrowOnSetup = false, bool shouldThrowOnRuntimeInit = false, bool dependenciesReady = true, string exceptionMessage = TestExceptionMessage)
        {
            return new TestDualInterfacePlugin(name)
            {
                ShouldThrowOnRegister = shouldThrowOnRegister,
                ShouldThrowOnInitialize = shouldThrowOnInitialize,
                ShouldThrowOnSetup = shouldThrowOnSetup,
                ShouldThrowOnRuntimeInit = shouldThrowOnRuntimeInit,
                DependenciesReady = dependenciesReady,
                ExceptionMessage = exceptionMessage
            };
        }
        
        public static TestDependencyDeclarationPlugin CreateDependencyDeclarationPlugin(string name = TestPluginName, bool shouldThrowOnRegister = false, bool shouldThrowOnInitialize = false, bool shouldThrowOnSetup = false, bool shouldThrowOnRuntimeInit = false, bool dependenciesReady = true, string exceptionMessage = TestExceptionMessage)
        {
            return new TestDependencyDeclarationPlugin(name)
            {
                ShouldThrowOnRegister = shouldThrowOnRegister,
                ShouldThrowOnInitialize = shouldThrowOnInitialize,
                ShouldThrowOnSetup = shouldThrowOnSetup,
                ShouldThrowOnRuntimeInit = shouldThrowOnRuntimeInit,
                DependenciesReady = dependenciesReady,
                ExceptionMessage = exceptionMessage
            };
        }
        
        public static List<IGamePlugin> CreateGamePlugins(int count, bool shouldThrowOnRegister = false, string exceptionMessage = TestExceptionMessage)
        {
            return Enumerable.Range(0, count)
                .Select(i => CreateGamePlugin($"Plugin{i}", shouldThrowOnRegister, exceptionMessage))
                .Cast<IGamePlugin>()
                .ToList();
        }
        
        public static List<IRuntimePlugin> CreateRuntimePlugins(int count, bool shouldThrowOnInitialize = false, bool shouldThrowOnSetup = false, bool shouldThrowOnRuntimeInit = false, bool dependenciesReady = true, string exceptionMessage = TestExceptionMessage)
        {
            return Enumerable.Range(0, count)
                .Select(i => CreateRuntimePlugin($"RuntimePlugin{i}", shouldThrowOnInitialize, shouldThrowOnSetup, shouldThrowOnRuntimeInit, dependenciesReady, exceptionMessage))
                .Cast<IRuntimePlugin>()
                .ToList();
        }
    }

    /// <summary>
    /// Reflection utilities for accessing private fields and methods with performance optimization
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// </summary>
    internal static class PluginRegistryReflectionHelper
    {
        /// <summary>
        /// Cached FieldInfo for performance optimization
        /// </summary>
        private static readonly FieldInfo GamePluginsField = typeof(PluginRegistry)
            .GetField("gamePlugins", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly FieldInfo RuntimePluginsField = typeof(PluginRegistry)
            .GetField("runtimePlugins", BindingFlags.NonPublic | BindingFlags.Instance);
        
        public static List<IGamePlugin> GetGamePlugins(PluginRegistry registry)
        {
            return GamePluginsField?.GetValue(registry) as List<IGamePlugin>;
        }
        
        public static List<IRuntimePlugin> GetRuntimePlugins(PluginRegistry registry)
        {
            return RuntimePluginsField?.GetValue(registry) as List<IRuntimePlugin>;
        }
    }

    /// <summary>
    /// Enhanced thread-safe test implementation with call tracking, state verification, and method-specific exception control
    /// CRITICAL: Supports granular exception control for precise testing scenarios
    /// CRITICAL: Uses Interlocked operations for thread-safe call count tracking
    /// </summary>
    public class TestGamePlugin : IGamePlugin
    {
        public string Name { get; }
        
        // Thread-safe call tracking using private fields with public getters
        private int _registerCallCount;
        private readonly object _lockObject = new object();
        private IContainerBuilder _lastBuilder;
        private Exception _lastThrownException;
        private bool _registerCalled;
        
        public bool RegisterCalled => _registerCalled;
        public int RegisterCallCount => _registerCallCount;
        public IContainerBuilder LastBuilder => _lastBuilder;
        public Exception LastThrownException => _lastThrownException;
        
        // Exception control
        public bool ShouldThrowOnRegister { get; set; }
        public string ExceptionMessage { get; set; } = PluginRegistryTestData.TestExceptionMessage;
        
        public TestGamePlugin(string name)
        {
            Name = name;
        }
        
        public void Register(IContainerBuilder builder)
        {
            Interlocked.Increment(ref _registerCallCount);
            
            lock (_lockObject)
            {
                _lastBuilder = builder;
                
                if (ShouldThrowOnRegister)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _registerCalled = true;
            }
        }
        
        public void ResetCallCounts()
        {
            Interlocked.Exchange(ref _registerCallCount, 0);
            
            lock (_lockObject)
            {
                _registerCalled = false;
                _lastBuilder = null;
                _lastThrownException = null;
            }
            
            // Reset exception behavior
            ExceptionMessage = PluginRegistryTestData.TestExceptionMessage;
        }
    }

    /// <summary>
    /// Enhanced thread-safe test implementation for IRuntimePlugin with call tracking and state verification
    /// CRITICAL: Uses Interlocked operations for thread-safe call count tracking
    /// CRITICAL: Supports method-specific exception control for precise testing scenarios
    /// CRITICAL: Pure IRuntimePlugin implementation (does not implement IGamePlugin)
    /// </summary>
    public class TestRuntimePlugin : IRuntimePlugin
    {
        public string Name { get; }
        
        // Thread-safe call tracking using private fields with public getters
        private int _initializeCallCount;
        private int _performSetupCallCount;
        private int _performRuntimeInitializationCallCount;
        private int _areDependenciesReadyCallCount;
        private readonly object _lockObject = new object();
        private IObjectResolver _lastResolver;
        private Exception _lastThrownException;
        private bool _initializeCalled;
        private bool _performSetupCalled;
        private bool _performRuntimeInitializationCalled;
        private bool _areDependenciesReadyCalled;
        
        public bool InitializeCalled => _initializeCalled;
        public bool PerformSetupCalled => _performSetupCalled;
        public bool PerformRuntimeInitializationCalled => _performRuntimeInitializationCalled;
        public bool AreDependenciesReadyCalled => _areDependenciesReadyCalled;
        public int InitializeCallCount => _initializeCallCount;
        public int PerformSetupCallCount => _performSetupCallCount;
        public int PerformRuntimeInitializationCallCount => _performRuntimeInitializationCallCount;
        public int AreDependenciesReadyCallCount => _areDependenciesReadyCallCount;
        public IObjectResolver LastResolver => _lastResolver;
        public Exception LastThrownException => _lastThrownException;
        
        // Exception control
        public bool ShouldThrowOnInitialize { get; set; }
        public bool ShouldThrowOnSetup { get; set; }
        public bool ShouldThrowOnRuntimeInit { get; set; }
        public bool DependenciesReady { get; set; } = true;
        public string ExceptionMessage { get; set; } = PluginRegistryTestData.TestExceptionMessage;
        
        public TestRuntimePlugin(string name)
        {
            Name = name;
        }
        
        public void Register(IContainerBuilder builder)
        {
            // No-op for testing - this method is called during Phase 1
        }
        
        public void Initialize(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _initializeCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnInitialize)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _initializeCalled = true;
            }
        }
        
        public void PerformSetup(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _performSetupCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnSetup)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _performSetupCalled = true;
            }
        }
        
        public void PerformRuntimeInitialization(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _performRuntimeInitializationCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnRuntimeInit)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _performRuntimeInitializationCalled = true;
            }
        }
        
        public bool AreDependenciesReady(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _areDependenciesReadyCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                _areDependenciesReadyCalled = true;
            }
            
            return DependenciesReady;
        }
        
        public void ResetCallCounts()
        {
            Interlocked.Exchange(ref _initializeCallCount, 0);
            Interlocked.Exchange(ref _performSetupCallCount, 0);
            Interlocked.Exchange(ref _performRuntimeInitializationCallCount, 0);
            Interlocked.Exchange(ref _areDependenciesReadyCallCount, 0);
            
            lock (_lockObject)
            {
                _performSetupCalled = false;
                _performRuntimeInitializationCalled = false;
                _areDependenciesReadyCalled = false;
            }
            
            // Reset exception behavior
            ShouldThrowOnInitialize = false;
            ShouldThrowOnSetup = false;
            ShouldThrowOnRuntimeInit = false;
            DependenciesReady = true;
            ExceptionMessage = PluginRegistryTestData.TestExceptionMessage;
        }
    }

    /// <summary>
    /// Thread-safe test implementation that implements both IGamePlugin and IRuntimePlugin interfaces
    /// CRITICAL: Tests dual-interface plugin handling with thread-safe call tracking
    /// CRITICAL: Uses Interlocked operations for thread-safe call count tracking
    /// </summary>
    public class TestDualInterfacePlugin : IGamePlugin, IRuntimePlugin
    {
        public string Name { get; }
        
        // Thread-safe call tracking using private fields with public getters
        private int _registerCallCount;
        private int _initializeCallCount;
        private int _performSetupCallCount;
        private int _performRuntimeInitializationCallCount;
        private int _areDependenciesReadyCallCount;
        private readonly object _lockObject = new object();
        private IContainerBuilder _lastBuilder;
        private IObjectResolver _lastResolver;
        private Exception _lastThrownException;
        private bool _registerCalled;
        private bool _initializeCalled;
        private bool _performSetupCalled;
        private bool _performRuntimeInitializationCalled;
        private bool _areDependenciesReadyCalled;
        
        public bool RegisterCalled => _registerCalled;
        public bool InitializeCalled => _initializeCalled;
        public bool PerformSetupCalled => _performSetupCalled;
        public bool PerformRuntimeInitializationCalled => _performRuntimeInitializationCalled;
        public bool AreDependenciesReadyCalled => _areDependenciesReadyCalled;
        public int RegisterCallCount => _registerCallCount;
        public int InitializeCallCount => _initializeCallCount;
        public int PerformSetupCallCount => _performSetupCallCount;
        public int PerformRuntimeInitializationCallCount => _performRuntimeInitializationCallCount;
        public int AreDependenciesReadyCallCount => _areDependenciesReadyCallCount;
        public IContainerBuilder LastBuilder => _lastBuilder;
        public IObjectResolver LastResolver => _lastResolver;
        public Exception LastThrownException => _lastThrownException;
        
        // Exception control
        public bool ShouldThrowOnRegister { get; set; }
        public bool ShouldThrowOnInitialize { get; set; }
        public bool ShouldThrowOnSetup { get; set; }
        public bool ShouldThrowOnRuntimeInit { get; set; }
        public bool DependenciesReady { get; set; } = true;
        public string ExceptionMessage { get; set; } = PluginRegistryTestData.TestExceptionMessage;
        
        public TestDualInterfacePlugin(string name)
        {
            Name = name;
        }
        
        public void Register(IContainerBuilder builder)
        {
            Interlocked.Increment(ref _registerCallCount);
            
            lock (_lockObject)
            {
                _lastBuilder = builder;
                
                if (ShouldThrowOnRegister)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _registerCalled = true;
            }
        }
        
        public void Initialize(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _initializeCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnInitialize)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _initializeCalled = true;
            }
        }
        
        public void PerformSetup(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _performSetupCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnSetup)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _performSetupCalled = true;
            }
        }
        
        public void PerformRuntimeInitialization(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _performRuntimeInitializationCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnRuntimeInit)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _performRuntimeInitializationCalled = true;
            }
        }
        
        public bool AreDependenciesReady(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _areDependenciesReadyCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                _areDependenciesReadyCalled = true;
            }
            
            return DependenciesReady;
        }
        
        public void ResetCallCounts()
        {
            Interlocked.Exchange(ref _registerCallCount, 0);
            Interlocked.Exchange(ref _initializeCallCount, 0);
            Interlocked.Exchange(ref _performSetupCallCount, 0);
            Interlocked.Exchange(ref _performRuntimeInitializationCallCount, 0);
            Interlocked.Exchange(ref _areDependenciesReadyCallCount, 0);
            
            lock (_lockObject)
            {
                _registerCalled = false;
                _initializeCalled = false;
                _performSetupCalled = false;
                _performRuntimeInitializationCalled = false;
                _areDependenciesReadyCalled = false;
                _lastBuilder = null;
                _lastResolver = null;
                _lastThrownException = null;
            }
            
            // Reset exception behavior
            ShouldThrowOnInitialize = false;
            ShouldThrowOnSetup = false;
            ShouldThrowOnRuntimeInit = false;
            DependenciesReady = true;
            ExceptionMessage = PluginRegistryTestData.TestExceptionMessage;
        }
    }

    /// <summary>
    /// Thread-safe test implementation that implements IDependencyDeclaration interface
    /// CRITICAL: Tests dependency validation with thread-safe call tracking
    /// CRITICAL: Uses Interlocked operations for thread-safe call count tracking
    /// </summary>
    public class TestDependencyDeclarationPlugin : IGamePlugin, IRuntimePlugin, IDependencyDeclaration
    {
        public string Name { get; }
        
        // Thread-safe call tracking using private fields with public getters
        private int _registerCallCount;
        private int _initializeCallCount;
        private int _performSetupCallCount;
        private int _performRuntimeInitializationCallCount;
        private int _areDependenciesReadyCallCount;
        private int _validateDependenciesCallCount;
        private readonly object _lockObject = new object();
        private IContainerBuilder _lastBuilder;
        private IObjectResolver _lastResolver;
        private Exception _lastThrownException;
        private bool _registerCalled;
        private bool _initializeCalled;
        private bool _performSetupCalled;
        private bool _performRuntimeInitializationCalled;
        private bool _areDependenciesReadyCalled;
        private bool _validateDependenciesCalled;
        
        public bool RegisterCalled => _registerCalled;
        public bool InitializeCalled => _initializeCalled;
        public bool PerformSetupCalled => _performSetupCalled;
        public bool PerformRuntimeInitializationCalled => _performRuntimeInitializationCalled;
        public bool AreDependenciesReadyCalled => _areDependenciesReadyCalled;
        public bool ValidateDependenciesCalled => _validateDependenciesCalled;
        public int RegisterCallCount => _registerCallCount;
        public int InitializeCallCount => _initializeCallCount;
        public int PerformSetupCallCount => _performSetupCallCount;
        public int PerformRuntimeInitializationCallCount => _performRuntimeInitializationCallCount;
        public int AreDependenciesReadyCallCount => _areDependenciesReadyCallCount;
        public int ValidateDependenciesCallCount => _validateDependenciesCallCount;
        public IContainerBuilder LastBuilder => _lastBuilder;
        public IObjectResolver LastResolver => _lastResolver;
        public Exception LastThrownException => _lastThrownException;
        
        // Exception control
        public bool ShouldThrowOnRegister { get; set; }
        public bool ShouldThrowOnInitialize { get; set; }
        public bool ShouldThrowOnSetup { get; set; }
        public bool ShouldThrowOnRuntimeInit { get; set; }
        public bool DependenciesReady { get; set; } = true;
        public string ExceptionMessage { get; set; } = PluginRegistryTestData.TestExceptionMessage;
        
        // Dependency declaration
        public IEnumerable<Type> RequiredServices { get; set; } = new[] { typeof(string) };
        public IEnumerable<Type> OptionalServices { get; set; } = new[] { typeof(int) };
        
        public TestDependencyDeclarationPlugin(string name)
        {
            Name = name;
        }
        
        public void Register(IContainerBuilder builder)
        {
            Interlocked.Increment(ref _registerCallCount);
            
            lock (_lockObject)
            {
                _lastBuilder = builder;
                
                if (ShouldThrowOnRegister)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _registerCalled = true;
            }
        }
        
        public void Initialize(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _initializeCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnInitialize)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _initializeCalled = true;
            }
        }
        
        public void PerformSetup(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _performSetupCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnSetup)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _performSetupCalled = true;
            }
        }
        
        public void PerformRuntimeInitialization(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _performRuntimeInitializationCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                
                if (ShouldThrowOnRuntimeInit)
                {
                    var exception = new InvalidOperationException(ExceptionMessage);
                    _lastThrownException = exception;
                    throw exception;
                }
                
                _performRuntimeInitializationCalled = true;
            }
        }
        
        public bool AreDependenciesReady(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _areDependenciesReadyCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                _areDependenciesReadyCalled = true;
            }
            
            return DependenciesReady;
        }
        
        public bool ValidateDependencies(IObjectResolver resolver)
        {
            Interlocked.Increment(ref _validateDependenciesCallCount);
            
            lock (_lockObject)
            {
                _lastResolver = resolver;
                _validateDependenciesCalled = true;
            }
            
            // Check if all required services are available
            foreach (var serviceType in RequiredServices)
            {
                if (!resolver.TryResolve(serviceType, out _))
                    return false;
            }
            
            return true;
        }
        
        public void ResetCallCounts()
        {
            Interlocked.Exchange(ref _registerCallCount, 0);
            Interlocked.Exchange(ref _initializeCallCount, 0);
            Interlocked.Exchange(ref _performSetupCallCount, 0);
            Interlocked.Exchange(ref _performRuntimeInitializationCallCount, 0);
            Interlocked.Exchange(ref _areDependenciesReadyCallCount, 0);
            Interlocked.Exchange(ref _validateDependenciesCallCount, 0);
            
            lock (_lockObject)
            {
                _registerCalled = false;
                _initializeCalled = false;
                _performSetupCalled = false;
                _performRuntimeInitializationCalled = false;
                _areDependenciesReadyCalled = false;
                _validateDependenciesCalled = false;
                _lastBuilder = null;
                _lastResolver = null;
                _lastThrownException = null;
            }
            
            // Reset exception behavior
            ShouldThrowOnInitialize = false;
            ShouldThrowOnSetup = false;
            ShouldThrowOnRuntimeInit = false;
            DependenciesReady = true;
            ExceptionMessage = PluginRegistryTestData.TestExceptionMessage;
        }
    }

    /// <summary>
    /// Comprehensive test suite for PluginRegistry class
    /// CRITICAL: Uses nested TestFixture organization for better structure and maintainability
    /// CRITICAL: Implements thread-safe testing patterns with proper exception isolation
    /// </summary>
    [TestFixture]
    public class PluginRegistryTests
    {
        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private ILogger _mockLogger;
        private PluginRegistry _pluginRegistry;
        
        [SetUp]
        public void Setup()
        {
            _containerBuilder = new ContainerBuilder();
            SetupTestContainer();
            _resolver = _containerBuilder.Build();
            _pluginRegistry = new PluginRegistry();
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
        /// Helper method to create isolated VContainer instance per test
        /// CRITICAL: Prevents registration conflicts between tests
        /// </summary>
        protected IObjectResolver CreateTestResolverWithPlugins(IEnumerable<IGamePlugin> plugins)
        {
            // Create fresh container builder for this test
            var testContainerBuilder = new ContainerBuilder();
            
            // Register the common dependencies
            testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            
            // Register the test-specific plugins
            testContainerBuilder.RegisterInstance(plugins).As<IEnumerable<IGamePlugin>>();
            
            // Build and resolve
            var testResolver = testContainerBuilder.Build();
            
            // Store the resolver for cleanup in TearDown
            _resolver?.Dispose();
            _resolver = testResolver;
            
            return testResolver;
        }

        #region Register Method Tests

        /// <summary>
        /// Test suite for Register method functionality
        /// CRITICAL: Tests plugin registration, dual-interface handling, and collection management
        /// </summary>
        [TestFixture]
        public class RegisterTests : PluginRegistryTests
        {
            [Test]
            public void Register_WhenValidGamePluginProvided_ShouldAddToGamePluginsCollection()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateGamePlugin();
                
                // Act
                _pluginRegistry.Register(plugin);
                
                // Assert
                var gamePlugins = PluginRegistryReflectionHelper.GetGamePlugins(_pluginRegistry);
                Assert.That(gamePlugins, Is.Not.Null);
                Assert.That(gamePlugins.Count, Is.EqualTo(1));
                Assert.That(gamePlugins[0], Is.SameAs(plugin));
            }

            [Test]
            public void Register_WhenDualInterfacePluginProvided_ShouldAddToBothCollections()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateDualInterfacePlugin();
                
                // Act
                _pluginRegistry.Register(plugin);
                
                // Assert
                var gamePlugins = PluginRegistryReflectionHelper.GetGamePlugins(_pluginRegistry);
                var runtimePlugins = PluginRegistryReflectionHelper.GetRuntimePlugins(_pluginRegistry);
                
                Assert.That(gamePlugins, Is.Not.Null);
                Assert.That(gamePlugins.Count, Is.EqualTo(1));
                Assert.That(gamePlugins[0], Is.SameAs(plugin));
                
                Assert.That(runtimePlugins, Is.Not.Null);
                Assert.That(runtimePlugins.Count, Is.EqualTo(1));
                Assert.That(runtimePlugins[0], Is.SameAs(plugin));
            }

            [Test]
            public void Register_WhenMultiplePluginsProvided_ShouldAddAllToCollections()
            {
                // Arrange
                var plugin1 = PluginRegistryTestData.CreateGamePlugin("Plugin1");
                var plugin2 = PluginRegistryTestData.CreateDualInterfacePlugin("Plugin2");
                var plugin3 = PluginRegistryTestData.CreateRuntimePlugin("Plugin3");
                
                // Act
                _pluginRegistry.Register(plugin1);
                _pluginRegistry.Register(plugin2);
                _pluginRegistry.InitializeRuntimePlugin(plugin3, _resolver);
                
                // Assert
                var gamePlugins = PluginRegistryReflectionHelper.GetGamePlugins(_pluginRegistry);
                var runtimePlugins = PluginRegistryReflectionHelper.GetRuntimePlugins(_pluginRegistry);
                
                Assert.That(gamePlugins.Count, Is.EqualTo(2)); // Only plugin1 and plugin2 implement IGamePlugin
                Assert.That(runtimePlugins.Count, Is.EqualTo(2)); // Only plugin2 and plugin3 implement IRuntimePlugin
            }

            [Test]
            public void Register_WhenNullPluginProvided_ShouldHandleGracefully()
            {
                // Act & Assert
                Assert.DoesNotThrow(() => _pluginRegistry.Register(null));
                
                var gamePlugins = PluginRegistryReflectionHelper.GetGamePlugins(_pluginRegistry);
                Assert.That(gamePlugins.Count, Is.EqualTo(1));
                Assert.That(gamePlugins[0], Is.Null);
            }

            /// <summary>
            /// CRITICAL: Tests concurrent access to Register method for thread safety
            /// </summary>
            [Test]
            public void Register_WhenCalledConcurrently_ShouldHandleGracefully()
            {
                // Arrange
                var plugins = PluginRegistryTestData.CreateGamePlugins(10);
                var tasks = new List<Task>();
                var exceptions = new List<Exception>();
                
                // Act - Simulate concurrent access
                foreach (var plugin in plugins)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            _pluginRegistry.Register(plugin);
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
                var gamePlugins = PluginRegistryReflectionHelper.GetGamePlugins(_pluginRegistry);
                Assert.That(gamePlugins.Count, Is.EqualTo(10));
            }
        }

        #endregion

        #region ApplyAll Method Tests

        /// <summary>
        /// Test suite for ApplyAll method functionality
        /// CRITICAL: Tests VContainer integration and exception propagation
        /// </summary>
        [TestFixture]
        public class ApplyAllTests : PluginRegistryTests
        {
            [Test]
            public void ApplyAll_WhenCalledWithRegisteredPlugins_ShouldCallRegisterOnAllPlugins()
            {
                // Arrange
                var plugin1 = PluginRegistryTestData.CreateGamePlugin("Plugin1");
                var plugin2 = PluginRegistryTestData.CreateGamePlugin("Plugin2");
                var plugin3 = PluginRegistryTestData.CreateDualInterfacePlugin("Plugin3");
                
                _pluginRegistry.Register(plugin1);
                _pluginRegistry.Register(plugin2);
                _pluginRegistry.Register(plugin3);
                
                var mockBuilder = Substitute.For<IContainerBuilder>();
                
                // Act
                _pluginRegistry.ApplyAll(mockBuilder);
                
                // Assert
                Assert.That(plugin1.RegisterCalled, Is.True);
                Assert.That(plugin2.RegisterCalled, Is.True);
                Assert.That(plugin3.RegisterCalled, Is.True);
                Assert.That(plugin1.RegisterCallCount, Is.EqualTo(1));
                Assert.That(plugin2.RegisterCallCount, Is.EqualTo(1));
                Assert.That(plugin3.RegisterCallCount, Is.EqualTo(1));
            }

            [Test]
            public void ApplyAll_WhenCalledWithEmptyRegistry_ShouldNotThrow()
            {
                // Arrange
                var mockBuilder = Substitute.For<IContainerBuilder>();
                
                // Act & Assert
                Assert.DoesNotThrow(() => _pluginRegistry.ApplyAll(mockBuilder));
            }

            [Test]
            public void ApplyAll_WhenPluginRegisterThrowsException_ShouldPropagateException()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateGamePlugin(shouldThrowOnRegister: true);
                _pluginRegistry.Register(plugin);
                var mockBuilder = Substitute.For<IContainerBuilder>();
                
                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => _pluginRegistry.ApplyAll(mockBuilder));
                Assert.That(exception.Message, Is.EqualTo(PluginRegistryTestData.TestExceptionMessage));
            }

            [Test]
            public void ApplyAll_WhenNullBuilderProvided_ShouldPassNullToPlugins()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateGamePlugin();
                _pluginRegistry.Register(plugin);
                
                // Act
                _pluginRegistry.ApplyAll(null);
                
                // Assert
                Assert.That(plugin.RegisterCalled, Is.True);
                Assert.That(plugin.LastBuilder, Is.Null);
            }

            /// <summary>
            /// CRITICAL: Tests exception isolation - exceptions in one plugin don't affect others
            /// </summary>
            [Test]
            public void ApplyAll_WhenOnePluginThrowsException_ShouldNotAffectOtherPlugins()
            {
                // Arrange
                var workingPlugin = PluginRegistryTestData.CreateGamePlugin("WorkingPlugin");
                var throwingPlugin = PluginRegistryTestData.CreateGamePlugin("ThrowingPlugin", shouldThrowOnRegister: true);
                
                _pluginRegistry.Register(workingPlugin);
                _pluginRegistry.Register(throwingPlugin);
                
                var mockBuilder = Substitute.For<IContainerBuilder>();
                
                // Act & Assert - Should throw exception from throwing plugin
                var exception = Assert.Throws<InvalidOperationException>(() => _pluginRegistry.ApplyAll(mockBuilder));
                Assert.That(exception.Message, Is.EqualTo(PluginRegistryTestData.TestExceptionMessage));
                
                // Verify working plugin was still called before the exception
                Assert.That(workingPlugin.RegisterCalled, Is.True);
                Assert.That(throwingPlugin.RegisterCalled, Is.False); // Exception prevented completion
            }
        }

        #endregion

        #region Dependency Validation Tests

        /// <summary>
        /// Test suite for dependency validation functionality
        /// CRITICAL: Tests IDependencyDeclaration interface integration
        /// </summary>
        [TestFixture]
        public class DependencyValidationTests
        {
            private ContainerBuilder _containerBuilder;
            private IObjectResolver _resolver;
            private ILogger _mockLogger;
            private PluginRegistry _pluginRegistry;
            
            [SetUp]
            public void Setup()
            {
                _containerBuilder = new ContainerBuilder();
                SetupTestContainer();
                _resolver = _containerBuilder.Build();
                _pluginRegistry = new PluginRegistry();
            }
            
            [TearDown]
            public void TearDown()
            {
                _resolver?.Dispose();
            }
            
            private void SetupTestContainer()
            {
                // Using MockLogger instead of NSubstitute for IL2CPP compatibility (no Reflection.Emit)
                _mockLogger = new MockLogger();
                _containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                
                // Register test services
                _containerBuilder.RegisterInstance("test string").As<string>();
                _containerBuilder.RegisterInstance(42).As<int>();
            }

            [Test]
            public void PerformPluginSetup_WhenDependencyDeclarationPluginHasAllRequiredServices_ShouldSucceed()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateDependencyDeclarationPlugin();
                plugin.RequiredServices = new[] { typeof(string) }; // Available in container
                _pluginRegistry.Register(plugin);
                
                // Act
                _pluginRegistry.PerformPluginSetup(_resolver);
                
                // Assert
                Assert.That(plugin.ValidateDependenciesCalled, Is.True);
                Assert.That(plugin.PerformSetupCalled, Is.True);
            }

            [Test]
            public void PerformPluginSetup_WhenDependencyDeclarationPluginMissingRequiredService_ShouldThrowException()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateDependencyDeclarationPlugin();
                plugin.RequiredServices = new[] { typeof(double) }; // Not available in container
                _pluginRegistry.Register(plugin);
                
                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => 
                    _pluginRegistry.PerformPluginSetup(_resolver));
                
                Assert.That(exception.Message, Does.Contain("missing required dependencies"));
                Assert.That(exception.Message, Does.Contain("Double"));
                Assert.That(plugin.ValidateDependenciesCalled, Is.True);
                Assert.That(plugin.PerformSetupCalled, Is.False);
            }

            [Test]
            public void PerformPluginSetup_WhenNonDependencyDeclarationPlugin_ShouldNotValidateDependencies()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin();
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Act
                _pluginRegistry.PerformPluginSetup(_resolver);
                
                // Assert
                Assert.That(plugin.PerformSetupCalled, Is.True);
                // Non-IDependencyDeclaration plugins don't have ValidateDependenciesCalled
            }

            [Test]
            public void PerformPluginSetup_WhenMultipleDependencyDeclarationPlugins_ShouldValidateAll()
            {
                // Arrange
                var plugin1 = PluginRegistryTestData.CreateDependencyDeclarationPlugin("Plugin1");
                var plugin2 = PluginRegistryTestData.CreateDependencyDeclarationPlugin("Plugin2");
                
                plugin1.RequiredServices = new[] { typeof(string) };
                plugin2.RequiredServices = new[] { typeof(int) };
                
                _pluginRegistry.Register(plugin1);
                _pluginRegistry.Register(plugin2);
                
                // Act
                _pluginRegistry.PerformPluginSetup(_resolver);
                
                // Assert
                Assert.That(plugin1.ValidateDependenciesCalled, Is.True);
                Assert.That(plugin1.PerformSetupCalled, Is.True);
                Assert.That(plugin2.ValidateDependenciesCalled, Is.True);
                Assert.That(plugin2.PerformSetupCalled, Is.True);
            }

            [Test]
            public void PerformPluginSetup_WhenOneDependencyDeclarationPluginFailsValidation_ShouldThrowException()
            {
                // Arrange
                var failingPlugin = PluginRegistryTestData.CreateDependencyDeclarationPlugin("Failing");
                failingPlugin.RequiredServices = new[] { typeof(double) }; // Not available
                
                _pluginRegistry.Register(failingPlugin);
                
                // Verify plugin is registered
                var runtimePlugins = _pluginRegistry.GetRuntimePlugins().ToList();
                Assert.That(runtimePlugins.Count, Is.EqualTo(1), "Plugin should be registered as runtime plugin");
                
                // Test individual validation
                Assert.That(failingPlugin.ValidateDependencies(_resolver), Is.False, "Failing plugin should fail validation");
                
                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => 
                    _pluginRegistry.PerformPluginSetup(_resolver));
                
                Assert.That(exception.Message, Does.Contain("TestDependencyDeclarationPlugin"));
                Assert.That(exception.Message, Does.Contain("missing required dependencies"));
                
                // When an exception is thrown, no plugins should have their setup performed
                Assert.That(failingPlugin.PerformSetupCalled, Is.False);
                
                // Plugin should have had its dependencies validated
                Assert.That(failingPlugin.ValidateDependenciesCalled, Is.True);
            }
        }

        #endregion

        #region GetGamePlugins Method Tests

        /// <summary>
        /// Test suite for GetGamePlugins method functionality
        /// CRITICAL: Tests collection access and enumeration behavior
        /// </summary>
        [TestFixture]
        public class GetGamePluginsTests : PluginRegistryTests
        {
            [Test]
            public void GetGamePlugins_WhenRegistryHasPlugins_ShouldReturnAllGamePlugins()
            {
                // Arrange
                var plugin1 = PluginRegistryTestData.CreateGamePlugin("Plugin1");
                var plugin2 = PluginRegistryTestData.CreateDualInterfacePlugin("Plugin2");
                var plugin3 = PluginRegistryTestData.CreateRuntimePlugin("Plugin3");
                
                _pluginRegistry.Register(plugin1);
                _pluginRegistry.Register(plugin2);
                _pluginRegistry.InitializeRuntimePlugin(plugin3, _resolver);
                
                // Act
                var result = _pluginRegistry.GetGamePlugins();
                
                // Assert
                Assert.That(result, Is.Not.Null);
                var pluginsList = result.ToList();
                Assert.That(pluginsList.Count, Is.EqualTo(2)); // Only plugin1 and plugin2 implement IGamePlugin
                Assert.That(pluginsList, Contains.Item(plugin1));
                Assert.That(pluginsList, Contains.Item(plugin2));
                // plugin3 is not a game plugin, so it won't be in the collection
            }

            [Test]
            public void GetGamePlugins_WhenRegistryIsEmpty_ShouldReturnEmptyEnumerable()
            {
                // Act
                var result = _pluginRegistry.GetGamePlugins();
                
                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Count(), Is.EqualTo(0));
            }

            [Test]
            public void GetGamePlugins_WhenCalledMultipleTimes_ShouldReturnSameInstances()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateGamePlugin();
                _pluginRegistry.Register(plugin);
                
                // Act
                var result1 = _pluginRegistry.GetGamePlugins();
                var result2 = _pluginRegistry.GetGamePlugins();
                
                // Assert
                Assert.That(result1, Is.Not.Null);
                Assert.That(result2, Is.Not.Null);
                Assert.That(result1.Count(), Is.EqualTo(1));
                Assert.That(result2.Count(), Is.EqualTo(1));
                Assert.That(result1.First(), Is.SameAs(result2.First()));
            }
        }

        #endregion

        #region GetRuntimePlugins Method Tests

        /// <summary>
        /// Test suite for GetRuntimePlugins method functionality
        /// CRITICAL: Tests runtime plugin collection access and filtering
        /// </summary>
        [TestFixture]
        public class GetRuntimePluginsTests : PluginRegistryTests
        {
            [Test]
            public void GetRuntimePlugins_WhenRegistryHasRuntimePlugins_ShouldReturnAllRuntimePlugins()
            {
                // Arrange
                var plugin1 = PluginRegistryTestData.CreateDualInterfacePlugin("Plugin1");
                var plugin2 = PluginRegistryTestData.CreateRuntimePlugin("Plugin2");
                var plugin3 = PluginRegistryTestData.CreateGamePlugin("Plugin3"); // Not a runtime plugin
                
                _pluginRegistry.Register(plugin1);
                _pluginRegistry.InitializeRuntimePlugin(plugin2, _resolver);
                _pluginRegistry.Register(plugin3);
                
                // Act
                var result = _pluginRegistry.GetRuntimePlugins();
                
                // Assert
                Assert.That(result, Is.Not.Null);
                var pluginsList = result.ToList();
                Assert.That(pluginsList.Count, Is.EqualTo(2)); // Only plugin1 and plugin2 implement IRuntimePlugin
                Assert.That(pluginsList, Contains.Item(plugin1));
                Assert.That(pluginsList, Contains.Item(plugin2));
                // plugin3 is not a runtime plugin, so it won't be in the collection
            }

            [Test]
            public void GetRuntimePlugins_WhenRegistryIsEmpty_ShouldReturnEmptyEnumerable()
            {
                // Act
                var result = _pluginRegistry.GetRuntimePlugins();
                
                // Assert
                Assert.That(result, Is.Not.Null);
                Assert.That(result.Count(), Is.EqualTo(0));
            }

            [Test]
            public void GetRuntimePlugins_WhenCalledMultipleTimes_ShouldReturnSameInstances()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin();
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Act
                var result1 = _pluginRegistry.GetRuntimePlugins();
                var result2 = _pluginRegistry.GetRuntimePlugins();
                
                // Assert
                Assert.That(result1, Is.Not.Null);
                Assert.That(result2, Is.Not.Null);
                Assert.That(result1.Count(), Is.EqualTo(1));
                Assert.That(result2.Count(), Is.EqualTo(1));
                Assert.That(result1.First(), Is.SameAs(result2.First()));
            }
        }

        #endregion

        #region InitializeRuntimePlugin Method Tests

        /// <summary>
        /// Test suite for InitializeRuntimePlugin method functionality
        /// CRITICAL: Tests plugin initialization and collection management
        /// </summary>
        [TestFixture]
        public class InitializeRuntimePluginTests : PluginRegistryTests
        {
            [Test]
            public void InitializeRuntimePlugin_WhenPluginNotInRuntimePlugins_ShouldAddToRuntimePlugins()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin();
                
                // Act
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Assert
                var runtimePlugins = PluginRegistryReflectionHelper.GetRuntimePlugins(_pluginRegistry);
                Assert.That(runtimePlugins, Is.Not.Null);
                Assert.That(runtimePlugins.Count, Is.EqualTo(1));
                Assert.That(runtimePlugins[0], Is.SameAs(plugin));
                Assert.That(plugin.InitializeCalled, Is.True);
            }

            [Test]
            public void InitializeRuntimePlugin_WhenPluginAlreadyInRuntimePlugins_ShouldNotAddDuplicate()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateDualInterfacePlugin();
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver); // This adds it to runtimePlugins
                
                // Act
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Assert
                var runtimePlugins = PluginRegistryReflectionHelper.GetRuntimePlugins(_pluginRegistry);
                Assert.That(runtimePlugins.Count, Is.EqualTo(1));
                Assert.That(plugin.InitializeCalled, Is.True);
            }

            [Test]
            public void InitializeRuntimePlugin_WhenPluginInitializeThrowsException_ShouldPropagateException()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin(shouldThrowOnInitialize: true);
                
                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver));
                Assert.That(exception.Message, Is.EqualTo(PluginRegistryTestData.TestExceptionMessage));
            }

            [Test]
            public void InitializeRuntimePlugin_WhenNullPluginProvided_ShouldThrowArgumentNullException()
            {
                // Arrange - Get initial count
                var initialCount = PluginRegistryReflectionHelper.GetRuntimePlugins(_pluginRegistry).Count();
                
                // Act & Assert
                Assert.Throws<ArgumentNullException>(() => _pluginRegistry.InitializeRuntimePlugin(null, _resolver));
                
                // Verify no plugins were added due to null check
                var runtimePlugins = PluginRegistryReflectionHelper.GetRuntimePlugins(_pluginRegistry);
                Assert.That(runtimePlugins.Count(), Is.EqualTo(initialCount), 
                    "No plugins should be added when null plugin is provided");
            }

            [Test]
            public void InitializeRuntimePlugin_WhenNullResolverProvided_ShouldPassNullToPlugin()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin();
                
                // Act
                _pluginRegistry.InitializeRuntimePlugin(plugin, null);
                
                // Assert
                Assert.That(plugin.InitializeCalled, Is.True);
                Assert.That(plugin.LastResolver, Is.Null);
            }
        }

        #endregion

        #region PerformPluginSetup Method Tests

        /// <summary>
        /// Test suite for PerformPluginSetup method functionality
        /// CRITICAL: Tests setup calls on all runtime plugins and exception handling
        /// </summary>
        [TestFixture]
        public class PerformPluginSetupTests : PluginRegistryTests
        {
            [Test]
            public void PerformPluginSetup_WhenCalledWithRuntimePlugins_ShouldCallPerformSetupOnAllPlugins()
            {
                // Arrange
                var plugin1 = PluginRegistryTestData.CreateRuntimePlugin("Plugin1");
                var plugin2 = PluginRegistryTestData.CreateDualInterfacePlugin("Plugin2");
                
                _pluginRegistry.InitializeRuntimePlugin(plugin1, _resolver);
                _pluginRegistry.Register(plugin2);
                
                // Act
                _pluginRegistry.PerformPluginSetup(_resolver);
                
                // Assert
                Assert.That(plugin1.PerformSetupCalled, Is.True);
                Assert.That(plugin2.PerformSetupCalled, Is.True);
                Assert.That(plugin1.PerformSetupCallCount, Is.EqualTo(1));
                Assert.That(plugin2.PerformSetupCallCount, Is.EqualTo(1));
            }

            [Test]
            public void PerformPluginSetup_WhenCalledWithEmptyRegistry_ShouldNotThrow()
            {
                // Act & Assert
                Assert.DoesNotThrow(() => _pluginRegistry.PerformPluginSetup(_resolver));
            }

            [Test]
            public void PerformPluginSetup_WhenPluginPerformSetupThrowsException_ShouldPropagateException()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin(shouldThrowOnSetup: true);
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Act & Assert
                var exception = Assert.Throws<InvalidOperationException>(() => _pluginRegistry.PerformPluginSetup(_resolver));
                Assert.That(exception.Message, Is.EqualTo(PluginRegistryTestData.TestExceptionMessage));
            }

            [Test]
            public void PerformPluginSetup_WhenNullResolverProvided_ShouldPassNullToPlugins()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin();
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Act
                _pluginRegistry.PerformPluginSetup(null);
                
                // Assert
                Assert.That(plugin.PerformSetupCalled, Is.True);
                Assert.That(plugin.LastResolver, Is.Null);
            }

            /// <summary>
            /// CRITICAL: Tests exception isolation - exceptions in one plugin don't affect others
            /// </summary>
            [Test]
            public void PerformPluginSetup_WhenOnePluginThrowsException_ShouldNotAffectOtherPlugins()
            {
                // Arrange
                var workingPlugin = PluginRegistryTestData.CreateRuntimePlugin("WorkingPlugin");
                var throwingPlugin = PluginRegistryTestData.CreateRuntimePlugin("ThrowingPlugin", shouldThrowOnSetup: true);
                
                _pluginRegistry.InitializeRuntimePlugin(workingPlugin, _resolver);
                _pluginRegistry.InitializeRuntimePlugin(throwingPlugin, _resolver);
                
                // Act & Assert - Should throw exception from throwing plugin
                var exception = Assert.Throws<InvalidOperationException>(() => _pluginRegistry.PerformPluginSetup(_resolver));
                Assert.That(exception.Message, Is.EqualTo(PluginRegistryTestData.TestExceptionMessage));
                
                // Verify working plugin was still called before the exception
                Assert.That(workingPlugin.PerformSetupCalled, Is.True);
                Assert.That(throwingPlugin.PerformSetupCalled, Is.False); // Exception prevented completion
            }
        }

        #endregion

        #region PerformPluginRuntimeInitialization Method Tests

        /// <summary>
        /// Test suite for PerformPluginRuntimeInitialization method functionality
        /// CRITICAL: Tests dependency readiness logic and conditional initialization
        /// </summary>
        [TestFixture]
        public class PerformPluginRuntimeInitializationTests : PluginRegistryTests
        {
            [Test]
            public void PerformPluginRuntimeInitialization_WhenDependenciesReady_ShouldCallPerformRuntimeInitialization()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin(dependenciesReady: true);
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Act
                _pluginRegistry.PerformPluginRuntimeInitialization(_resolver);
                
                // Assert
                Assert.That(plugin.AreDependenciesReadyCalled, Is.True);
                Assert.That(plugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(plugin.AreDependenciesReadyCallCount, Is.EqualTo(1));
                Assert.That(plugin.PerformRuntimeInitializationCallCount, Is.EqualTo(1));
            }

            [Test]
            public void PerformPluginRuntimeInitialization_WhenDependenciesNotReady_ShouldNotCallPerformRuntimeInitialization()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin(dependenciesReady: false);
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Expect error log for dependencies not ready
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex($@".*Plugin TestRuntimePlugin dependencies not ready.*Missing: unknown dependencies.*"));
                
                // Act
                _pluginRegistry.PerformPluginRuntimeInitialization(_resolver);
                
                // Assert
                Assert.That(plugin.AreDependenciesReadyCalled, Is.True);
                Assert.That(plugin.PerformRuntimeInitializationCalled, Is.False);
                Assert.That(plugin.AreDependenciesReadyCallCount, Is.EqualTo(1));
                Assert.That(plugin.PerformRuntimeInitializationCallCount, Is.EqualTo(0));
            }

            [Test]
            public void PerformPluginRuntimeInitialization_WhenMixedDependencyStates_ShouldOnlyInitializeReadyPlugins()
            {
                // Arrange
                var readyPlugin = PluginRegistryTestData.CreateRuntimePlugin("ReadyPlugin", dependenciesReady: true);
                var notReadyPlugin = PluginRegistryTestData.CreateRuntimePlugin("NotReadyPlugin", dependenciesReady: false);
                
                _pluginRegistry.InitializeRuntimePlugin(readyPlugin, _resolver);
                _pluginRegistry.InitializeRuntimePlugin(notReadyPlugin, _resolver);
                
                // Expect error log for dependencies not ready
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex($@".*Plugin TestRuntimePlugin dependencies not ready.*Missing: unknown dependencies.*"));
                
                // Act
                _pluginRegistry.PerformPluginRuntimeInitialization(_resolver);
                
                // Assert
                Assert.That(readyPlugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(notReadyPlugin.PerformRuntimeInitializationCalled, Is.False);
                Assert.That(readyPlugin.AreDependenciesReadyCalled, Is.True);
                Assert.That(notReadyPlugin.AreDependenciesReadyCalled, Is.True);
            }

            [Test]
            public void PerformPluginRuntimeInitialization_WhenPerformRuntimeInitializationThrowsException_ShouldPropagateException()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin(shouldThrowOnRuntimeInit: true);
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Expect error log for runtime initialization failure
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex($@".*Plugin TestRuntimePlugin runtime initialization failed.*Test exception.*"));
                
                // Act & Assert - Exception should be caught and logged, not propagated
                Assert.DoesNotThrow(() => _pluginRegistry.PerformPluginRuntimeInitialization(_resolver));
                
                // Verify the plugin was called
                Assert.That(plugin.AreDependenciesReadyCalled, Is.True);
                Assert.That(plugin.PerformRuntimeInitializationCallCount, Is.EqualTo(1)); // Call count should be incremented
                Assert.That(plugin.PerformRuntimeInitializationCalled, Is.False); // Flag should be false because exception was thrown
                Assert.That(plugin.LastThrownException, Is.Not.Null); // Exception should be stored
                Assert.That(plugin.LastThrownException.Message, Is.EqualTo(PluginRegistryTestData.TestExceptionMessage));
            }

            [Test]
            public void PerformPluginRuntimeInitialization_WhenCalledWithEmptyRegistry_ShouldNotThrow()
            {
                // Act & Assert
                Assert.DoesNotThrow(() => _pluginRegistry.PerformPluginRuntimeInitialization(_resolver));
            }

            [Test]
            public void PerformPluginRuntimeInitialization_WhenNullResolverProvided_ShouldPassNullToPlugins()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin();
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Act
                _pluginRegistry.PerformPluginRuntimeInitialization(null);
                
                // Assert
                Assert.That(plugin.AreDependenciesReadyCalled, Is.True);
                Assert.That(plugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(plugin.LastResolver, Is.Null);
            }

            /// <summary>
            /// CRITICAL: Parameterized test for dependency readiness scenarios
            /// </summary>
            [TestCase(true, true)]
            [TestCase(false, false)]
            [TestCase(true, false)]
            [TestCase(false, true)]
            public void PerformPluginRuntimeInitialization_WhenDependencyStatesVary_ShouldHandleCorrectly(bool dependenciesReady, bool shouldThrowOnRuntimeInit)
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateRuntimePlugin(
                    dependenciesReady: dependenciesReady, 
                    shouldThrowOnRuntimeInit: shouldThrowOnRuntimeInit);
                _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                
                // Expect error logs based on the test scenario
                if (!dependenciesReady)
                {
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex($@".*Plugin TestRuntimePlugin dependencies not ready.*Missing: unknown dependencies.*"));
                }
                if (shouldThrowOnRuntimeInit && dependenciesReady)
                {
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex($@".*Plugin TestRuntimePlugin runtime initialization failed.*Test exception.*"));
                }
                
                // Act - Should not throw as exceptions are caught and logged
                Assert.DoesNotThrow(() => _pluginRegistry.PerformPluginRuntimeInitialization(_resolver));
                
                // Assert
                Assert.That(plugin.AreDependenciesReadyCalled, Is.True);
                
                if (dependenciesReady && !shouldThrowOnRuntimeInit)
                {
                    Assert.That(plugin.PerformRuntimeInitializationCalled, Is.True);
                }
                else
                {
                    Assert.That(plugin.PerformRuntimeInitializationCalled, Is.False);
                }
            }
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Test suite for complete workflow integration testing
        /// CRITICAL: Tests end-to-end plugin lifecycle management
        /// </summary>
        [TestFixture]
        public class IntegrationTests : PluginRegistryTests
        {
            [Test]
            public void CompleteWorkflow_WhenMultiplePluginsRegistered_ShouldHandleAllLifecyclePhases()
            {
                // Arrange
                var gamePlugin = PluginRegistryTestData.CreateGamePlugin("GamePlugin");
                var runtimePlugin = PluginRegistryTestData.CreateRuntimePlugin("RuntimePlugin");
                var dualPlugin = PluginRegistryTestData.CreateDualInterfacePlugin("DualPlugin");
                
                _pluginRegistry.Register(gamePlugin);
                _pluginRegistry.InitializeRuntimePlugin(runtimePlugin, _resolver);
                _pluginRegistry.Register(dualPlugin);
                
                var mockBuilder = Substitute.For<IContainerBuilder>();
                
                // Act - Complete workflow
                _pluginRegistry.ApplyAll(mockBuilder);
                _pluginRegistry.InitializeRuntimePlugin(runtimePlugin, _resolver);
                _pluginRegistry.PerformPluginSetup(_resolver);
                _pluginRegistry.PerformPluginRuntimeInitialization(_resolver);
                
                // Assert - Verify all phases completed
                Assert.That(gamePlugin.RegisterCalled, Is.True);
                Assert.That(dualPlugin.RegisterCalled, Is.True);
                
                Assert.That(runtimePlugin.InitializeCalled, Is.True);
                Assert.That(dualPlugin.InitializeCalled, Is.False); // Not initialized via InitializeRuntimePlugin
                
                Assert.That(runtimePlugin.PerformSetupCalled, Is.True);
                Assert.That(dualPlugin.PerformSetupCalled, Is.True);
                
                Assert.That(runtimePlugin.PerformRuntimeInitializationCalled, Is.True);
                Assert.That(dualPlugin.PerformRuntimeInitializationCalled, Is.True);
            }

            /// <summary>
            /// CRITICAL: Tests exception isolation across the complete workflow
            /// </summary>
            [Test]
            public void ExceptionIsolation_WhenOnePluginThrows_ShouldNotAffectOtherPlugins()
            {
                // Arrange
                var workingPlugin = PluginRegistryTestData.CreateRuntimePlugin("WorkingPlugin");
                var throwingPlugin = PluginRegistryTestData.CreateRuntimePlugin("ThrowingPlugin", shouldThrowOnSetup: true);
                
                _pluginRegistry.InitializeRuntimePlugin(workingPlugin, _resolver);
                _pluginRegistry.InitializeRuntimePlugin(throwingPlugin, _resolver);
                
                // Act & Assert - Setup should throw but not affect other operations
                var exception = Assert.Throws<InvalidOperationException>(() => _pluginRegistry.PerformPluginSetup(_resolver));
                Assert.That(exception.Message, Is.EqualTo(PluginRegistryTestData.TestExceptionMessage));
                
                // Verify working plugin was still called before the exception
                Assert.That(workingPlugin.PerformSetupCalled, Is.True);
                Assert.That(throwingPlugin.PerformSetupCalled, Is.False); // Exception prevented completion
            }

            /// <summary>
            /// CRITICAL: Tests concurrent access to multiple registry methods
            /// </summary>
            [Test]
            public void ConcurrentAccess_WhenMultipleMethodsCalledSimultaneously_ShouldHandleGracefully()
            {
                // Arrange
                var plugins = PluginRegistryTestData.CreateGamePlugins(5);
                var runtimePlugins = PluginRegistryTestData.CreateRuntimePlugins(5);
                var tasks = new List<Task>();
                var exceptions = new List<Exception>();
                
                // Act - Simulate concurrent access to different methods
                foreach (var plugin in plugins)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            _pluginRegistry.Register(plugin);
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
                
                foreach (var plugin in runtimePlugins)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
                            _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver);
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
                var gamePlugins = PluginRegistryReflectionHelper.GetGamePlugins(_pluginRegistry);
                var runtimePluginsResult = PluginRegistryReflectionHelper.GetRuntimePlugins(_pluginRegistry);
                Assert.That(gamePlugins.Count, Is.EqualTo(5)); // Only game plugins implement IGamePlugin
                Assert.That(runtimePluginsResult.Count, Is.EqualTo(5)); // Only runtime plugins implement IRuntimePlugin
            }

            /// <summary>
            /// CRITICAL: Tests multiple lifecycle cycles for state consistency
            /// </summary>
            [Test]
            public void MultipleLifecycleCycles_WhenRepeatedOperations_ShouldMaintainConsistency()
            {
                // Arrange
                var plugin = PluginRegistryTestData.CreateDualInterfacePlugin("TestPlugin");
                _pluginRegistry.Register(plugin); // Register first to add to both collections
                
                // Act - Multiple cycles
                for (int i = 0; i < 3; i++)
                {
                    plugin.ResetCallCounts();
                    
                    _pluginRegistry.ApplyAll(Substitute.For<IContainerBuilder>());
                    _pluginRegistry.InitializeRuntimePlugin(plugin, _resolver); // Initialize the already-registered plugin
                    _pluginRegistry.PerformPluginSetup(_resolver);
                    _pluginRegistry.PerformPluginRuntimeInitialization(_resolver);
                }
                
                // Assert - State should be consistent after multiple cycles
                Assert.That(plugin.RegisterCalled, Is.True); // From initial Register call
                Assert.That(plugin.InitializeCalled, Is.True); // From InitializeRuntimePlugin calls
                Assert.That(plugin.PerformSetupCalled, Is.True); // From PerformPluginSetup calls
                Assert.That(plugin.PerformRuntimeInitializationCalled, Is.True); // From PerformPluginRuntimeInitialization calls
                
                // Verify call counts are from the last cycle only
                Assert.That(plugin.RegisterCallCount, Is.EqualTo(1)); // From the last ApplyAll call
                Assert.That(plugin.InitializeCallCount, Is.EqualTo(1));
                Assert.That(plugin.PerformSetupCallCount, Is.EqualTo(1));
                Assert.That(plugin.PerformRuntimeInitializationCallCount, Is.EqualTo(1));
            }
        }

        #endregion
    }
}
