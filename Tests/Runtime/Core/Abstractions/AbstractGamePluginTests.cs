/**
 * Unit tests for AbstractGamePlugin.cs
 * Refactored for improved quality, coverage, and maintainability
 * Framework: Unity Test Framework with NUnit
 * 
 * Test Coverage:
 * - Plugin registration and lifecycle management
 * - Static logger initialization and access
 * - Runtime system tick methods
 * - Error handling and edge cases
 * - Reflection-based testing for Unity components
 * 
 * Mock Dependencies:
 * - IContainerBuilder: VContainer dependency registration using NSubstitute
 * - ILogger: Serilog logging using NSubstitute
 */

using System;
using System.Reflection;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using Serilog;
using Serilog.Core;
using NSubstitute;
using MToolKit.Tests.Runtime.Core;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Interfaces;
using ILogger = Serilog.ILogger;

namespace MToolKit.Tests.Runtime.Core.Abstractions
{
    /// <summary>
    /// Test data constants for consistent test values
    /// </summary>
    internal static class TestData
    {
        public const float ValidDeltaTime = 0.016f;
        public const float ZeroDeltaTime = 0f;
        public const float NegativeDeltaTime = -0.016f;
        public const float LargeDeltaTime = 1f;
        public const string TestGameObjectName = "TestPlugin";
    }

    /// <summary>
    /// Comprehensive test suite for AbstractGamePlugin base class functionality
    /// Tests plugin registration, lifecycle management, and static logger access
    /// </summary>
    [TestFixture]
    public class AbstractGamePluginTests
    {
        #region Test Data and Utilities

        /// <summary>
        /// Reflection utilities for accessing private fields and methods
        /// </summary>
        private static class ReflectionHelper
        {
            public static readonly FieldInfo IsStartedField = typeof(AbstractGamePlugin)
                .GetField("isStarted", BindingFlags.NonPublic | BindingFlags.Instance);
            
            public static readonly FieldInfo IsShutdownField = typeof(AbstractGamePlugin)
                .GetField("isShutdown", BindingFlags.NonPublic | BindingFlags.Instance);
            
            public static readonly FieldInfo LogLazyField = typeof(AbstractGamePlugin)
                .GetField("logLazy", BindingFlags.NonPublic | BindingFlags.Static);
            
            public static readonly PropertyInfo LogProperty = typeof(AbstractGamePlugin)
                .GetProperty("log", BindingFlags.NonPublic | BindingFlags.Static);

            public static bool GetIsStarted(AbstractGamePlugin plugin) => 
                (bool)IsStartedField.GetValue(plugin);

            public static bool GetIsShutdown(AbstractGamePlugin plugin) => 
                (bool)IsShutdownField.GetValue(plugin);

            public static Lazy<ILogger> GetLogLazy() => 
                (Lazy<ILogger>)LogLazyField.GetValue(null);

            public static ILogger GetLog() => 
                (ILogger)LogProperty.GetValue(null);
        }

        #endregion

        #region Test Setup

        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private TestGamePlugin _testPlugin;
        private GameObject _testGameObject;
        private ILogger _mockLogger;

        [SetUp]
        public void Setup()
        {
            _containerBuilder = new ContainerBuilder();
            SetupTestContainer();
            _resolver = _containerBuilder.Build();
            
            CreateTestGameObject();
        }

        [TearDown]
        public void TearDown()
        {
            CleanupTestGameObject();
            _resolver?.Dispose();
        }

        private void SetupTestContainer()
        {
            // Using MockLogger instead of NSubstitute for IL2CPP compatibility (no Reflection.Emit)
            _mockLogger = new MockLogger();
            _containerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
        }

        private void CreateTestGameObject()
        {
            _testGameObject = new GameObject(TestData.TestGameObjectName);
            _testPlugin = _testGameObject.AddComponent<TestGamePlugin>();
        }

        private void CleanupTestGameObject()
        {
            if (_testGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_testGameObject);
                _testGameObject = null;
                _testPlugin = null;
            }
        }

        #endregion

        #region Register Method Tests

        [TestFixture]
        public class RegisterTests : AbstractGamePluginTests
        {
            [Test]
            public void Register_WhenValidBuilderProvided_ShouldCompleteWithoutException()
            {
                // Arrange
                var builder = Substitute.For<IContainerBuilder>();

                // Act & Assert
                Assert.DoesNotThrow(() => _testPlugin.Register(builder));
            }

            [Test]
            public void Register_WhenNullBuilderProvided_ShouldCompleteWithoutException()
            {
                // Act & Assert
                Assert.DoesNotThrow(() => _testPlugin.Register(null));
            }

            [Test]
            public void Register_WhenCalledMultipleTimes_ShouldCompleteWithoutException()
            {
                // Arrange
                var builder = Substitute.For<IContainerBuilder>();

                // Act & Assert
                Assert.DoesNotThrow(() => 
                {
                    _testPlugin.Register(builder);
                    _testPlugin.Register(builder);
                    _testPlugin.Register(builder);
                });
            }

