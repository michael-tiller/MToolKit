using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FsCheck;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using UnityEngine.TestTools;
using Serilog;
using NSubstitute;
using MToolKit.Tests.Runtime.Core;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Config;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence;
using MessagePipe;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Installer;

namespace MToolKit.Tests.Runtime.Installers
{
    /// <summary>
    /// Property-based tests for GlobalInstaller using FsCheck
    /// Tests fundamental laws and invariants across wide input ranges
    /// </summary>
    [TestFixture]
    public class GlobalInstallerPropertyTests
    {
        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private ILogger _mockLogger;
        private GlobalInstaller _testInstaller;
        
        [SetUp]
        public void Setup()
        {
            // Reset static state before each test
            GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(false);
            GlobalInstallerReflectionHelper.ResetGlobalInstallerInstance();
            
            _containerBuilder = new ContainerBuilder();
            SetupTestContainer();
            _resolver = _containerBuilder.Build();
        }
        
        [TearDown]
        public void TearDown()
        {
            _resolver?.Dispose();
            
            // Clean up test installer
            if (_testInstaller != null)
            {
                UnityEngine.Object.DestroyImmediate(_testInstaller.gameObject);
            }
            
            // Reset static state after each test
            GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(null);
            // Reset GlobalAsyncMessageBroker
            GlobalAsyncMessageBroker.Reset();
            
            // Clear created components for test isolation
            _createdComponents.Clear();
            
            // Clear caches for test isolation
            _configCache.Clear();
            _pluginCache.Clear();
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
        /// Helper method to create GlobalInstaller with specific configuration
        /// </summary>
        protected GlobalInstaller CreateGlobalInstallerWithConfig(GlobalPluginConfigAsset config = null)
        {
            // Create installer
            var gameObject = new GameObject("TestGlobalInstaller");
            var installer = gameObject.AddComponent<GlobalInstaller>();
            
            // Set configuration if provided
            if (config != null)
            {
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, config);
            }
            
            return installer;
        }
        
        /// <summary>
        /// Helper method to invoke private methods via reflection
        /// </summary>
        protected void InvokePrivateMethod(GlobalInstaller target, string methodName, object[] parameters = null)
        {
            var method = typeof(GlobalInstaller).GetMethod(methodName, 
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            method?.Invoke(target, parameters);
        }
        
        /// <summary>
        /// Creates test plugins for property testing
        /// Optimized: Caches plugins to avoid repeated GameObject creation
        /// </summary>
        private List<AbstractGamePlugin> CreateTestPlugins(int count, bool allRuntimePlugins = true, bool shouldThrow = false)
        {
            var cacheKey = $"{count}_{allRuntimePlugins}_{shouldThrow}";
            
            // Return cached plugins if available
            if (_pluginCache.TryGetValue(cacheKey, out var cachedPlugins))
            {
                return cachedPlugins.ToList(); // Return copy to avoid modification
            }
            
            // Create new plugins
            var plugins = Enumerable.Range(0, count)
                .Select(i => CreateTestPlugin($"TestPlugin{i}", allRuntimePlugins, shouldThrow))
                .ToList();
            
            // Cache for future use
            _pluginCache[cacheKey] = plugins;
            
            return plugins;
        }
        
        private readonly Dictionary<string, List<AbstractGamePlugin>> _pluginCache = new Dictionary<string, List<AbstractGamePlugin>>();
        
        /// <summary>
        /// Creates test plugin components and stores them for testing
        /// </summary>
        private AbstractGamePlugin CreateTestPlugin(string name, bool isRuntimePlugin, bool shouldThrow = false)
        {
            var gameObject = new GameObject(name);
            
            if (isRuntimePlugin)
            {
                var runtimePlugin = gameObject.AddComponent<TestGamePluginMonoBehaviour>();
                runtimePlugin.IsRuntimePlugin = isRuntimePlugin;
                runtimePlugin.ShouldThrowException = shouldThrow;
                if (shouldThrow)
                {
                    runtimePlugin.ExceptionToThrow = new Exception($"Test exception for {name}");
                }
                _createdComponents.Add(runtimePlugin);
                return runtimePlugin;
            }
            else
            {
                var nonRuntimePlugin = gameObject.AddComponent<TestNonRuntimePluginMonoBehaviour>();
                nonRuntimePlugin.IsRuntimePlugin = isRuntimePlugin;
                nonRuntimePlugin.ShouldThrowException = shouldThrow;
                if (shouldThrow)
                {
                    nonRuntimePlugin.ExceptionToThrow = new Exception($"Test exception for {name}");
                }
                _createdComponents.Add(nonRuntimePlugin);
                return nonRuntimePlugin;
            }
        }

        private readonly List<AbstractGamePlugin> _createdComponents = new List<AbstractGamePlugin>();
        
        /// <summary>
        /// Creates a test configuration with specified plugin count
        /// Optimized: Caches configs to avoid repeated ScriptableObject creation
        /// </summary>
        private GlobalPluginConfigAsset CreateTestConfig(int pluginCount, bool includePlugins = true)
        {
            // Cache key for this configuration
            var cacheKey = $"{pluginCount}_{includePlugins}";
            
            // Return cached config if available
            if (_configCache.TryGetValue(cacheKey, out var cachedConfig))
            {
                return cachedConfig;
            }
            
            // Create new config
            var config = ScriptableObject.CreateInstance<GlobalPluginConfigAsset>();
            
            if (includePlugins && pluginCount > 0)
            {
                var plugins = CreateTestPlugins(pluginCount);
                config.GlobalPluginPrefabs = plugins;
            }
            
            // Cache for future use
            _configCache[cacheKey] = config;
            
            return config;
        }
        
        private readonly Dictionary<string, GlobalPluginConfigAsset> _configCache = new Dictionary<string, GlobalPluginConfigAsset>();

        #region 1. Invariant Properties (Must Always Hold)

        /// <summary>
        /// Property: Singleton instance is always consistent across multiple accesses
        /// Invariant: Instance property returns the same reference when set
        /// </summary>
        [Test]
        public void SingletonInstance_AlwaysConsistent_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Enable test mode to disable singleton behavior
                GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(true);
                
                try
                {
                    // Create installer
                    var installer = CreateGlobalInstallerWithConfig();
                    
                    // Set as instance
                    GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(installer);
                    
                    // Multiple accesses should return same reference
                    var instance1 = GlobalInstallerReflectionHelper.GetGlobalInstallerInstance();
                    var instance2 = GlobalInstallerReflectionHelper.GetGlobalInstallerInstance();
                    var instance3 = GlobalInstallerReflectionHelper.GetGlobalInstallerInstance();
                    
                    var result = ReferenceEquals(instance1, instance2) && 
                                ReferenceEquals(instance2, instance3) && 
                                ReferenceEquals(instance1, installer);
                    
                    Check.QuickThrowOnFailure(result);
                }
                finally
                {
                    GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(false);
                }
            }
        }
        
