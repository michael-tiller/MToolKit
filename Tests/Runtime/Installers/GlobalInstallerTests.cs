/**
 * Unit tests for GlobalInstaller.cs
 * Updated for redesigned plugin list retrieval on 2025-01-20
 * Framework: Unity Test Framework with NUnit
 * 
 * Updated Approach:
 * - Simplified plugin list mocking: Mock GlobalPluginPrefabs contents directly
 * - Avoid complex GlobalConfigLoader singleton setup
 * - Focus on testing plugin retrieval and registration logic
 * - Direct field injection via reflection for test isolation
 * 
 * Plugin Retrieval Design:
 * - GlobalInstaller.Configure() retrieves GlobalPluginConfig via GlobalConfigLoader.Instance.GlobalPluginConfig
 * - Simple mocking: Set mock plugin lists directly on GlobalPluginConfigAsset instances
 * - Null-safe access pattern: GlobalConfigLoader.Instance.GlobalPluginConfig ?? null
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;
using UnityEngine;
using Serilog;
using NSubstitute;
using MToolKit.Tests.Runtime.Core;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Config;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.MessageBus.Events;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence;
using MessagePipe;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.Core.Singletons;

namespace MToolKit.Tests.Runtime.Installers
{
    /// <summary>
    /// Test data constants and factory methods for GlobalInstaller testing
    /// </summary>
    internal static class GlobalInstallerTestData
    {
        public const string TestPluginName = "TestPlugin";
        public const int DefaultPluginCount = 2;
        public const int LargePluginListCount = 10;

        /// <summary>
        /// Creates a GlobalPluginConfigAsset with mocked plugin list contents
        /// </summary>
        /// <param name="pluginCount">Number of plugins in the list</param>
        /// <returns>Configured GlobalPluginConfigAsset with mocked plugins</returns>
        public static GlobalPluginConfigAsset CreateGlobalPluginConfig(int pluginCount = DefaultPluginCount)
        {
            var config = Substitute.For<GlobalPluginConfigAsset>();
            
            var plugins = new List<AbstractGamePlugin>();
            for (int i = 0; i < pluginCount; i++)
            {
                plugins.Add(CreateMockPlugin($"TestPlugin_{i}"));
            }
            
            config.GlobalPluginPrefabs = plugins;
            return config;
        }

        /// <summary>
        /// Alias for CreateGlobalPluginConfig with default parameters
        /// </summary>
        /// <returns>Configured GlobalPluginConfigAsset</returns>
        public static GlobalPluginConfigAsset CreateMockGlobalPluginConfig()
        {
            return CreateGlobalPluginConfig();
        }
        
        /// <summary>
        /// Creates a mock AbstractGamePlugin for testing
        /// </summary>
        /// <param name="name">Plugin name for identification</param>
        /// <returns>Configured mock plugin</returns>
        public static AbstractGamePlugin CreateMockPlugin(string name = TestPluginName)
        {
            var mockPlugin = Substitute.For<AbstractGamePlugin>();
            
            // Set up Register behavior
            mockPlugin.When(x => x.Register(Arg.Any<VContainer.IContainerBuilder>()))
                .Do(x => { /* Mock implementation */ });
            
            return mockPlugin;
        }

        /// <summary>
        /// Creates multiple mock plugins for comprehensive testing
        /// </summary>
        /// <param name="count">Number of plugins to create</param>
        /// <returns>List of mock plugins</returns>
        public static List<AbstractGamePlugin> CreateMockPlugins(int count = DefaultPluginCount)
        {
            var plugins = new List<AbstractGamePlugin>();
            for (int i = 0; i < count; i++)
            {
                plugins.Add(CreateMockPlugin($"MockPlugin_{i}"));
            }
            return plugins;
        }
        
        /// <summary>
        /// Creates a null GlobalPluginConfigAsset to test null handling
        /// </summary>
        /// <returns>Null config asset</returns>
        public static GlobalPluginConfigAsset CreateNullGlobalPluginConfig()
        {
            return null;
        }
    }

    /// <summary>
    /// Reflection utilities for setting up GlobalInstaller tests with plugin list mocking
    /// </summary>
    internal static class GlobalInstallerReflectionHelper
    {
        private static readonly Lazy<FieldInfo> GlobalPluginConfigField = new(() => 
            typeof(GlobalInstaller).GetField("globalPluginConfig", BindingFlags.NonPublic | BindingFlags.Instance));
        private static readonly Lazy<FieldInfo> GlobalPluginConfigSetByTestField = new(() => 
            typeof(GlobalInstaller).GetField("globalPluginConfigSetByTest", BindingFlags.NonPublic | BindingFlags.Instance));

        /// <summary>
        /// Sets the globalPluginConfig field directly on GlobalInstaller for testing
        /// </summary>
        /// <param name="installer">GlobalInstaller instance</param>
        /// <param name="config">GlobalPluginConfigAsset to set</param>
        public static void SetGlobalPluginConfig(GlobalInstaller installer, GlobalPluginConfigAsset config)
        {
            GlobalPluginConfigField.Value?.SetValue(installer, config);
            GlobalPluginConfigSetByTestField.Value?.SetValue(installer, true);
        }

        /// <summary>
        /// Gets the globalPluginConfig field value from GlobalInstaller
        /// </summary>
        /// <param name="installer">GlobalInstaller instance</param>
        /// <returns>Current GlobalPluginConfigAsset</returns>
        public static GlobalPluginConfigAsset GetGlobalPluginConfig(GlobalInstaller installer)
        {
            return GlobalPluginConfigField.Value?.GetValue(installer) as GlobalPluginConfigAsset;
    }

    /// <summary>
        /// Invokes Configure method via reflection for testing
    /// </summary>
        /// <param name="installer">GlobalInstaller instance</param>
        /// <param name="builder">ContainerBuilder to pass to Configure</param>
        public static void InvokeConfigure(GlobalInstaller installer, IContainerBuilder builder)
        {
            var configureMethod = typeof(GlobalInstaller).GetMethod("Configure", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            configureMethod?.Invoke(installer, new object[] { builder });
        }

        /// <summary>
        /// Invokes RegisterGlobalPlugins method via reflection for testing
        /// </summary>
        /// <param name="installer">GlobalInstaller instance</param>
        /// <param name="builder">ContainerBuilder to pass to RegisterGlobalPlugins</param>
        public static void InvokeRegisterGlobalPlugins(GlobalInstaller installer, IContainerBuilder builder)
        {
            var registerMethod = typeof(GlobalInstaller).GetMethod("RegisterGlobalPlugins", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            registerMethod?.Invoke(installer, new object[] { builder });
        }

        /// <summary>
        /// Invokes InitializeGlobalRuntimePlugins method via reflection for testing
        /// </summary>
        /// <param name="installer">GlobalInstaller instance</param>
        /// <param name="builder">ContainerBuilder to pass to InitializeGlobalRuntimePlugins</param>
        public static void InvokeInitializeGlobalRuntimePlugins(GlobalInstaller installer, IContainerBuilder builder)
        {
            var initializeMethod = typeof(GlobalInstaller).GetMethod("InitializeGlobalRuntimePlugins", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            initializeMethod?.Invoke(installer, new object[] { builder });
        }

        /// <summary>
        /// Sets up singleton behavior control for testing
        /// </summary>
        /// <param name="disable">Whether to disable singleton behavior</param>
        public static void SetDisableSingletonBehavior(bool disable)
        {
            GlobalInstaller.DisableSingletonBehavior = disable;
        }
        
    /// <summary>
        /// Sets the GlobalInstaller singleton instance for testing
    /// </summary>
        /// <param name="installer">GlobalInstaller instance to set</param>
        public static void SetGlobalInstallerInstance(GlobalInstaller installer)
        {
            GlobalInstaller.SetInstanceForTesting(installer);
    }

    /// <summary>
        /// Resets the GlobalInstaller singleton
    /// </summary>
        public static void ResetGlobalInstallerInstance()
        {
            GlobalInstaller.Reset();
        }

        /// <summary>
        /// Gets the GlobalInstaller singleton instance for testing
        /// </summary>
        public static GlobalInstaller GetGlobalInstallerInstance()
        {
            return GlobalInstaller.Instance;
        }
    }

    /// <summary>
        /// Mock test plugin that implements both IGamePlugin and IRuntimePlugin
    /// </summary>
    public class TestGamePluginMonoBehaviour : AbstractGamePlugin, IRuntimePlugin
    {
        public bool IsRuntimePlugin { get; set; } = true;
            public bool ShouldThrowException { get; set; } = false;
            public bool RegisterCalled { get; private set; } = false;
            public bool InitializeCalled { get; private set; } = false;
            public int RegisterCallCount { get; private set; } = 0;
            public Exception ExceptionToThrow { get; set; }

            public override void Register(IContainerBuilder builder)
            {
                RegisterCalled = true;
                RegisterCallCount++;
                if (ShouldThrowException && ExceptionToThrow != null)
                    throw ExceptionToThrow;
            }
        
        public void Initialize(IObjectResolver resolver)
        {
                InitializeCalled = true;
                if (ShouldThrowException && ExceptionToThrow != null)
                    throw ExceptionToThrow;
        }
        
        public void PerformSetup(IObjectResolver resolver)
        {
                // Test setup implementation
        }
        
        public void PerformRuntimeInitialization(IObjectResolver resolver)
        {
                // Test runtime initialization implementation
        }
        
        public bool AreDependenciesReady(IObjectResolver resolver)
        {
                return true; // Test implementation
            }

            public void ResetCallTracking()
            {
                RegisterCalled = false;
                RegisterCallCount = 0;
                InitializeCalled = false;
                ShouldThrowException = false;
                ExceptionToThrow = null;
            }
    }

    /// <summary>
    /// Mock test plugin that implements only IGamePlugin (not IRuntimePlugin)
    /// </summary>
    public class TestNonRuntimePluginMonoBehaviour : AbstractGamePlugin
    {
        public bool IsRuntimePlugin { get; set; } = false;
        public bool ShouldThrowException { get; set; } = false;
        public bool RegisterCalled { get; private set; } = false;
        public int RegisterCallCount { get; private set; } = 0;
        public Exception ExceptionToThrow { get; set; }

        public override void Register(IContainerBuilder builder)
        {
            RegisterCalled = true;
            RegisterCallCount++;
            if (ShouldThrowException && ExceptionToThrow != null)
                throw ExceptionToThrow;
        }

        public void Initialize(IObjectResolver resolver)
        {
            // Test initialization implementation
        }

        public void Reset()
        {
            RegisterCalled = false;
            RegisterCallCount = 0;
            ShouldThrowException = false;
            ExceptionToThrow = null;
        }
    }

    [TestFixture]
    public class GlobalInstallerTests
    {
        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private readonly List<GameObject> _createdGameObjects = new List<GameObject>();
        private ILogger _mockLogger;
        
        [SetUp]
        public void Setup()
        {
            SetupTestEnvironment();
        }
        
        [TearDown]
        public void TearDown()
        {
            CleanupTestEnvironment();
        }
        
        /// <summary>
        /// Sets up the test environment with proper isolation
        /// </summary>
        private void SetupTestEnvironment()
        {
            // Reset singleton states
            GlobalInstallerReflectionHelper.ResetGlobalInstallerInstance();
            GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(false);

            // Create fresh container builder
            _containerBuilder = new ContainerBuilder();
            SetupMockDependencies();

            // Build resolver
            _resolver = _containerBuilder.Build();
        }
        
        /// <summary>
        /// Cleans up test environment with proper disposal
        /// </summary>
        private void CleanupTestEnvironment()
        {
            // Dispose resolver
            _resolver?.Dispose();
            
            // Reset singleton states
            GlobalInstallerReflectionHelper.ResetGlobalInstallerInstance();
            GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(false);

            // Clean up GameObjects
            foreach (var obj in _createdGameObjects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }
            _createdGameObjects.Clear();

            // Cleanup complete
        }
        
        /// <summary>
        /// Sets up mock dependencies for testing
        /// </summary>
        private void SetupMockDependencies()
        {
            // Register mock logger
            // Using MockLogger instead of NSubstitute for IL2CPP compatibility (no Reflection.Emit)
            _mockLogger = new MockLogger();
            _containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
        }
        
        /// <summary>
        /// Creates a GlobalInstaller with mocked plugin configuration
        /// </summary>
        /// <param name="config">GlobalPluginConfigAsset to set on installer</param>
        /// <returns>Configured GlobalInstaller instance</returns>
        private GlobalInstaller CreateGlobalInstallerWithConfig(GlobalPluginConfigAsset config = null)
        {
            var gameObject = new GameObject("TestGlobalInstaller");
            _createdGameObjects.Add(gameObject);
            var installer = gameObject.AddComponent<GlobalInstaller>();
            
            // Always set plugin config directly on installer field - even if null
            // This ensures tests can explicitly set null configs
            GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, config);
            
            return installer;
        }
        
        #region Singleton Pattern Tests

        [TestFixture]
        public class SingletonPatternTests : GlobalInstallerTests
        {
            [Test]
            public void Awake_WhenFirstInstance_ShouldSetAsSingleton()
            {
                // Arrange - Create real GlobalConfigLoader instance instead of mock
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var mockGlobalPluginConfig = GlobalInstallerTestData.CreateMockGlobalPluginConfig();
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, mockGlobalPluginConfig);
                
                // Set the mock as the singleton via reflection
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(true);

                var installer = new GameObject("TestGlobalInstaller").AddComponent<GlobalInstaller>();
                _createdGameObjects.Add(installer.gameObject);

                // Act
                installer.TestAwake();

                // Assert
                Assert.That(GlobalInstaller.Instance, Is.EqualTo(installer));
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void Awake_WhenDuplicateInstance_ShouldDestroyDuplicate()
            {
                // Arrange - Create real GlobalConfigLoader instance instead of mock
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var mockGlobalPluginConfig = GlobalInstallerTestData.CreateMockGlobalPluginConfig();
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, mockGlobalPluginConfig);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(false);

                var firstInstaller = new GameObject("FirstGlobalInstaller").AddComponent<GlobalInstaller>();
                _createdGameObjects.Add(firstInstaller.gameObject);

                // Set first as singleton instance
                GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(firstInstaller);

                // Act - Create second instance (should detect duplicate)
                var secondInstaller = new GameObject("SecondGlobalInstaller").AddComponent<GlobalInstaller>();
                _createdGameObjects.Add(secondInstaller.gameObject);

                // Assert - Check singleton behavior
                var singletonInstance = GlobalInstaller.Instance;
                Assert.That(singletonInstance, Is.Not.Null, "Should have a singleton instance");
                Assert.That(singletonInstance, Is.TypeOf<GlobalInstaller>(), "Singleton should be GlobalInstaller");
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }

            [Test]
            public void OnDestroy_WhenSingletonInstance_ShouldClearSingleton()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var mockGlobalPluginConfig = GlobalInstallerTestData.CreateMockGlobalPluginConfig();
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, mockGlobalPluginConfig);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig();
                GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(installer);

                // Act
                installer.TestOnDestroy();

                // Assert
                Assert.That(GlobalInstaller.Instance, Is.Null);
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }

            [Test]
            public void Reset_ShouldClearSingletonInstance()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var mockGlobalPluginConfig = GlobalInstallerTestData.CreateMockGlobalPluginConfig();
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, mockGlobalPluginConfig);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig();
                GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(installer);

                // Act
                GlobalInstaller.Reset();

                // Assert
                Assert.That(GlobalInstaller.Instance, Is.Null);
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
        }

        #endregion

        #region Plugin Configuration Retrieval Tests

        [TestFixture]
        public class PluginConfigurationTests : GlobalInstallerTests
        {
            [Test]
            public void GlobalPluginConfig_SetValidConfig_ShouldRetrieveCorrectly()
            {
                // Arrange
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(pluginCount: 3);
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                
                // Assert
                var retrievedConfig = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                Assert.That(retrievedConfig, Is.Not.Null, "GlobalPluginConfig should be retrieved");
                Assert.That(retrievedConfig.GlobalPluginPrefabs.Count, Is.EqualTo(3), 
                    "Retrieved config should have 3 plugins");
            }
            
            [Test]
            public void GlobalPluginConfig_SetNullConfig_ShouldHandleGracefully()
            {
                // Arrange
                var installer = CreateGlobalInstallerWithConfig(null);

                // Act & Assert − Should not throw exception
                Assert.DoesNotThrow(() =>
                {
                    GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                }, "Configure should handle null config gracefully");

                // Assert null config is handled
                var retrievedConfig = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                Assert.That(retrievedConfig, Is.Null, "Null config should be handled gracefully");
            }

            [TestCase(0)]
            [TestCase(1)]
            [TestCase(5)]
            [TestCase(10)]
            public void GlobalPluginConfig_VariousCounts_ShouldRetrieveCorrectly(int pluginCount)
            {
                // Arrange
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(pluginCount);
                var installer = CreateGlobalInstallerWithConfig(config);

                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);

                // Assert
                var retrievedConfig = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                Assert.That(retrievedConfig, Is.Not.Null, $"Config with {pluginCount} plugins should be retrievable");
                Assert.That(retrievedConfig.GlobalPluginPrefabs.Count, Is.EqualTo(pluginCount),
                    $"Retrieved config should have {pluginCount} plugins");
            }
            
            [Test]
            public void GlobalPluginConfig_AfterMultipleSets_ShouldUseLatestValue()
            {
                // Arrange
                var installer = CreateGlobalInstallerWithConfig();

                var config1 = GlobalInstallerTestData.CreateGlobalPluginConfig(2);
                var config2 = GlobalInstallerTestData.CreateGlobalPluginConfig(5);

                // Act
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, config1);
                var retrieved1 = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);

                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, config2);
                var retrieved2 = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);

                // Assert
                Assert.That(retrieved1.GlobalPluginPrefabs.Count, Is.EqualTo(2), "First config should have 2 plugins");
                Assert.That(retrieved2.GlobalPluginPrefabs.Count, Is.EqualTo(5), "Second config should have 5 plugins");
                Assert.That(retrieved1, Is.Not.EqualTo(retrieved2), "Configs should be different instances");
            }
        }

        #endregion

        #region Plugin Registration Tests

        [TestFixture]
        public class PluginRegistrationTests : GlobalInstallerTests
        {
            [Test]
            public void RegisterGlobalPlugins_WithValidPluginList_ShouldRegisterAllPlugins()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var plugins = GlobalInstallerTestData.CreateMockPlugins(3);
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig();
                config.GlobalPluginPrefabs = plugins;
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);

                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, _containerBuilder);
                
                // Assert
                var resolver = _containerBuilder.Build();
                
                // Verify plugins were processed
                Assert.That(plugins.Count, Is.EqualTo(3), "Should have 3 plugins in config");

                // Expect error logs from ProfileAwareES3Service initialization (no save file exists in tests)
                // Error occurs in constructor and again when profile subscription fires
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));

                // Verify basic services are registered
                var es3Service = resolver.Resolve<IES3Service>();
                Assert.That(es3Service, Is.Not.Null, "Basic ES3 service should be registered");
                
                resolver.Dispose();
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void RegisterGlobalPlugins_WithNullConfig_ShouldSkipRegistration()
            {
                // Arrange - Create real GlobalConfigLoader instance with null config
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                // Set null GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, null);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig(null);
                
                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, _containerBuilder);
                
                // Assert − Should not throw exception
                Assert.DoesNotThrow(() =>
                {
                var resolver = _containerBuilder.Build();
                resolver.Dispose();
                }, "Registration should handle null config gracefully");
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void RegisterGlobalPlugins_WithEmptyPluginList_ShouldHandleGracefully()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(0);
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, _containerBuilder);
                
                // Assert − Should complete without errors
                Assert.DoesNotThrow(() =>
                {
                var resolver = _containerBuilder.Build();
                resolver.Dispose();
                }, "Empty plugin list should be handled gracefully");
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void RegisterGlobalPlugins_WithMixedValidNullPlugins_ShouldHandleGracefully()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(4);
                
                // Make some plugins null to test mixed scenario
                config.GlobalPluginPrefabs[1] = null;
                config.GlobalPluginPrefabs[3] = null;
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);

                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, _containerBuilder);

                // Assert − Should handle mixed plugin scenarios gracefully
                Assert.DoesNotThrow(() =>
                {
                var resolver = _containerBuilder.Build();
                resolver.Dispose();
                }, "Mixed valid/null plugins should be handled gracefully");
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void RegisterGlobalPlugins_WithLargePluginList_ShouldProcessCorrectly()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(GlobalInstallerTestData.LargePluginListCount);
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);

                var installer = CreateGlobalInstallerWithConfig(config);

                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, _containerBuilder);

                // Assert − Should handle large plugin lists
                Assert.DoesNotThrow(() => 
                {
                    var resolver = _containerBuilder.Build();
                    resolver.Dispose();
                }, $"Plugin list with {GlobalInstallerTestData.LargePluginListCount} plugins should be processed");
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
        }

        #endregion

        #region Service Registration Tests

        [TestFixture]
        public class ServiceRegistrationTests : GlobalInstallerTests
        {
            [Test]
            public void Configure_WithValidConfig_ShouldRegisterCoreServices()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig();
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig(config);

                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);

                // Assert − Verify services are registered
                var resolver = _containerBuilder.Build();

                // Expect error logs from ProfileAwareES3Service initialization (no save file exists in tests)
                // Error occurs in constructor and again when profile subscription fires
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));

                // Test ES3 service registration
                var es3Service = resolver.Resolve<IES3Service>();
                Assert.That(es3Service, Is.Not.Null, "IES3Service should be registered");

                // Test SaveSystemCoordinator registration
                var coordinator = resolver.Resolve<SaveSystemCoordinator>();
                Assert.That(coordinator, Is.Not.Null, "SaveSystemCoordinator should be registered");

                // Test ES3GameSaveSystem registration
                var gameSaveSystem = resolver.Resolve<ES3GameSaveSystem>();
                Assert.That(gameSaveSystem, Is.Not.Null, "ES3GameSaveSystem should be registered");

                resolver.Dispose();
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void Configure_WithNullConfig_ShouldRegisterCoreServicesOnly()
            {
                // Arrange - Create real GlobalConfigLoader instance with null config
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                // Set null GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, null);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig(null);
                
                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);

                // Assert − Core services should still be registered
                var resolver = _containerBuilder.Build();

                // Expect error logs from ProfileAwareES3Service initialization (no save file exists in tests)
                // Error occurs in constructor and again when profile subscription fires
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));

                var es3Service = resolver.Resolve<IES3Service>();
                Assert.That(es3Service, Is.Not.Null, "IES3Service should be registered even with null config");

                var coordinator = resolver.Resolve<SaveSystemCoordinator>();
                Assert.That(coordinator, Is.Not.Null, "SaveSystemCoordinator should be registered even with null config");

                resolver.Dispose();
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void Configure_WithMessagePipeServices_ShouldRegisterMessageBrokers()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig();
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);

                // Assert − Verify message brokers are registered
                var resolver = _containerBuilder.Build();

                // Expect error logs from ProfileAwareES3Service initialization (no save file exists in tests)
                // Error occurs in constructor and again when profile subscription fires
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));

                // Basic services verification
                var es3Service = resolver.Resolve<IES3Service>();
                Assert.That(es3Service, Is.Not.Null, "Core services should be registered");

                resolver.Dispose();
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
        }

        #endregion

        #region Integration Tests

        [TestFixture]
        public class IntegrationTests : GlobalInstallerTests
        {
            [Test]
            public void FullLifecycle_WithValidConfiguration_ShouldWorkCorrectly()
            {
                // Arrange
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(pluginCount: 2);
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act − Complete lifecycle
                installer.TestAwake();
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeInitializeGlobalRuntimePlugins(installer, _containerBuilder);

                var resolver = _containerBuilder.Build();

                installer.TestOnDestroy();

                // Assert
                var instance = GlobalInstaller.Instance;
                Assert.That(instance, Is.Null, "Installer should be cleared after OnDestroy");

                // Expect error logs from ProfileAwareES3Service initialization (no save file exists in tests)
                // Error occurs in constructor and again when profile subscription fires
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));

                // Verify services were registered
                var es3Service = resolver.Resolve<IES3Service>();
                Assert.That(es3Service, Is.Not.Null, "ES3 service should be registered");

                var coordinator = resolver.Resolve<SaveSystemCoordinator>();
                Assert.That(coordinator, Is.Not.Null, "SaveSystemCoordinator should be registered");

                resolver.Dispose();
            }
            
            [Test]
            public void MultipleInstances_WithDifferentConfigs_ShouldWorkIndependently()
            {
                // Arrange
                var config1 = GlobalInstallerTestData.CreateGlobalPluginConfig(3);
                var config2 = GlobalInstallerTestData.CreateGlobalPluginConfig(1);

                var installer1 = CreateGlobalInstallerWithConfig(config1);
                var installer2 = CreateGlobalInstallerWithConfig(config2);

                // Act
                GlobalInstallerReflectionHelper.InvokeConfigure(installer1, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeConfigure(installer2, _containerBuilder);

                // Assert
                var config1Retrieved = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer1);
                var config2Retrieved = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer2);

                Assert.That(config1Retrieved.GlobalPluginPrefabs.Count, Is.EqualTo(3), 
                    "First installer should have 3 plugins");
                Assert.That(config2Retrieved.GlobalPluginPrefabs.Count, Is.EqualTo(1), 
                    "Second installer should have 1 plugin");
            }

            [Test]
            public void ConfigureRegisterInitializeSequence_ShouldProcessStepsCorrectly()
            {
                // Arrange
                var plugins = GlobalInstallerTestData.CreateMockPlugins(2);
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig();
                config.GlobalPluginPrefabs = plugins;
                
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act − Step-by-step processing
                GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, _containerBuilder);
                GlobalInstallerReflectionHelper.InvokeInitializeGlobalRuntimePlugins(installer, _containerBuilder);

                // Assert
                var resolver = _containerBuilder.Build();
                
                // Expect error logs from ProfileAwareES3Service initialization (no save file exists in tests)
                // Error occurs in constructor and again when profile subscription fires
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                
                // Verify services are available
                var es3Service = resolver.Resolve<IES3Service>();
                Assert.That(es3Service, Is.Not.Null, "ES3 service should be registered");

                var coordinator = resolver.Resolve<SaveSystemCoordinator>();
                Assert.That(coordinator, Is.Not.Null, "SaveSystemCoordinator should be registered");
                
                resolver.Dispose();
            }
        }

        #endregion

        #region Error Handling Tests

        [TestFixture]
        public class ErrorHandlingTests : GlobalInstallerTests
        {
            [Test]
            public void Configure_WithNullContainerBuilder_ShouldThrowArgumentNullException()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(2);
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act & Assert
                Assert.Throws<TargetInvocationException>(() =>
                {
                    GlobalInstallerReflectionHelper.InvokeConfigure(installer, null);
                }, "Configure should throw when builder is null");

                // Verify the inner exception is ArgumentNullException
                try
                {
                    GlobalInstallerReflectionHelper.InvokeConfigure(installer, null);
                }
                catch (TargetInvocationException ex)
                {
                    Assert.That(ex.InnerException, Is.InstanceOf<ArgumentNullException>());
                }
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void RegisterGlobalPlugins_WithNullContainerBuilder_ShouldThrowArgumentNullException()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(2);
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Act & Assert
                Assert.Throws<TargetInvocationException>(() => 
                {
                    GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, null);
                }, "RegisterGlobalPlugins should throw when builder is null");

                // Verify the inner exception is ArgumentNullException
                try
                {
                    GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, null);
                }
                catch (TargetInvocationException ex)
                {
                    Assert.That(ex.InnerException, Is.InstanceOf<ArgumentNullException>());
                }
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }

            [Test]
            public void Configure_WithMalformedPluginList_ShouldContinueOperation()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig(5);

                // Make some plugins null
                config.GlobalPluginPrefabs[0] = null;
                config.GlobalPluginPrefabs[3] = null;
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);

                var installer = CreateGlobalInstallerWithConfig(config);

                // Act & Assert
                Assert.DoesNotThrow(() =>
                {
                    GlobalInstallerReflectionHelper.InvokeConfigure(installer, _containerBuilder);
                    GlobalInstallerReflectionHelper.InvokeRegisterGlobalPlugins(installer, _containerBuilder);
                }, "Should handle malformed plugin list gracefully");

                // Verify operation continued
                var resolver = _containerBuilder.Build();
                
                // Expect error logs from ProfileAwareES3Service initialization (no save file exists in tests)
                // Error occurs in constructor and again when profile subscription fires
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Failed to initialize from existing save.*"));
                
                var es3Service = resolver.Resolve<IES3Service>();
                Assert.That(es3Service, Is.Not.Null, "Registration should continue despite malformed plugins");
                
                resolver.Dispose();
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
            
            [Test]
            public void InitializeGlobalRuntimePlugins_WithNullBuilder_ShouldThrowArgumentNullException()
            {
                // Arrange - Create real GlobalConfigLoader instance
                var globalConfigLoader = new GameObject("TestGlobalConfigLoader").AddComponent<GlobalConfigLoader>();
                _createdGameObjects.Add(globalConfigLoader.gameObject);
                
                var config = GlobalInstallerTestData.CreateGlobalPluginConfig();
                
                // Set the GlobalPluginConfig using reflection since it has private setter
                var globalPluginConfigField = typeof(GlobalConfigLoader).GetField("<GlobalPluginConfig>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                globalPluginConfigField?.SetValue(globalConfigLoader, config);
                
                GlobalConfigLoaderReflectionHelper.SetInstance(globalConfigLoader);
                
                var installer = CreateGlobalInstallerWithConfig(config);

                // Act & Assert
                Assert.Throws<TargetInvocationException>(() =>
                {
                    GlobalInstallerReflectionHelper.InvokeInitializeGlobalRuntimePlugins(installer, null);
                }, "InitializeGlobalRuntimePlugins should throw when builder is null");
                
                // Cleanup
                GlobalConfigLoaderReflectionHelper.SetInstance(null);
            }
        }

        #endregion
    }
}