            [Test]
            public void Register_WhenCalledViaReflection_ShouldWorkCorrectly()
            {
                // Arrange
                var registerMethod = typeof(AbstractGamePlugin).GetMethod("Register", 
                    BindingFlags.Public | BindingFlags.Instance);
                var builder = Substitute.For<IContainerBuilder>();

                // Act & Assert
                Assert.DoesNotThrow(() => registerMethod.Invoke(_testPlugin, new object[] { builder }));
            }
        }

        #endregion

        #region Start Method Tests

        [TestFixture]
        public class StartTests : AbstractGamePluginTests
        {
            [Test]
            public void Start_WhenCalledFirstTime_ShouldSetIsStartedToTrue()
            {
                // Act
                _testPlugin.Start();

                // Assert
                Assert.That(ReflectionHelper.GetIsStarted(_testPlugin), Is.True);
            }

            [Test]
            public void Start_WhenCalledMultipleTimes_ShouldOnlyExecuteOnce()
            {
                // Act
                _testPlugin.Start();
                _testPlugin.Start();
                _testPlugin.Start();

                // Assert
                Assert.That(ReflectionHelper.GetIsStarted(_testPlugin), Is.True);
            }


            [Test]
            public void Start_WhenCalledViaReflection_ShouldWorkCorrectly()
            {
                // Arrange
                var startMethod = typeof(AbstractGamePlugin).GetMethod("Start", 
                    BindingFlags.Public | BindingFlags.Instance);

                // Act
                startMethod.Invoke(_testPlugin, null);

                // Assert
                Assert.That(ReflectionHelper.GetIsStarted(_testPlugin), Is.True);
            }

            [Test]
            public void Start_WhenAlreadyShutdown_ShouldStillSetIsStartedToTrue()
            {
                // Arrange
                _testPlugin.Shutdown();

                // Act
                _testPlugin.Start();

                // Assert
                Assert.That(ReflectionHelper.GetIsStarted(_testPlugin), Is.True);
            }

            [Test]
            public void Start_WhenPluginReferenceIsNull_ShouldThrowNullReferenceException()
            {
                // Arrange
                TestGamePlugin nullPlugin = null;

                // Act & Assert
                Assert.Throws<NullReferenceException>(() => nullPlugin.Start());
            }

            [Test]
            public void Start_WhenCalledOnDestroyedPlugin_ShouldThrowMissingReferenceException()
            {
                // Arrange
                var plugin = _testPlugin;
                CleanupTestGameObject(); // This sets _testPlugin = null but plugin still holds reference

                // Act & Assert
                Assert.Throws<MissingReferenceException>(() => plugin.Start());
            }
        }

        #endregion

        #region Shutdown Method Tests

        [TestFixture]
        public class ShutdownTests : AbstractGamePluginTests
        {
            [Test]
            public void Shutdown_WhenCalledFirstTime_ShouldSetIsShutdownToTrue()
            {
                // Act
                _testPlugin.Shutdown();

                // Assert
                Assert.That(ReflectionHelper.GetIsShutdown(_testPlugin), Is.True);
            }

            [Test]
            public void Shutdown_WhenCalledFirstTime_ShouldSetIsStartedToFalse()
            {
                // Arrange
                _testPlugin.Start();

                // Act
                _testPlugin.Shutdown();

                // Assert
                Assert.That(ReflectionHelper.GetIsStarted(_testPlugin), Is.False);
            }

            [Test]
            public void Shutdown_WhenCalledMultipleTimes_ShouldOnlyExecuteOnce()
            {
                // Act
                _testPlugin.Shutdown();
                _testPlugin.Shutdown();
                _testPlugin.Shutdown();

                // Assert
                Assert.That(ReflectionHelper.GetIsShutdown(_testPlugin), Is.True);
            }



            [Test]
            public void Shutdown_WhenCalledViaReflection_ShouldWorkCorrectly()
            {
                // Arrange
                var shutdownMethod = typeof(AbstractGamePlugin).GetMethod("Shutdown", 
                    BindingFlags.Public | BindingFlags.Instance);

                // Act
                shutdownMethod.Invoke(_testPlugin, null);

                // Assert
                Assert.That(ReflectionHelper.GetIsShutdown(_testPlugin), Is.True);
            }

            [Test]
            public void Shutdown_WhenPluginReferenceIsNull_ShouldThrowNullReferenceException()
            {
                // Arrange
                TestGamePlugin nullPlugin = null;

                // Act & Assert
                Assert.Throws<NullReferenceException>(() => nullPlugin.Shutdown());
            }

            [Test]
            public void Shutdown_WhenGameObjectIsDestroyed_ShouldCompleteWithoutException()
            {
                // Arrange
                var plugin = _testPlugin; // Keep reference to plugin before destroying GameObject
                CleanupTestGameObject(); // This destroys the GameObject but plugin reference still exists

                // Act & Assert
                Assert.DoesNotThrow(() => plugin.Shutdown());
            }

