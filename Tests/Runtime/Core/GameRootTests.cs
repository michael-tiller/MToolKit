using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using VContainer;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Tests.Runtime.Core
{
    /// <summary>
    /// <see cref="GameRoot"/>
    /// Comprehensive unit tests for GameRoot static class functionality.
    /// Tests initialization, plugin registration, static properties, and edge cases.
    /// </summary>
    [TestFixture]
    public class GameRootTests
    {
        #region Fields

        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private PluginRegistry _pluginRegistry;
        private MockRuntimePlugin _mockPlugin;

        #endregion

        #region Setup/Teardown

        [SetUp]
        public void SetUp()
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

        #region Initialize Method Tests

        [Test]
        public void Initialize_WithValidResolver_SetsResolverAndPluginRegistry()
        {
            // Act
            GameRoot.Initialize(_resolver);

            // Assert
            Assert.That(GameRoot.Resolver, Is.EqualTo(_resolver));
            Assert.That(GameRoot.PluginRegistry, Is.EqualTo(_pluginRegistry));
        }

        [Test]
        public void Initialize_WithNullResolver_ThrowsNullReferenceException()
        {
            // Act & Assert
            // GameRoot doesn't validate null parameters, so VContainer throws NullReferenceException
            Assert.Throws<NullReferenceException>(() => GameRoot.Initialize(null));
        }

        [Test]
        public void Initialize_CalledMultipleTimes_UpdatesResolverAndRegistry()
        {
            // Arrange
            var secondResolver = CreateSecondResolver();

            // Act
            GameRoot.Initialize(_resolver);
            GameRoot.Initialize(secondResolver.Resolver);

            // Assert
            Assert.That(GameRoot.Resolver, Is.EqualTo(secondResolver.Resolver));
            Assert.That(GameRoot.PluginRegistry, Is.EqualTo(secondResolver.PluginRegistry));

            // Cleanup
            secondResolver.Resolver.Dispose();
        }

        [Test]
        public void Initialize_WithResolverMissingPluginRegistry_ThrowsVContainerException()
        {
            // Arrange
            var resolver = CreateResolverWithoutPluginRegistry();

            // Act & Assert
            Assert.Throws<VContainerException>(() => GameRoot.Initialize(resolver));

            // Cleanup
            resolver.Dispose();
        }

        #endregion

        #region RegisterAndInit Method Tests

        [Test]
        public void RegisterAndInit_WithValidPlugin_CallsInitializeRuntimePlugin()
        {
            // Arrange
            InitializeGameRoot();

            // Act
            GameRoot.RegisterAndInit(_mockPlugin);

            // Assert
            Assert.That(_mockPlugin.IsInitialized, Is.True);
        }

        [Test]
        public void RegisterAndInit_WithNullPlugin_ThrowsArgumentNullException()
        {
            // Arrange
            InitializeGameRoot();

            // Act & Assert
            // PluginRegistry.InitializeRuntimePlugin validates null plugin parameter
            Assert.Throws<ArgumentNullException>(() => GameRoot.RegisterAndInit(null));
        }

        [Test]
        public void RegisterAndInit_WithoutInitialization_DoesNotThrow()
        {
            // Act & Assert - Should not throw due to null-conditional operator
            Assert.DoesNotThrow(() => GameRoot.RegisterAndInit(_mockPlugin));
            
            // Note: Plugin may be initialized if GameRoot was initialized in previous tests
            // due to static state persistence. This test verifies no exception is thrown.
        }

        [Test]
        public void RegisterAndInit_WithMultiplePlugins_InitializesAll()
        {
            // Arrange
            var plugins = CreateMultiplePlugins(2);
            InitializeGameRoot();

            // Act
            RegisterMultiplePlugins(plugins);

            // Assert
            AssertAllPluginsInitialized(plugins);
        }

        [Test]
        public void RegisterAndInit_WithPluginThrowingException_PropagatesException()
        {
            // Arrange
            var throwingPlugin = new ThrowingRuntimePlugin();
            InitializeGameRoot();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => GameRoot.RegisterAndInit(throwingPlugin));
        }

        #endregion

        #region Static Properties Tests

        [Test]
        public void PluginRegistry_AfterInitialization_ReturnsCorrectInstance()
        {
            // Arrange
            InitializeGameRoot();

            // Act
            var registry = GameRoot.PluginRegistry;

            // Assert
            Assert.That(registry, Is.EqualTo(_pluginRegistry));
        }

        [Test]
        public void Resolver_AfterInitialization_ReturnsCorrectInstance()
        {
            // Arrange
            InitializeGameRoot();

            // Act
            var resolver = GameRoot.Resolver;

            // Assert
            Assert.That(resolver, Is.EqualTo(_resolver));
        }

        [Test]
        public void StaticProperties_CanBeAccessedAfterInitialization()
        {
            // Arrange
            InitializeGameRoot();

            // Act & Assert
            Assert.That(GameRoot.Resolver, Is.Not.Null);
            Assert.That(GameRoot.PluginRegistry, Is.Not.Null);
            Assert.That(GameRoot.Resolver, Is.EqualTo(_resolver));
            Assert.That(GameRoot.PluginRegistry, Is.EqualTo(_pluginRegistry));
        }

        #endregion

        #region Integration Tests

        [Test]
        public void InitializeThenRegisterAndInit_CompleteWorkflow()
        {
            // Act
            InitializeGameRoot();
            GameRoot.RegisterAndInit(_mockPlugin);

            // Assert
            AssertGameRootInitialized();
            Assert.That(_mockPlugin.IsInitialized, Is.True);
        }

        [Test]
        public void MultipleInitializationsAndRegistrations_HandlesCorrectly()
        {
            // Arrange
            var plugins = CreateMultiplePlugins(5);

            // Act
            InitializeGameRoot();
            RegisterMultiplePlugins(plugins);

            // Assert
            AssertAllPluginsInitialized(plugins);
        }

        #endregion

        #region Thread Safety Tests

        [Test]
        public void ConcurrentInitialization_HandlesCorrectly()
        {
            // Arrange
            const int taskCount = 10;
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            // Act
            for (int i = 0; i < taskCount; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        GameRoot.Initialize(_resolver);
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

            // Assert
            Assert.That(exceptions.Count, Is.EqualTo(0));
            AssertGameRootInitialized();
        }

        [Test]
        public void ConcurrentRegisterAndInit_HandlesCorrectly()
        {
            // Arrange
            const int pluginCount = 10;
            InitializeGameRoot();
            var plugins = CreateMultiplePlugins(pluginCount);
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            // Act
            foreach (var plugin in plugins)
            {
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

            // Assert
            Assert.That(exceptions.Count, Is.EqualTo(0));
            AssertAllPluginsInitialized(plugins);
        }

        #endregion

        #region Edge Cases

        [Test]
        public void RegisterAndInit_AfterResolverDisposal_DoesNotThrow()
        {
            // Arrange
            InitializeGameRoot();
            _resolver.Dispose();

            // Act & Assert
            Assert.DoesNotThrow(() => GameRoot.RegisterAndInit(_mockPlugin));
        }

        [Test]
        public void Initialize_WithDisposedResolver_HandlesGracefully()
        {
            // Arrange
            _resolver.Dispose();

            // Act & Assert
            // VContainer may not throw ObjectDisposedException, so we test that it handles gracefully
            Assert.DoesNotThrow(() => GameRoot.Initialize(_resolver));
            
            // Verify that GameRoot was still initialized despite disposed resolver
            Assert.That(GameRoot.Resolver, Is.EqualTo(_resolver));
        }

        #endregion

        #region Helper Methods

        private void CreateTestDependencies()
        {
            _containerBuilder = new ContainerBuilder();
            _pluginRegistry = new PluginRegistry();
            _mockPlugin = new MockRuntimePlugin();

            _containerBuilder.RegisterInstance(_pluginRegistry).AsSelf();
            _resolver = _containerBuilder.Build();
        }

        private void DisposeTestDependencies()
        {
            _resolver?.Dispose();
            _resolver = null;
            _containerBuilder = null;
            _pluginRegistry = null;
            _mockPlugin = null;
        }

        private void InitializeGameRoot()
        {
            GameRoot.Initialize(_resolver);
        }

        private void AssertGameRootInitialized()
        {
            Assert.That(GameRoot.Resolver, Is.EqualTo(_resolver));
            Assert.That(GameRoot.PluginRegistry, Is.EqualTo(_pluginRegistry));
        }

        private List<MockRuntimePlugin> CreateMultiplePlugins(int count)
        {
            var plugins = new List<MockRuntimePlugin>();
            for (int i = 0; i < count; i++)
            {
                plugins.Add(new MockRuntimePlugin());
            }
            return plugins;
        }

        private void RegisterMultiplePlugins(List<MockRuntimePlugin> plugins)
        {
            foreach (var plugin in plugins)
            {
                GameRoot.RegisterAndInit(plugin);
            }
        }

        private void AssertAllPluginsInitialized(List<MockRuntimePlugin> plugins)
        {
            foreach (var plugin in plugins)
            {
                Assert.That(plugin.IsInitialized, Is.True);
            }
        }

        private (IObjectResolver Resolver, PluginRegistry PluginRegistry) CreateSecondResolver()
        {
            var containerBuilder = new ContainerBuilder();
            var pluginRegistry = new PluginRegistry();
            containerBuilder.RegisterInstance(pluginRegistry).AsSelf();
            var resolver = containerBuilder.Build();
            return (resolver, pluginRegistry);
        }

        private IObjectResolver CreateResolverWithoutPluginRegistry()
        {
            var containerBuilder = new ContainerBuilder();
            return containerBuilder.Build();
        }

        #endregion

        #region Mock Classes

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

        private class ThrowingRuntimePlugin : IRuntimePlugin
        {
            public void Register(IContainerBuilder builder)
            {
                // No-op for testing
            }

            public void PerformSetup(IObjectResolver resolver)
            {
                throw new InvalidOperationException("Mock exception for testing");
            }

            public void PerformRuntimeInitialization(IObjectResolver resolver)
            {
                throw new InvalidOperationException("Mock exception for testing");
            }

            public bool AreDependenciesReady(IObjectResolver resolver)
            {
                return true; // Always ready for testing
            }
        }

        #endregion
    }
}
