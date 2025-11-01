using System;
using NUnit.Framework;
using UnityEngine;
using VContainer;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Tests.Runtime.Core.Abstractions
{
    /// <summary>
    /// Unit tests for AbstractRuntimePlugin class.
    /// Tests the base functionality of runtime plugins including tick methods,
    /// lifecycle management, and interface compliance.
    /// </summary>
    [TestFixture]
    public class AbstractRuntimePluginTests
    {
        #region Fields

        private TestRuntimePlugin _plugin;
        private GameObject _gameObject;

        #endregion

        #region Setup/Teardown

        /// <summary>
        /// Sets up test environment before each test.
        /// Creates a GameObject and attaches TestRuntimePlugin component.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestPlugin");
            _plugin = _gameObject.AddComponent<TestRuntimePlugin>();
        }

        /// <summary>
        /// Cleans up test environment after each test.
        /// Destroys the test GameObject to prevent memory leaks.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_gameObject);
            }
        }

        #endregion

        #region Tick Method Tests

        /// <summary>
        /// Tests that Tick method calls the override implementation.
        /// </summary>
        [Test]
        public void Tick_ShouldCallOverrideMethod()
        {
            // Arrange
            const float deltaTime = 0.016f;

            // Act
            _plugin.Tick(deltaTime);

            // Assert
            Assert.IsTrue(_plugin.TickCalled);
            Assert.AreEqual(deltaTime, _plugin.LastTickDeltaTime);
        }

        /// <summary>
        /// Tests that LateTick method calls the override implementation.
        /// </summary>
        [Test]
        public void LateTick_ShouldCallOverrideMethod()
        {
            // Arrange
            const float deltaTime = 0.016f;

            // Act
            _plugin.LateTick(deltaTime);

            // Assert
            Assert.IsTrue(_plugin.LateTickCalled);
            Assert.AreEqual(deltaTime, _plugin.LastLateTickDeltaTime);
        }

        /// <summary>
        /// Tests that FixedTick method calls the override implementation.
        /// </summary>
        [Test]
        public void FixedTick_ShouldCallOverrideMethod()
        {
            // Arrange
            const float deltaTime = 0.02f;

            // Act
            _plugin.FixedTick(deltaTime);

            // Assert
            Assert.IsTrue(_plugin.FixedTickCalled);
            Assert.AreEqual(deltaTime, _plugin.LastFixedTickDeltaTime);
        }

        #endregion

        #region Plugin Registration Tests

        /// <summary>
        /// Tests that Register method calls the override implementation.
        /// </summary>
        [Test]
        public void Register_ShouldCallOverrideMethod()
        {
            // Arrange
            var builder = new ContainerBuilder();

            // Act
            _plugin.Register(builder);

            // Assert
            Assert.IsTrue(_plugin.RegisterCalled);
        }

        #endregion

        #region Lifecycle Management Tests

        /// <summary>
        /// Tests that Start method calls the override implementation and sets started state.
        /// </summary>
        [Test]
        public void Start_ShouldCallOverrideMethod()
        {
            // Act
            _plugin.Start();

            // Assert
            Assert.IsTrue(_plugin.StartCalled);
            Assert.IsTrue(_plugin.IsStarted);
        }

        /// <summary>
        /// Tests that Start method maintains correct state when called multiple times.
        /// </summary>
        [Test]
        public void Start_WhenAlreadyStarted_ShouldMaintainCorrectState()
        {
            // Arrange
            _plugin.Start();
            Assert.IsTrue(_plugin.IsStarted);

            // Act
            _plugin.Start();

            // Assert
            Assert.IsTrue(_plugin.IsStarted, "Plugin should remain started after multiple Start calls");
            Assert.IsTrue(_plugin.StartCalled, "StartCalled should be true after Start is called");
        }

        /// <summary>
        /// Tests that Shutdown method calls the override implementation and sets shutdown state.
        /// </summary>
        [Test]
        public void Shutdown_ShouldCallOverrideMethod()
        {
            // Arrange
            _plugin.Start();

            // Act
            _plugin.Shutdown();

            // Assert
            Assert.IsTrue(_plugin.ShutdownCalled);
            Assert.IsTrue(_plugin.IsShutdown);
            Assert.IsFalse(_plugin.IsStarted);  
        }

        /// <summary>
        /// Tests that Shutdown method maintains correct state when called multiple times.
        /// </summary>
        [Test]
        public void Shutdown_WhenAlreadyShutdown_ShouldMaintainCorrectState()
        {
            // Arrange
            _plugin.Start();
            _plugin.Shutdown();
            Assert.IsTrue(_plugin.IsShutdown);
            Assert.IsFalse(_plugin.IsStarted);

            // Act
            _plugin.Shutdown();

            // Assert
            Assert.IsTrue(_plugin.IsShutdown, "Plugin should remain shutdown after multiple Shutdown calls");
            Assert.IsFalse(_plugin.IsStarted, "Plugin should remain not started after multiple Shutdown calls");
            Assert.IsTrue(_plugin.ShutdownCalled, "ShutdownCalled should be true after Shutdown is called");
        }

        /// <summary>
        /// Tests that Shutdown method handles gracefully when GameObject is destroyed.
        /// </summary>
        [Test]
        public void Shutdown_WhenObjectDestroyed_ShouldHandleGracefully()
        {
            // Arrange
            _plugin.Start();
            UnityEngine.Object.DestroyImmediate(_gameObject);

            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.Shutdown());
        }

        #endregion

        #region Interface Compliance Tests

        /// <summary>
        /// Tests that the plugin implements IGamePlugin interface.
        /// </summary>
        [Test]
        public void ImplementsIGamePlugin()
        {
            // Assert
            Assert.IsInstanceOf<IGamePlugin>(_plugin);
        }

        /// <summary>
        /// Tests that the plugin implements IRuntimeSystem interface.
        /// </summary>
        [Test]
        public void ImplementsIRuntimeSystem()
        {
            // Assert
            Assert.IsInstanceOf<IRuntimeSystem>(_plugin);
        }

        /// <summary>
        /// Tests that the plugin inherits from MonoBehaviour.
        /// </summary>
        [Test]
        public void InheritsFromMonoBehaviour()
        {
            // Assert
            Assert.IsInstanceOf<MonoBehaviour>(_plugin);
        }

        #endregion

        #region Edge Case Tests

        /// <summary>
        /// Tests that Tick method handles zero delta time without throwing exceptions.
        /// </summary>
        [Test]
        public void Tick_WithZeroDeltaTime_ShouldWork()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.Tick(0f));
            Assert.IsTrue(_plugin.TickCalled);
        }

        /// <summary>
        /// Tests that Tick method handles negative delta time without throwing exceptions.
        /// </summary>
        [Test]
        public void Tick_WithNegativeDeltaTime_ShouldWork()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.Tick(-0.1f));
            Assert.IsTrue(_plugin.TickCalled);
        }

        /// <summary>
        /// Tests that LateTick method handles zero delta time without throwing exceptions.
        /// </summary>
        [Test]
        public void LateTick_WithZeroDeltaTime_ShouldWork()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.LateTick(0f));
            Assert.IsTrue(_plugin.LateTickCalled);
        }

        /// <summary>
        /// Tests that FixedTick method handles zero delta time without throwing exceptions.
        /// </summary>
        [Test]
        public void FixedTick_WithZeroDeltaTime_ShouldWork()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _plugin.FixedTick(0f));
            Assert.IsTrue(_plugin.FixedTickCalled);
        }

        #endregion

        #region Multiple Call Tests

        /// <summary>
        /// Tests that multiple Tick calls work correctly and track call count.
        /// </summary>
        [Test]
        public void MultipleTickCalls_ShouldAllWork()
        {
            // Arrange
            const float deltaTime1 = 0.016f;
            const float deltaTime2 = 0.017f;
            const float deltaTime3 = 0.015f;

            // Act
            _plugin.Tick(deltaTime1);
            _plugin.Tick(deltaTime2);
            _plugin.Tick(deltaTime3);

            // Assert
            Assert.AreEqual(deltaTime3, _plugin.LastTickDeltaTime);
            Assert.AreEqual(3, _plugin.TickCallCount);
        }

        /// <summary>
        /// Tests that multiple LateTick calls work correctly and track call count.
        /// </summary>
        [Test]
        public void MultipleLateTickCalls_ShouldAllWork()
        {
            // Arrange
            const float deltaTime1 = 0.016f;
            const float deltaTime2 = 0.017f;

            // Act
            _plugin.LateTick(deltaTime1);
            _plugin.LateTick(deltaTime2);

            // Assert
            Assert.AreEqual(deltaTime2, _plugin.LastLateTickDeltaTime);
            Assert.AreEqual(2, _plugin.LateTickCallCount);
        }

        /// <summary>
        /// Tests that multiple FixedTick calls work correctly and track call count.
        /// </summary>
        [Test]
        public void MultipleFixedTickCalls_ShouldAllWork()
        {
            // Arrange
            const float deltaTime1 = 0.02f;
            const float deltaTime2 = 0.021f;

            // Act
            _plugin.FixedTick(deltaTime1);
            _plugin.FixedTick(deltaTime2);

            // Assert
            Assert.AreEqual(deltaTime2, _plugin.LastFixedTickDeltaTime);
            Assert.AreEqual(2, _plugin.FixedTickCallCount);
        }

        #endregion

        #region Lifecycle Cycle Tests

        /// <summary>
        /// Tests that Start-Shutdown cycle works correctly and maintains proper state.
        /// </summary>
        [Test]
        public void StartShutdownCycle_ShouldWorkCorrectly()
        {
            // Act
            _plugin.Start();
            _plugin.Shutdown();

            // Assert
            Assert.IsTrue(_plugin.StartCalled);
            Assert.IsTrue(_plugin.ShutdownCalled);
            Assert.IsFalse(_plugin.IsStarted);
            Assert.IsTrue(_plugin.IsShutdown);
        }

        /// <summary>
        /// Tests that Start-Shutdown-Start cycle works correctly and allows restart.
        /// </summary>
        [Test]
        public void StartShutdownStartCycle_ShouldWorkCorrectly()
        {
            // Act
            _plugin.Start();
            _plugin.Shutdown();
            _plugin.StartCalled = false;
            _plugin.Start();

            // Assert
            Assert.IsTrue(_plugin.StartCalled);
            Assert.IsTrue(_plugin.IsStarted);
        }

        #endregion
    }

    /// <summary>
    /// Test implementation of AbstractRuntimePlugin for unit testing.
    /// Tracks method calls and parameters to verify behavior in tests.
    /// </summary>
    public class TestRuntimePlugin : AbstractRuntimePlugin
    {
        #region Properties

        /// <summary>
        /// Indicates whether Tick method was called.
        /// </summary>
        public bool TickCalled { get; set; }

        /// <summary>
        /// Indicates whether LateTick method was called.
        /// </summary>
        public bool LateTickCalled { get; set; }

        /// <summary>
        /// Indicates whether FixedTick method was called.
        /// </summary>
        public bool FixedTickCalled { get; set; }

        /// <summary>
        /// Indicates whether Register method was called.
        /// </summary>
        public bool RegisterCalled { get; set; }

        /// <summary>
        /// Indicates whether Start method was called.
        /// </summary>
        public bool StartCalled { get; set; }

        /// <summary>
        /// Indicates whether Shutdown method was called.
        /// </summary>
        public bool ShutdownCalled { get; set; }

        /// <summary>
        /// The delta time from the last Tick call.
        /// </summary>
        public float LastTickDeltaTime { get; set; }

        /// <summary>
        /// The delta time from the last LateTick call.
        /// </summary>
        public float LastLateTickDeltaTime { get; set; }

        /// <summary>
        /// The delta time from the last FixedTick call.
        /// </summary>
        public float LastFixedTickDeltaTime { get; set; }

        /// <summary>
        /// The number of times Tick method was called.
        /// </summary>
        public int TickCallCount { get; set; }

        /// <summary>
        /// The number of times LateTick method was called.
        /// </summary>
        public int LateTickCallCount { get; set; }

        /// <summary>
        /// The number of times FixedTick method was called.
        /// </summary>
        public int FixedTickCallCount { get; set; }

        /// <summary>
        /// The number of times Shutdown method was called.
        /// </summary>
        public int ShutdownCallCount { get; set; }

        /// <summary>
        /// The number of times Start method was called.
        /// </summary>
        public int StartCallCount { get; set; }

        #endregion

        #region Override Methods

        /// <summary>
        /// Override Tick method to track calls and parameters.
        /// </summary>
        /// <param name="deltaTime">The time since the last tick.</param>
        public override void Tick(float deltaTime)
        {
            TickCalled = true;
            LastTickDeltaTime = deltaTime;
            TickCallCount++;
        }

        /// <summary>
        /// Override LateTick method to track calls and parameters.
        /// </summary>
        /// <param name="deltaTime">The time since the last tick.</param>
        public override void LateTick(float deltaTime)
        {
            LateTickCalled = true;
            LastLateTickDeltaTime = deltaTime;
            LateTickCallCount++;
        }

        /// <summary>
        /// Override FixedTick method to track calls and parameters.
        /// </summary>
        /// <param name="deltaTime">The time since the last tick.</param>
        public override void FixedTick(float deltaTime)
        {
            FixedTickCalled = true;
            LastFixedTickDeltaTime = deltaTime;
            FixedTickCallCount++;
        }

        /// <summary>
        /// Override Register method to track calls.
        /// </summary>
        /// <param name="builder">The container builder.</param>
        public override void Register(IContainerBuilder builder)
        {
            RegisterCalled = true;
        }

        /// <summary>
        /// Override Start method to track calls and call base implementation.
        /// </summary>
        public override void Start()
        {
            StartCalled = true;
            StartCallCount++;
            base.Start();
        }

        /// <summary>
        /// Override Shutdown method to track calls and call base implementation.
        /// </summary>
        public override void Shutdown()
        {
            ShutdownCalled = true;
            ShutdownCallCount++;
            base.Shutdown();
        }

        #endregion
    }
}