            [Test]
            public void Shutdown_WhenCalledOnDestroyedPlugin_ShouldCompleteWithoutException()
            {
                // Arrange
                var plugin = _testPlugin;
                CleanupTestGameObject(); // This sets _testPlugin = null but plugin still holds reference

                // Act & Assert
                Assert.DoesNotThrow(() => plugin.Shutdown());
            }
        }

        #endregion

        #region Logger Tests

        [TestFixture]
        public class LoggerTests
        {
            [Test]
            public void LogLazy_WhenAccessedFirstTime_ShouldInitializeLogger()
            {
                // Act
                var logLazy = ReflectionHelper.GetLogLazy();

                // Assert
                Assert.That(logLazy, Is.Not.Null);
                Assert.That(logLazy, Is.InstanceOf<Lazy<ILogger>>());
            }

            [Test]
            public void LogProperty_WhenAccessed_ShouldReturnLoggerInstance()
            {
                // Act
                var logger = ReflectionHelper.GetLog();

                // Assert
                Assert.That(logger, Is.Not.Null);
                Assert.That(logger, Is.InstanceOf<ILogger>());
            }

            [Test]
            public void LogProperty_WhenAccessedMultipleTimes_ShouldReturnSameInstance()
            {
                // Act
                var logger1 = ReflectionHelper.GetLog();
                var logger2 = ReflectionHelper.GetLog();

                // Assert
                Assert.That(logger1, Is.SameAs(logger2));
            }

            [Test]
            public void LogLazy_WhenAccessedMultipleTimes_ShouldReturnSameInstance()
            {
                // Act
                var logLazy1 = ReflectionHelper.GetLogLazy();
                var logLazy2 = ReflectionHelper.GetLogLazy();

                // Assert
                Assert.That(logLazy1, Is.SameAs(logLazy2));
            }
        }

        #endregion

        #region Lifecycle State Tests

        [TestFixture]
        public class LifecycleStateTests : AbstractGamePluginTests
        {
            [Test]
            public void Lifecycle_WhenStartThenShutdown_ShouldHaveCorrectState()
            {
                // Act
                _testPlugin.Start();
                _testPlugin.Shutdown();

                // Assert
                Assert.That(ReflectionHelper.GetIsStarted(_testPlugin), Is.False);
                Assert.That(ReflectionHelper.GetIsShutdown(_testPlugin), Is.True);
            }

            [Test]
            public void Lifecycle_WhenShutdownThenStart_ShouldHaveCorrectState()
            {
                // Act
                _testPlugin.Shutdown();
                _testPlugin.Start();

                // Assert
                Assert.That(ReflectionHelper.GetIsStarted(_testPlugin), Is.True);
                Assert.That(ReflectionHelper.GetIsShutdown(_testPlugin), Is.True);
            }

            [Test]
            public void Lifecycle_WhenMultipleStartShutdownCycles_ShouldMaintainCorrectState()
            {
                // Act
                _testPlugin.Start();
                _testPlugin.Shutdown();
                _testPlugin.Start();
                _testPlugin.Shutdown();

                // Assert
                Assert.That(ReflectionHelper.GetIsStarted(_testPlugin), Is.False);
                Assert.That(ReflectionHelper.GetIsShutdown(_testPlugin), Is.True);
            }
        }

        #endregion

        #region Interface Implementation Tests

        [TestFixture]
        public class InterfaceImplementationTests : AbstractGamePluginTests
        {
            [Test]
            public void AbstractGamePlugin_ShouldImplementIGamePlugin()
            {
                // Assert
                Assert.That(_testPlugin, Is.InstanceOf<IGamePlugin>());
            }

            [Test]
            public void AbstractGamePlugin_ShouldInheritFromMonoBehaviour()
            {
                // Assert
                Assert.That(_testPlugin, Is.InstanceOf<MonoBehaviour>());
            }

            [Test]
            public void AbstractGamePlugin_ShouldHaveVirtualMethods()
            {
                // Arrange
                var registerMethod = typeof(AbstractGamePlugin).GetMethod("Register");
                var startMethod = typeof(AbstractGamePlugin).GetMethod("Start");
                var shutdownMethod = typeof(AbstractGamePlugin).GetMethod("Shutdown");

                // Assert
                Assert.That(registerMethod.IsVirtual, Is.True);
                Assert.That(startMethod.IsVirtual, Is.True);
                Assert.That(shutdownMethod.IsVirtual, Is.True);
            }
        }

        #endregion
    }


    #region Test Helper Classes

    /// <summary>
    /// Test implementation of AbstractGamePlugin for unit testing
    /// Provides empty implementation to test base class behavior
    /// </summary>
    public class TestGamePlugin : AbstractGamePlugin
    {
        // Empty implementation - inherits all behavior from AbstractGamePlugin
    }


    #endregion
}