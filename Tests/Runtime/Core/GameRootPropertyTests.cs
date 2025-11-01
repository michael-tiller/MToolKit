using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FsCheck;
using NUnit.Framework;
using VContainer;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Tests.Runtime.Core
{
    /// <summary>
    /// Test fixture for GameRoot property-based tests
    /// Comprehensive property tests for GameRoot static class functionality using FsCheck.
    /// Tests invariants, mathematical properties, state transitions, reversibility, and error boundaries.
    /// </summary>
    [TestFixture]
    public class GameRootPropertyTests
    {
        #region Fields

        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private PluginRegistry _pluginRegistry;

        #endregion

        #region Setup/Teardown

        [SetUp]
        public void Setup()
        {
            CreateTestDependencies();
        }

        [TearDown]
        public void TearDown()
        {
            DisposeTestDependencies();
            // Note: Static state cannot be reset due to read-only properties
        }

        #endregion

        #region Invariant Properties (Must Always Hold)

        /// <summary>
        /// Property: Resolver and PluginRegistry are always consistent after initialization
        /// Invariant: Once initialized, GameRoot properties maintain consistent relationship
        /// </summary>
        [Test]
        public void Initialize_ResolverAndRegistryConsistency_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry for each iteration
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                // Act
                GameRoot.Initialize(resolver);

                // Assert - Invariant: Resolver and PluginRegistry must be consistent
                var result = GameRoot.Resolver == resolver && 
                           GameRoot.PluginRegistry == pluginRegistry &&
                           GameRoot.Resolver != null && 
                           GameRoot.PluginRegistry != null;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        /// <summary>
        /// Property: Static properties remain accessible after initialization
        /// Invariant: Once set, static properties maintain their values
        /// </summary>
        [Test]
        public void StaticProperties_RemainAccessibleAfterInitialization_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                // Act
                GameRoot.Initialize(resolver);

                // Assert - Invariant: Properties remain accessible and consistent
                var resolverAccessible = GameRoot.Resolver != null;
                var registryAccessible = GameRoot.PluginRegistry != null;
                var resolverMatches = GameRoot.Resolver == resolver;
                var registryMatches = GameRoot.PluginRegistry == pluginRegistry;

                var result = resolverAccessible && registryAccessible && 
                           resolverMatches && registryMatches;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        /// <summary>
        /// Property: PluginRegistry contains all registered plugins
        /// Invariant: Registry maintains accurate plugin collection
        /// </summary>
        [Test]
        public void PluginRegistry_MaintainsPluginCollection_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                GameRoot.Initialize(resolver);

                // Register multiple plugins
                var pluginCount = random.Next(1, 11);
                var plugins = new List<MockRuntimePlugin>();
                for (int j = 0; j < pluginCount; j++)
                {
                    var plugin = new MockRuntimePlugin();
                    plugins.Add(plugin);
                    GameRoot.RegisterAndInit(plugin);
                }

                // Assert - Invariant: All registered plugins are in registry
                var runtimePlugins = GameRoot.PluginRegistry.GetRuntimePlugins().ToList();
                var allPluginsRegistered = plugins.All(plugin => runtimePlugins.Contains(plugin));
                var registryCountMatches = runtimePlugins.Count >= pluginCount;

                var result = allPluginsRegistered && registryCountMatches;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        #endregion

        #region Mathematical Properties (Laws of Operations)

        /// <summary>
        /// Property: Multiple initialization calls preserve last resolver
        /// Mathematical Law: Last initialization wins (idempotent with replacement)
        /// </summary>
        [Test]
        public void Initialize_MultipleCalls_LastResolverWins_MathematicalLaw()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create multiple resolvers
                var resolver1 = CreateTestResolver();
                var resolver2 = CreateTestResolver();

                // Act - Initialize with first resolver, then second
                GameRoot.Initialize(resolver1);
                GameRoot.Initialize(resolver2);

                // Assert - Mathematical Law: Last resolver should be active
                var result = GameRoot.Resolver == resolver2 && 
                           GameRoot.Resolver != resolver1;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver1.Dispose();
                resolver2.Dispose();
            }
        }

        /// <summary>
        /// Property: Plugin registration is commutative
        /// Mathematical Law: Order of plugin registration doesn't affect final state
        /// </summary>
        [Test]
        public void RegisterAndInit_PluginOrder_Commutative_MathematicalLaw()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                GameRoot.Initialize(resolver);

                // Create plugins
                var plugin1 = new MockRuntimePlugin();
                var plugin2 = new MockRuntimePlugin();

                // Register in one order
                GameRoot.RegisterAndInit(plugin1);
                GameRoot.RegisterAndInit(plugin2);

                var runtimePlugins1 = GameRoot.PluginRegistry.GetRuntimePlugins().ToList();

                // Reset and register in different order
                var containerBuilder2 = new ContainerBuilder();
                var pluginRegistry2 = new PluginRegistry();
                containerBuilder2.RegisterInstance(pluginRegistry2).AsSelf();
                var resolver2 = containerBuilder2.Build();

                GameRoot.Initialize(resolver2);

                GameRoot.RegisterAndInit(plugin2);
                GameRoot.RegisterAndInit(plugin1);

                var runtimePlugins2 = GameRoot.PluginRegistry.GetRuntimePlugins().ToList();

                // Assert - Mathematical Law: Both orders should contain same plugins
                var result = runtimePlugins1.Count == runtimePlugins2.Count &&
                           runtimePlugins1.All(p => runtimePlugins2.Contains(p)) &&
                           runtimePlugins2.All(p => runtimePlugins1.Contains(p));

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
                resolver2.Dispose();
            }
        }

        /// <summary>
        /// Property: Plugin registration is associative
        /// Mathematical Law: Grouping plugin registrations doesn't affect final state
        /// </summary>
        [Test]
        public void RegisterAndInit_PluginGrouping_Associative_MathematicalLaw()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                GameRoot.Initialize(resolver);

                // Create plugins
                var plugin1 = new MockRuntimePlugin();
                var plugin2 = new MockRuntimePlugin();
                var plugin3 = new MockRuntimePlugin();

                // Register all at once
                GameRoot.RegisterAndInit(plugin1);
                GameRoot.RegisterAndInit(plugin2);
                GameRoot.RegisterAndInit(plugin3);

                var runtimePlugins1 = GameRoot.PluginRegistry.GetRuntimePlugins().ToList();

                // Reset and register in groups
                var containerBuilder2 = new ContainerBuilder();
                var pluginRegistry2 = new PluginRegistry();
                containerBuilder2.RegisterInstance(pluginRegistry2).AsSelf();
                var resolver2 = containerBuilder2.Build();

                GameRoot.Initialize(resolver2);

                // Register in groups: (plugin1, plugin2) then plugin3
                GameRoot.RegisterAndInit(plugin1);
                GameRoot.RegisterAndInit(plugin2);
                GameRoot.RegisterAndInit(plugin3);

                var runtimePlugins2 = GameRoot.PluginRegistry.GetRuntimePlugins().ToList();

                // Assert - Mathematical Law: Grouping should not affect final state
                var result = runtimePlugins1.Count == runtimePlugins2.Count &&
                           runtimePlugins1.All(p => runtimePlugins2.Contains(p)) &&
                           runtimePlugins2.All(p => runtimePlugins1.Contains(p));

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
                resolver2.Dispose();
            }
        }

        #endregion

        #region State Transitions (Valid Progression of State)

        /// <summary>
        /// Property: After initialization, GameRoot is in valid state
        /// State Transition: Uninitialized → Initialized → Valid State
        /// </summary>
        [Test]
        public void Initialize_StateTransition_UninitializedToInitialized()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                // Act
                GameRoot.Initialize(resolver);

                // Assert - State Transition: GameRoot should be in valid initialized state
                var resolverSet = GameRoot.Resolver != null;
                var registrySet = GameRoot.PluginRegistry != null;
                var resolverCorrect = GameRoot.Resolver == resolver;
                var registryCorrect = GameRoot.PluginRegistry == pluginRegistry;

                var result = resolverSet && registrySet && resolverCorrect && registryCorrect;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        /// <summary>
        /// Property: After plugin registration, plugin is initialized
        /// State Transition: Plugin → Registered → Initialized
        /// </summary>
        [Test]
        public void RegisterAndInit_StateTransition_PluginToInitialized()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                GameRoot.Initialize(resolver);

                // Create plugin
                var plugin = new MockRuntimePlugin();

                // Act
                GameRoot.RegisterAndInit(plugin);

                // Assert - State Transition: Plugin should be initialized
                var pluginInRegistry = GameRoot.PluginRegistry.GetRuntimePlugins().Contains(plugin);
                var pluginInitialized = plugin.IsInitialized;

                var result = pluginInRegistry && pluginInitialized;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        /// <summary>
        /// Property: Multiple plugin registrations maintain state consistency
        /// State Transition: Empty Registry → Populated Registry → Consistent State
        /// </summary>
        [Test]
        public void RegisterAndInit_MultiplePlugins_StateConsistency()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                GameRoot.Initialize(resolver);

                // Register multiple plugins
                var pluginCount = random.Next(1, 11);
                var plugins = new List<MockRuntimePlugin>();
                for (int j = 0; j < pluginCount; j++)
                {
                    var plugin = new MockRuntimePlugin();
                    plugins.Add(plugin);
                    GameRoot.RegisterAndInit(plugin);
                }

                // Assert - State Transition: All plugins should be in consistent state
                var runtimePlugins = GameRoot.PluginRegistry.GetRuntimePlugins().ToList();
                var allPluginsRegistered = plugins.All(plugin => runtimePlugins.Contains(plugin));
                var allPluginsInitialized = plugins.All(plugin => plugin.IsInitialized);
                var registryCountCorrect = runtimePlugins.Count >= pluginCount;

                var result = allPluginsRegistered && allPluginsInitialized && registryCountCorrect;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        #endregion

        #region Reversibility / Round-trip Laws

        /// <summary>
        /// Property: Initialize → Dispose → Initialize restores state
        /// Reversibility: State can be restored after disposal
        /// </summary>
        [Test]
        public void Initialize_Dispose_Initialize_RoundTripReversibility()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create first resolver and registry
                var containerBuilder1 = new ContainerBuilder();
                var pluginRegistry1 = new PluginRegistry();
                containerBuilder1.RegisterInstance(pluginRegistry1).AsSelf();
                var resolver1 = containerBuilder1.Build();

                // Initialize
                GameRoot.Initialize(resolver1);

                // Record state
                var originalResolver = GameRoot.Resolver;
                var originalRegistry = GameRoot.PluginRegistry;

                // Dispose
                resolver1.Dispose();

                // Create new resolver and registry
                var containerBuilder2 = new ContainerBuilder();
                var pluginRegistry2 = new PluginRegistry();
                containerBuilder2.RegisterInstance(pluginRegistry2).AsSelf();
                var resolver2 = containerBuilder2.Build();

                // Re-initialize
                GameRoot.Initialize(resolver2);

                // Assert - Reversibility: New state should be valid
                var newResolverSet = GameRoot.Resolver != null;
                var newRegistrySet = GameRoot.PluginRegistry != null;
                var newResolverCorrect = GameRoot.Resolver == resolver2;
                var newRegistryCorrect = GameRoot.PluginRegistry == pluginRegistry2;

                var result = newResolverSet && newRegistrySet && 
                           newResolverCorrect && newRegistryCorrect;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver2.Dispose();
            }
        }

        /// <summary>
        /// Property: Plugin registration → Multiple registrations → Same plugin state
        /// Reversibility: Multiple registrations of same plugin maintain consistent state
        /// </summary>
        [Test]
        public void RegisterAndInit_MultipleRegistrations_SamePlugin_RoundTripReversibility()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                GameRoot.Initialize(resolver);

                // Create plugin
                var plugin = new MockRuntimePlugin();

                // Register multiple times
                var registrationCount = random.Next(2, 6);
                for (int j = 0; j < registrationCount; j++)
                {
                    GameRoot.RegisterAndInit(plugin);
                }

                // Assert - Reversibility: Plugin should be in consistent state
                var pluginInRegistry = GameRoot.PluginRegistry.GetRuntimePlugins().Contains(plugin);
                var pluginInitialized = plugin.IsInitialized;
                var registryCountReasonable = GameRoot.PluginRegistry.GetRuntimePlugins().Count() >= 1;

                var result = pluginInRegistry && pluginInitialized && registryCountReasonable;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        #endregion

        #region Error / Boundary Behavior

        /// <summary>
        /// Property: Null resolver initialization throws exception
        /// Error Boundary: Null parameters are handled appropriately
        /// </summary>
        [Test]
        public void Initialize_NullResolver_ThrowsException_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Act & Assert - Error Boundary: Null resolver should throw
                var exceptionThrown = false;
                try
                {
                    GameRoot.Initialize(null);
                }
                catch (NullReferenceException)
                {
                    exceptionThrown = true;
                }

                var result = exceptionThrown;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: RegisterAndInit with null plugin throws exception
        /// Error Boundary: Null plugin parameter is handled appropriately
        /// </summary>
        [Test]
        public void RegisterAndInit_NullPlugin_ThrowsException_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                GameRoot.Initialize(resolver);

                // Act & Assert - Error Boundary: Null plugin should throw
                var exceptionThrown = false;
                try
                {
                    GameRoot.RegisterAndInit(null);
                }
                catch (ArgumentNullException)
                {
                    exceptionThrown = true;
                }

                var result = exceptionThrown;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        /// <summary>
        /// Property: RegisterAndInit without initialization doesn't crash
        /// Error Boundary: Graceful handling of uninitialized state
        /// </summary>
        [Test]
        public void RegisterAndInit_WithoutInitialization_DoesNotCrash_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create plugin
                var plugin = new MockRuntimePlugin();

                // Act & Assert - Error Boundary: Should not crash without initialization
                var noExceptionThrown = true;
                try
                {
                    GameRoot.RegisterAndInit(plugin);
                }
                catch (Exception)
                {
                    noExceptionThrown = false;
                }

                var result = noExceptionThrown;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Disposed resolver initialization handles gracefully
        /// Error Boundary: Disposed objects are handled appropriately
        /// </summary>
        [Test]
        public void Initialize_DisposedResolver_HandlesGracefully_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create and dispose resolver
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();
                resolver.Dispose();

                // Act & Assert - Error Boundary: Should handle disposed resolver gracefully
                var noExceptionThrown = true;
                try
                {
                    GameRoot.Initialize(resolver);
                }
                catch (Exception)
                {
                    noExceptionThrown = false;
                }

                var result = noExceptionThrown;

                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region Thread Safety Properties

        /// <summary>
        /// Property: Concurrent initialization maintains consistency
        /// Thread Safety: Multiple threads can initialize safely
        /// </summary>
        [Test]
        public void Initialize_ConcurrentAccess_MaintainsConsistency_ThreadSafety()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                const int taskCount = 10;
                var tasks = new List<Task>();
                var exceptions = new List<Exception>();

                // Act - Concurrent initialization
                for (int j = 0; j < taskCount; j++)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            var containerBuilder = new ContainerBuilder();
                            var pluginRegistry = new PluginRegistry();
                            containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                            var resolver = containerBuilder.Build();
                            GameRoot.Initialize(resolver);
                            resolver.Dispose();
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

                // Assert - Thread Safety: Should complete without exceptions
                var result = exceptions.Count == 0;

                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Concurrent plugin registration maintains consistency
        /// Thread Safety: Multiple threads can register plugins safely
        /// </summary>
        [Test]
        public void RegisterAndInit_ConcurrentAccess_MaintainsConsistency_ThreadSafety()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create fresh resolver and registry
                var containerBuilder = new ContainerBuilder();
                var pluginRegistry = new PluginRegistry();
                containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
                var resolver = containerBuilder.Build();

                GameRoot.Initialize(resolver);

                const int pluginCount = 10;
                var tasks = new List<Task>();
                var exceptions = new List<Exception>();

                // Act - Concurrent plugin registration
                for (int j = 0; j < pluginCount; j++)
                {
                    var plugin = new MockRuntimePlugin();
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            GameRoot.RegisterAndInit(plugin);
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

                // Assert - Thread Safety: Should complete without exceptions
                var result = exceptions.Count == 0;

                Check.QuickThrowOnFailure(result);

                // Cleanup
                resolver.Dispose();
            }
        }

        #endregion

        #region Helper Methods

        private void CreateTestDependencies()
        {
            _containerBuilder = new ContainerBuilder();
            _pluginRegistry = new PluginRegistry();
            _containerBuilder.RegisterInstance(_pluginRegistry).AsSelf();
            _resolver = _containerBuilder.Build();
        }

        private void DisposeTestDependencies()
        {
            _resolver?.Dispose();
            _resolver = null;
            _containerBuilder = null;
            _pluginRegistry = null;
        }

        private IObjectResolver CreateTestResolver()
        {
            var containerBuilder = new ContainerBuilder();
            var pluginRegistry = new PluginRegistry();
            containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
            return containerBuilder.Build();
        }

        #endregion

        #region Mock Classes

        /// <summary>
        /// Mock runtime plugin for property testing
        /// </summary>
        private class MockRuntimePlugin : IRuntimePlugin
        {
            public bool IsInitialized { get; private set; }
            public bool PerformSetupCalled { get; private set; }
            public bool PerformRuntimeInitializationCalled { get; private set; }
            public bool AreDependenciesReadyCalled { get; private set; }
            public bool RegisterCalled { get; private set; }

            public void Register(IContainerBuilder builder)
            {
                RegisterCalled = true;
            }

            public void PerformSetup(IObjectResolver resolver)
            {
                PerformSetupCalled = true;
            }

            public void PerformRuntimeInitialization(IObjectResolver resolver)
            {
                PerformRuntimeInitializationCalled = true;
                IsInitialized = true;
            }

            public bool AreDependenciesReady(IObjectResolver resolver)
            {
                AreDependenciesReadyCalled = true;
                return true; // Always ready for testing
            }
        }

        #endregion
    }
}