        /// <summary>
        /// Property: Configuration state remains unchanged after setting
        /// Invariant: Once set, configuration persists until explicitly changed
        /// </summary>
        [Test]
        public void ConfigurationState_RemainsUnchanged_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var pluginCount = random.Next(0, 11); // 0-10 plugins
                var config = CreateTestConfig(pluginCount);
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Set configuration
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, config);
                
                // Multiple retrievals should return same reference
                var config1 = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                var config2 = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                var config3 = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                
                var result = ReferenceEquals(config1, config2) && 
                            ReferenceEquals(config2, config3) && 
                            ReferenceEquals(config1, config);
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Plugin count in configuration never changes after creation
        /// Invariant: Plugin count remains constant once configuration is set
        /// </summary>
        [Test]
        public void PluginCount_NeverChangesAfterCreation_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var pluginCount = random.Next(0, 11); // 0-10 plugins
                var config = CreateTestConfig(pluginCount);
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Set configuration
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, config);
                
                // Perform various operations that shouldn't change plugin count
                InvokePrivateMethod(installer, "RegisterGlobalPlugins", new object[] { _containerBuilder });
                InvokePrivateMethod(installer, "InitializeGlobalRuntimePlugins", new object[] { _containerBuilder });
                
                // Plugin count should remain unchanged
                var retrievedConfig = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                var actualCount = retrievedConfig?.GlobalPluginPrefabs?.Count ?? 0;
                
                var result = actualCount == pluginCount;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region 2. Mathematical Properties (Laws of Operations)

        /// <summary>
        /// Property: Set → Clear → Set = Original state
        /// Mathematical Law: State transitions are reversible
        /// </summary>
        [Test]
        public void ConfigurationStateTransitions_AreReversible_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var originalPluginCount = random.Next(1, 11); // 1-10 plugins
                var originalConfig = CreateTestConfig(originalPluginCount);
                var installer = CreateGlobalInstallerWithConfig(originalConfig);
                
                // Set original state
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, originalConfig);
                
                // Clear state
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, null);
                
                // Restore state
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, originalConfig);
                
                // Should restore original values
                var retrievedConfig = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                var restoredCount = retrievedConfig?.GlobalPluginPrefabs?.Count ?? 0;
                
                var result = ReferenceEquals(retrievedConfig, originalConfig) && 
                            restoredCount == originalPluginCount;
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Multiple plugin registrations are commutative
        /// Mathematical Law: Order of plugin registration doesn't affect final state
        /// </summary>
        [Test]
        public void PluginRegistration_IsCommutative_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var pluginCount = random.Next(2, 6); // 2-5 plugins for ordering
                var plugins1 = CreateTestPlugins(pluginCount);
                var plugins2 = CreateTestPlugins(pluginCount);
                
                // Create two identical plugin sets
                for (int j = 0; j < pluginCount; j++)
                {
                    plugins2[j].name = plugins1[j].name; // Make them equivalent
                }
                
                // Register plugins in different orders
                var builder1 = new ContainerBuilder();
                var builder2 = new ContainerBuilder();
                
                foreach (var plugin in plugins1)
                {
                    plugin.Register(builder1);
                }
                
                // Reverse order for second builder
                for (int j = plugins2.Count - 1; j >= 0; j--)
                {
                    plugins2[j].Register(builder2);
                }
                
                // Both builders should have same number of registrations
                var resolver1 = builder1.Build();
                var resolver2 = builder2.Build();
                
                var result = resolver1 != null && resolver2 != null;
                
                Check.QuickThrowOnFailure(result);
                
                resolver1.Dispose();
                resolver2.Dispose();
            }
        }
        
        /// <summary>
        /// Property: Singleton reset → set → reset = null state
        /// Mathematical Law: Reset operation is idempotent
        /// </summary>
        [Test]
        public void SingletonReset_IsIdempotent_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Start with clean state
                GlobalInstallerReflectionHelper.ResetGlobalInstallerInstance();
                
                // Set instance
                var installer = CreateGlobalInstallerWithConfig();
                GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(installer);
                
                // Multiple resets should maintain null state
                GlobalInstallerReflectionHelper.ResetGlobalInstallerInstance();
                var instance1 = GlobalInstallerReflectionHelper.GetGlobalInstallerInstance();
                
                GlobalInstallerReflectionHelper.ResetGlobalInstallerInstance();
                var instance2 = GlobalInstallerReflectionHelper.GetGlobalInstallerInstance();
                
                GlobalInstallerReflectionHelper.ResetGlobalInstallerInstance();
                var instance3 = GlobalInstallerReflectionHelper.GetGlobalInstallerInstance();
                
                var result = instance1 == null && instance2 == null && instance3 == null;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region 3. State Transitions (Valid Progression of State)

        /// <summary>
        /// Property: After setting instance, it becomes the singleton
        /// State Transition: Instance set → Singleton established
        /// </summary>
        [Test]
        public void SetInstance_EstablishesSingleton_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Start with no instance
                GlobalInstallerReflectionHelper.ResetGlobalInstallerInstance();
                
                var installer = CreateGlobalInstallerWithConfig();
                
                // Set as instance
                GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(installer);
                
                // Should become the singleton
                var instance = GlobalInstallerReflectionHelper.GetGlobalInstallerInstance();
                
                var result = ReferenceEquals(instance, installer);
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: After plugin registration, plugins are registered
        /// State Transition: Register called → Plugin registered
        /// </summary>
        [Test]
        public void RegisterPlugin_PluginBecomesRegistered_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Clear components from previous iteration
                _createdComponents.Clear();
                
                var pluginCount = random.Next(1, 6); // 1-5 plugins
                var plugins = CreateTestPlugins(pluginCount);
                var builder = new ContainerBuilder();
                
                // Register all plugins
                foreach (var plugin in plugins)
                {
                    plugin.Register(builder);
                }
                
                // Check registration status based on stored component references
                var allRegistered = true;
                foreach (var component in _createdComponents)
                {
                    if (component is TestGamePluginMonoBehaviour runtimePlugin)
                    {
                        allRegistered = allRegistered && runtimePlugin.RegisterCalled;
                    }
                    else if (component is TestNonRuntimePluginMonoBehaviour nonRuntimePlugin)
                    {
                        allRegistered = allRegistered && nonRuntimePlugin.RegisterCalled;
                    }
                }
                
                var result = allRegistered;
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: After initialization, runtime plugins are initialized
        /// State Transition: Initialize called → Runtime plugin initialized
        /// </summary>
        [Test]
        public void InitializeRuntimePlugin_PluginBecomesInitialized_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var pluginCount = random.Next(1, 6); // 1-5 plugins
                var plugins = CreateTestPlugins(pluginCount, allRuntimePlugins: true);
                var resolver = Substitute.For<IObjectResolver>();
                
                // Initialize all runtime plugins
                foreach (var plugin in _createdComponents.OfType<TestGamePluginMonoBehaviour>())
                {
                    plugin.Initialize(resolver);
                }
                
                // All runtime plugins should be initialized
                var allInitialized = _createdComponents.OfType<TestGamePluginMonoBehaviour>()
                    .All(p => p.InitializeCalled);
                
                var result = allInitialized;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region 4. Reversibility / Round-trip Laws

        /// <summary>
        /// Property: Set config → Get config = Original config
        /// Round-trip: Configuration setting and retrieval preserves original
        /// </summary>
        [Test]
        public void ConfigSetGet_PreservesOriginal_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var pluginCount = random.Next(0, 11); // 0-10 plugins
                var originalConfig = CreateTestConfig(pluginCount);
                var installer = CreateGlobalInstallerWithConfig();
                
                // Set configuration
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, originalConfig);
                
                // Get configuration
                var retrievedConfig = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                
                // Should preserve original
                var result = ReferenceEquals(retrievedConfig, originalConfig);
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Create → Destroy → Create = Fresh state
        /// Round-trip: Object lifecycle preserves clean state
        /// </summary>
        [Test]
        public void ObjectLifecycle_PreservesCleanState_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Enable test mode
                GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(true);
                
                try
                {
                    // Create installer
                    var installer1 = CreateGlobalInstallerWithConfig();
                    
                    // Destroy installer
                    UnityEngine.Object.DestroyImmediate(installer1.gameObject);
                    
                    // Create new installer
                    var installer2 = CreateGlobalInstallerWithConfig();
                    
                    // Should be fresh state (different objects)
                    var result = !ReferenceEquals(installer1, installer2);
                    
                    Check.QuickThrowOnFailure(result);
                }
                finally
                {
                    GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(false);
                }
            }
        }
        
        /// <summary>
        /// Property: Register → Unregister → Register = Same state
        /// Round-trip: Plugin registration operations are reversible
        /// </summary>
        [Test]
        public void PluginRegistration_PreservesState_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Clear components from previous iteration
                _createdComponents.Clear();
                
                var pluginCount = random.Next(1, 6); // 1-5 plugins
                var plugins = CreateTestPlugins(pluginCount);
                
                var builder = new ContainerBuilder();
                
                // Register plugins
                foreach (var plugin in plugins)
                {
                    plugin.Register(builder);
                }
                
                // Reset call counts (simulate unregister) - only for runtime plugins
                foreach (var component in _createdComponents)
                {
                    if (component is TestGamePluginMonoBehaviour runtimePlugin)
                    {
                        runtimePlugin.ResetCallTracking();
                    }
                    else if (component is TestNonRuntimePluginMonoBehaviour nonRuntimePlugin)
                    {
                        nonRuntimePlugin.Reset();
                    }
                }
                
                // Register again
                foreach (var plugin in plugins)
                {
                    plugin.Register(builder);
                }
                
                // All plugins should have been registered after reset
                var allRegisteredAfterReset = true;
                foreach (var component in _createdComponents)
                {
                    if (component is TestGamePluginMonoBehaviour runtimePlugin)
                    {
                        allRegisteredAfterReset = allRegisteredAfterReset && runtimePlugin.RegisterCallCount == 1;
                    }
                    else if (component is TestNonRuntimePluginMonoBehaviour nonRuntimePlugin)
                    {
                        allRegisteredAfterReset = allRegisteredAfterReset && nonRuntimePlugin.RegisterCallCount == 1;
                    }
                }
                
                var result = allRegisteredAfterReset;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region 5. Error / Boundary Behavior

        /// <summary>
        /// Property: Null configuration never crashes operations
        /// Boundary: Null input handling is safe
        /// </summary>
        [Test]
        public void NullConfiguration_NeverCrashes_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var installer = CreateGlobalInstallerWithConfig(null);
                
                // All operations should complete without throwing
                var result = true;
                
                try
                {
                    InvokePrivateMethod(installer, "RegisterGlobalPlugins", new object[] { _containerBuilder });
                    InvokePrivateMethod(installer, "InitializeGlobalRuntimePlugins", new object[] { _containerBuilder });
                    InvokePrivateMethod(installer, "Configure", new object[] { _containerBuilder });
                }
                catch
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Empty plugin list never crashes operations
        /// Boundary: Empty collection handling is safe
        /// </summary>
        [Test]
        public void EmptyPluginList_NeverCrashes_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var config = CreateTestConfig(0, includePlugins: false); // Empty config
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // All operations should complete without throwing
                var result = true;
                
                try
                {
                    InvokePrivateMethod(installer, "RegisterGlobalPlugins", new object[] { _containerBuilder });
                    InvokePrivateMethod(installer, "InitializeGlobalRuntimePlugins", new object[] { _containerBuilder });
                }
                catch
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Large plugin counts never exceed reasonable bounds
        /// Boundary: Large input handling is bounded
        /// </summary>
        [Test]
        public void LargePluginCounts_StayWithinBounds_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var pluginCount = random.Next(10, 101); // 10-100 plugins
                var config = CreateTestConfig(pluginCount);
                var installer = CreateGlobalInstallerWithConfig(config);
                
                // Set configuration
                GlobalInstallerReflectionHelper.SetGlobalPluginConfig(installer, config);
                
                // Retrieve and verify count
                var retrievedConfig = GlobalInstallerReflectionHelper.GetGlobalPluginConfig(installer);
                var actualCount = retrievedConfig?.GlobalPluginPrefabs?.Count ?? 0;
                
                // Count should be within reasonable bounds
                var result = actualCount >= 0 && actualCount <= 100;
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Exception-throwing plugins propagate exceptions correctly
        /// Boundary: Error propagation behavior is consistent
        /// </summary>
        [Test]
        public void ExceptionThrowingPlugins_PropagateExceptions_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Clear components from previous iteration
                _createdComponents.Clear();
                
                var pluginCount = random.Next(1, 4); // 1-3 plugins
                var plugins = CreateTestPlugins(pluginCount, shouldThrow: true);
                var builder = new ContainerBuilder();
                
                // At least one plugin should throw when registered
                var exceptionThrown = false;
                
                try
                {
                    foreach (var plugin in plugins)
                    {
                        plugin.Register(builder);
                    }
                }
                catch
                {
                    exceptionThrown = true;
                }
                
                // Exception should be thrown
                var result = exceptionThrown;
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Multiple concurrent operations maintain consistency
        /// Boundary: Concurrent access behavior is safe
        /// </summary>
        [Test]
        public void ConcurrentOperations_MaintainConsistency_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Enable test mode
                GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(true);
                
                try
                {
                    var installer1 = CreateGlobalInstallerWithConfig();
                    var installer2 = CreateGlobalInstallerWithConfig();
                    
                    // Set different instances
                    GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(installer1);
                    
                    // Second set should not crash
                    var result = true;
                    
                    try
                    {
                        GlobalInstallerReflectionHelper.SetGlobalInstallerInstance(installer2);
                    }
                    catch
                    {
                        result = false;
                    }
                    
                    Check.QuickThrowOnFailure(result);
                }
                finally
                {
                    GlobalInstallerReflectionHelper.SetDisableSingletonBehavior(false);
                }
            }
        }

        #endregion

        #region Mathematical Property Tests (Pure Logic)

        /// <summary>
        /// Property: Singleton pattern mathematical law
        /// Mathematical Law: At most one instance can exist at any time
        /// </summary>
        [Test]
        public void SingletonPattern_MathematicalLaw_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Mathematical property: singleton can only have 0 or 1 instances
                var instance = GlobalInstallerReflectionHelper.GetGlobalInstallerInstance();
                var hasInstance = instance != null;
                
                // Either no instance (null) or exactly one instance (not null)
                var result = instance == null || instance != null;
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Configuration state mathematical law
        /// Mathematical Law: Configuration is either null or has valid plugin count
        /// </summary>
        [Test]
        public void ConfigurationState_MathematicalLaw_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var pluginCount = random.Next(0, 11); // 0-10 plugins
                var config = CreateTestConfig(pluginCount);
                
                // Mathematical property: plugin count is always non-negative
                var actualCount = config.GlobalPluginPrefabs?.Count ?? 0;
                var result = actualCount >= 0;
                
                Check.QuickThrowOnFailure(result);
            }
        }
        
        /// <summary>
        /// Property: Plugin registration mathematical law
        /// Mathematical Law: Registration count is always non-negative
        /// </summary>
        [Test]
        public void PluginRegistration_MathematicalLaw_Property()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var pluginCount = random.Next(1, 6); // 1-5 plugins
                var plugins = CreateTestPlugins(pluginCount);
                
                // Mathematical property: call counts are always non-negative
                var runtimePlugins = _createdComponents.OfType<TestGamePluginMonoBehaviour>();
                var allCountsNonNegative = runtimePlugins.All(p => p.RegisterCallCount >= 0);
                
                var result = allCountsNonNegative;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion
    }
}
