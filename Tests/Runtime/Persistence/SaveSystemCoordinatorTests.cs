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
using ILogger = Serilog.ILogger;
using MToolKit.Tests.Runtime.Core;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence.Enums;
using Cysharp.Threading.Tasks;
using R3;
using MToolKit.Runtime.Slog;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.BoundSettings;

namespace MToolKit.Tests.Runtime.Persistence
{
    /// <summary>
    /// Test data constants and factory methods for consistent test values
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// </summary>
    public static class SaveSystemCoordinatorTestData
    {
        // Basic test values
        public const string TestDomain = "TestDomain";
        public const string TestKey = "TestKey";
        public const int TestAutoSaveIntervalSeconds = 10; // Increased from 1 to 10 seconds for test stability
        public const int TestAutoSavePaddingMilliseconds = 100;
        public const float ValidDeltaTime = 0.016f;
        public const float ZeroDeltaTime = 0f;
        public const float NegativeDeltaTime = -0.016f;
        public const float LargeDeltaTime = 1f;
        
        // Factory methods for consistent test object creation
        public static TestSaveDomainController CreateTestController(ESaveDomain domain = ESaveDomain.Player, bool shouldThrow = false, string exceptionMessage = "Test exception")
        {
            return new TestSaveDomainController(domain, shouldThrow, exceptionMessage);
        }
        
        public static List<ISaveDomainController> CreateTestControllers(int count, bool shouldThrow = false, string exceptionMessage = "Test exception")
        {
            var domains = new[] { ESaveDomain.Settings, ESaveDomain.Player, ESaveDomain.World };
            return Enumerable.Range(0, count)
                .Select(i => CreateTestController(domains[i % domains.Length], shouldThrow, exceptionMessage))
                .Cast<ISaveDomainController>()
                .ToList();
        }
        
        public static ES3SaveConfig CreateTestConfig(bool enableAutoSave = false, bool autoSaveOnSceneChange = true, int intervalSeconds = TestAutoSaveIntervalSeconds, int paddingMs = TestAutoSavePaddingMilliseconds)
        {
            var config = ScriptableObject.CreateInstance<ES3SaveConfig>();
            
            // Use reflection to set private fields since properties are read-only
            var autoSaveOnSceneChangeField = typeof(ES3SaveConfig).GetField("autoSaveOnSceneChange", BindingFlags.NonPublic | BindingFlags.Instance);
            var autoSaveIntervalSecondsField = typeof(ES3SaveConfig).GetField("autoSaveIntervalSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            var autoSavePaddingMillisecondsField = typeof(ES3SaveConfig).GetField("autoSavePaddingMilliseconds", BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Note: enableAutoSave is now controlled by IGameSettings, not the private field
            autoSaveOnSceneChangeField?.SetValue(config, autoSaveOnSceneChange);
            autoSaveIntervalSecondsField?.SetValue(config, (float)intervalSeconds);
            autoSavePaddingMillisecondsField?.SetValue(config, paddingMs);
            
            return config;
        }
        
        public static MockGameSettings CreateTestGameSettings(bool enableAutoSave = false)
        {
            return new MockGameSettings(enableAutoSave);
        }
        
        public static SaveDomainControllerRegistry CreateTestRegistry(List<ISaveDomainController> controllers = null)
        {
            var registry = new SaveDomainControllerRegistry();
            if (controllers != null)
            {
                foreach (var controller in controllers)
                {
                    registry.RegisterController(controller);
                }
            }
            return registry;
        }
        
        public static TestES3Service CreateTestES3Service(bool shouldThrow = false, string exceptionMessage = "Test exception")
        {
            return new TestES3Service(shouldThrow, exceptionMessage);
        }
    }

    /// <summary>
    /// Reflection utilities for accessing private fields and methods with performance optimization
    /// CRITICAL: Use unique class names to avoid namespace conflicts
    /// </summary>
    internal static class SaveSystemCoordinatorReflectionHelper
    {
        /// <summary>
        /// Cached FieldInfo for performance optimization
        /// </summary>
        private static readonly FieldInfo UseLocalSystemField = typeof(SaveSystemCoordinator)
            .GetField("useLocalSystem", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly FieldInfo AutoSaveCtsField = typeof(SaveSystemCoordinator)
            .GetField("autoSaveCts", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly FieldInfo GlobalSaveSystemField = typeof(SaveSystemCoordinator)
            .GetField("globalSaveSystem", BindingFlags.NonPublic | BindingFlags.Instance);
        
        private static readonly FieldInfo LocalSaveSystemField = typeof(SaveSystemCoordinator)
            .GetField("localSaveSystem", BindingFlags.NonPublic | BindingFlags.Instance);
        
        public static bool GetUseLocalSystem(SaveSystemCoordinator coordinator)
        {
            return (bool)(UseLocalSystemField?.GetValue(coordinator) ?? false);
        }
        
        public static CancellationTokenSource GetAutoSaveCts(SaveSystemCoordinator coordinator)
        {
            return AutoSaveCtsField?.GetValue(coordinator) as CancellationTokenSource;
        }
        
        public static ES3GameSaveSystem GetGlobalSaveSystem(SaveSystemCoordinator coordinator)
        {
            return GlobalSaveSystemField?.GetValue(coordinator) as ES3GameSaveSystem;
        }
        
        public static ES3GameSaveSystem GetLocalSaveSystem(SaveSystemCoordinator coordinator)
        {
            return LocalSaveSystemField?.GetValue(coordinator) as ES3GameSaveSystem;
        }
        
        public static void InvokeUpdateReactiveProperties(SaveSystemCoordinator coordinator)
        {
            var method = typeof(SaveSystemCoordinator).GetMethod("UpdateReactiveProperties", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(coordinator, null);
        }
        
        public static void InvokeStartAutoSave(SaveSystemCoordinator coordinator)
        {
            var method = typeof(SaveSystemCoordinator).GetMethod("StartAutoSave", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(coordinator, null);
        }
        
        public static void InvokeStopAutoSave(SaveSystemCoordinator coordinator)
        {
            var method = typeof(SaveSystemCoordinator).GetMethod("StopAutoSave", BindingFlags.NonPublic | BindingFlags.Instance);
            method?.Invoke(coordinator, null);
        }
        
        public static UniTask InvokeAutoSaveLoopAsync(SaveSystemCoordinator coordinator, CancellationToken ct)
        {
            var method = typeof(SaveSystemCoordinator).GetMethod("AutoSaveLoopAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            return (UniTask)method?.Invoke(coordinator, new object[] { ct });
        }
    }

    /// <summary>
    /// Test implementation of ISaveDomainController with call tracking and state verification
    /// CRITICAL: Supports granular exception control for precise testing scenarios
    /// </summary>
    public class TestSaveDomainController : ISaveDomainController
    {
        public ESaveDomain Domain { get; }
        public bool SaveCalled { get; private set; }
        public bool LoadCalled { get; private set; }
        public int SaveCallCount { get; private set; }
        public int LoadCallCount { get; private set; }
        public Exception LastThrownException { get; private set; }
        
        // Global exception control
        public bool ShouldThrowException { get; set; }
        public string ExceptionMessage { get; set; } = "Test exception";
        
        // Method-specific exception control for precise testing
        public bool ShouldThrowOnSave { get; set; }
        public bool ShouldThrowOnLoad { get; set; }

        public TestSaveDomainController(ESaveDomain domain, bool shouldThrow = false, string exceptionMessage = "Test exception")
        {
            Domain = domain;
            ShouldThrowException = shouldThrow;
            ExceptionMessage = exceptionMessage;
        }

        public async UniTask SaveAsync(CancellationToken ct = default)
        {
            SaveCallCount++;
            if (ShouldThrowException || ShouldThrowOnSave)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            SaveCalled = true;
            await UniTask.CompletedTask;
        }

        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            LoadCallCount++;
            if (ShouldThrowException || ShouldThrowOnLoad)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                throw exception;
            }
            LoadCalled = true;
            await UniTask.CompletedTask;
        }
        
        public void ResetCallCounts()
        {
            SaveCallCount = 0;
            LoadCallCount = 0;
            SaveCalled = false;
            LoadCalled = false;
            LastThrownException = null;
            
            // Reset exception behavior
            ShouldThrowException = false;
            ShouldThrowOnSave = false;
            ShouldThrowOnLoad = false;
            ExceptionMessage = "Test exception";
        }
        
        public bool VerifyCallCounts(int expectedSave = 0, int expectedLoad = 0)
        {
            return SaveCallCount == expectedSave && LoadCallCount == expectedLoad;
        }
    }

    /// <summary>
    /// Test implementation of IES3Service with call tracking and state verification
    /// CRITICAL: Supports granular exception control for precise testing scenarios
    /// </summary>
    public class TestES3Service : IES3Service
    {
        public ReactiveProperty<bool> IsSaving { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsLoading { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<string> LastSaveTime { get; } = new ReactiveProperty<string>("Never");
        public ReactiveProperty<string> LastLoadTime { get; } = new ReactiveProperty<string>("Never");
        public ReactiveProperty<int> SaveCounter { get; } = new ReactiveProperty<int>(0);
        
        public bool SaveCalled { get; private set; }
        public bool LoadCalled { get; private set; }
        public int SaveCallCount { get; private set; }
        public int LoadCallCount { get; private set; }
        public Exception LastThrownException { get; private set; }
        
        // Global exception control
        public bool ShouldThrowException { get; set; }
        public string ExceptionMessage { get; set; } = "Test exception";
        
        // Method-specific exception control for precise testing
        public bool ShouldThrowOnSave { get; set; }
        public bool ShouldThrowOnLoad { get; set; }

        public TestES3Service(bool shouldThrow = false, string exceptionMessage = "Test exception")
        {
            ShouldThrowException = shouldThrow;
            ExceptionMessage = exceptionMessage;
        }

        public async UniTask SaveAsync(CancellationToken ct = default)
        {
            SaveCallCount++;
            IsSaving.Value = true;
            if (ShouldThrowException || ShouldThrowOnSave)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                IsSaving.Value = false;
                throw exception;
            }
            SaveCalled = true;
            SaveCounter.Value++;
            LastSaveTime.Value = DateTime.Now.ToString("HH:mm:ss");
            IsSaving.Value = false;
            await UniTask.CompletedTask;
        }

        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            LoadCallCount++;
            IsLoading.Value = true;
            if (ShouldThrowException || ShouldThrowOnLoad)
            {
                var exception = new InvalidOperationException(ExceptionMessage);
                LastThrownException = exception;
                IsLoading.Value = false;
                throw exception;
            }
            LoadCalled = true;
            LastLoadTime.Value = DateTime.Now.ToString("HH:mm:ss");
            IsLoading.Value = false;
            await UniTask.CompletedTask;
        }

        public async UniTask SaveAsync(string key, object value, CancellationToken ct = default)
        {
            await SaveAsync(ct);
        }

        public async UniTask<T> LoadAsync<T>(string key, T defaultValue = default, CancellationToken ct = default)
        {
            await LoadAsync(ct);
            return defaultValue;
        }

        public bool KeyExists(string key)
        {
            return true; // Mock implementation
        }

        public void DeleteKey(string key)
        {
            // Mock implementation
        }

        public void DeleteFile()
        {
            // Mock implementation
        }

        public string GetSaveFormatVersion()
        {
            return "1.0.0"; // Mock implementation
        }

        public string GetSavedFormatVersion()
        {
            return "1.0.0"; // Mock implementation
        }

        public bool CreateBackup()
        {
            return true; // Mock implementation
        }

        public bool RestoreFromBackup()
        {
            return true; // Mock implementation
        }

        public string[] GetAvailableBackups()
        {
            return new string[] { "backup1", "backup2" }; // Mock implementation
        }
        
        public void ResetCallCounts()
        {
            SaveCallCount = 0;
            LoadCallCount = 0;
            SaveCalled = false;
            LoadCalled = false;
            LastThrownException = null;
            
            // Reset exception behavior
            ShouldThrowException = false;
            ShouldThrowOnSave = false;
            ShouldThrowOnLoad = false;
            ExceptionMessage = "Test exception";
        }
        
        public bool VerifyCallCounts(int expectedSave = 0, int expectedLoad = 0)
        {
            return SaveCallCount == expectedSave && LoadCallCount == expectedLoad;
        }
    }

    /// <summary>
    /// Test fixture for SaveSystemCoordinator with improved organization and comprehensive coverage
    /// </summary>
    [TestFixture]
    public class SaveSystemCoordinatorTests
    {
        private ContainerBuilder _containerBuilder;
        private IObjectResolver _resolver;
        private ILogger _mockLogger;
        private SaveSystemCoordinator _coordinator;
        private TestES3Service _testES3Service;
        private SaveDomainControllerRegistry _globalRegistry;
        private SaveDomainControllerRegistry _localRegistry;
        private ES3SaveConfig _saveConfig;
        private MockGameSettings _gameSettings;
        private PersistenceTestHelper _testHelper;

        [SetUp]
        public void Setup()
        {
            // Initialize test helper to manage SlogLoader state
            _testHelper = new PersistenceTestHelper();
            
            // Mock SlogLoader as initialized to prevent Bootstrapper timeout issues
            _testHelper.MockSlogLoaderInitialized(true);
            _testHelper.MockFlushSlogOnQuitFound(true);
            
            _containerBuilder = new ContainerBuilder();
            SetupTestContainer();
            _resolver = _containerBuilder.Build();
        }

        [TearDown]
        public void TearDown()
        {
            // Reset mock state to prevent cleanup exceptions
            _testES3Service?.ResetCallCounts();
            
            _coordinator?.Shutdown();
            _resolver?.Dispose();
            _testHelper?.Cleanup();
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
            
            // Create test dependencies
            _testES3Service = SaveSystemCoordinatorTestData.CreateTestES3Service();
            _globalRegistry = SaveSystemCoordinatorTestData.CreateTestRegistry();
            _localRegistry = SaveSystemCoordinatorTestData.CreateTestRegistry();
            _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
            _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings();
            
            // Initialize the save config with the game settings
            _saveConfig.Initialize(_gameSettings);
            
            // Register dependencies - avoid registration conflicts by not registering registries as Self
            // The SaveSystemCoordinator constructor will receive them directly
            _containerBuilder.RegisterInstance(_testES3Service).As<IES3Service>();
            _containerBuilder.RegisterInstance(_saveConfig).AsSelf();
            _containerBuilder.RegisterInstance(_gameSettings).As<IGameSettings>();
            _containerBuilder.RegisterInstance(new MockProfileManager()).As<IProfileManager>();
            _containerBuilder.Register<SaveSystemCoordinator>(Lifetime.Singleton)
                .WithParameter("globalRegistry", _globalRegistry)
                .WithParameter("localRegistry", _localRegistry);
        }

        /// <summary>
        /// Helper method to create SaveSystemCoordinator with test dependencies
        /// CRITICAL: Creates fresh container for each test to avoid registration conflicts
        /// </summary>
        private SaveSystemCoordinator CreateCoordinatorWithDependencies()
        {
            // Create a fresh container builder for this test
            var testContainerBuilder = new ContainerBuilder();
            
            // Register the common dependencies - avoid registration conflicts by not registering registries as Self
            testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            testContainerBuilder.RegisterInstance(_testES3Service).As<IES3Service>();
            testContainerBuilder.RegisterInstance(_saveConfig).AsSelf();
            testContainerBuilder.RegisterInstance(_gameSettings).As<IGameSettings>();
            testContainerBuilder.RegisterInstance(new MockProfileManager()).As<IProfileManager>();
            testContainerBuilder.Register<SaveSystemCoordinator>(Lifetime.Singleton)
                .WithParameter("globalRegistry", _globalRegistry)
                .WithParameter("localRegistry", _localRegistry);
            
            // Build and resolve
            var testResolver = testContainerBuilder.Build();
            var coordinator = testResolver.Resolve<SaveSystemCoordinator>();
            
            // Store the resolver for cleanup in TearDown
            _resolver?.Dispose();
            _resolver = testResolver;
            
            return coordinator;
        }

        #region Constructor Tests

        [TestFixture]
        public class ConstructorTests : SaveSystemCoordinatorTests
        {
            [Test]
            public void Constructor_WhenValidParametersProvided_ShouldInitializeReactiveProperties()
            {
                // Act
                _coordinator = CreateCoordinatorWithDependencies();

                // Assert
                Assert.That(_coordinator.IsSaving.Value, Is.False);
                Assert.That(_coordinator.IsLoading.Value, Is.False);
                Assert.That(_coordinator.LastSaveTime.Value, Is.EqualTo("Never"));
                Assert.That(_coordinator.LastLoadTime.Value, Is.EqualTo("Never"));
                Assert.That(_coordinator.SaveCounter.Value, Is.EqualTo(0));
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.False);
                Assert.That(_coordinator.IsAutoSaveExecuting.Value, Is.False);
            }

            [TestCase(true)]
            [TestCase(false)]
            public void Constructor_WhenAutoSaveConfigVaries_ShouldInitializeCorrectly(bool enableAutoSave)
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(enableAutoSave);
                _saveConfig.Initialize(_gameSettings);
                
                // Act
                _coordinator = CreateCoordinatorWithDependencies();

                // Assert
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.False);
                Assert.That(_coordinator.IsAutoSaveExecuting.Value, Is.False);
            }

            [Test]
            public void Constructor_WhenES3ServiceIsNull_ShouldThrowVContainerException()
            {
                // Arrange
                var testContainerBuilder = new ContainerBuilder();
                testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                // Don't register IES3Service - let VContainer fail during resolution
                testContainerBuilder.RegisterInstance(_saveConfig).AsSelf();
                testContainerBuilder.Register<SaveSystemCoordinator>(Lifetime.Singleton)
                    .WithParameter("globalRegistry", _globalRegistry)
                    .WithParameter("localRegistry", _localRegistry);

                // Act & Assert
                var testResolver = testContainerBuilder.Build();
                Assert.Throws<VContainerException>(() => testResolver.Resolve<SaveSystemCoordinator>());
            }

            [Test]
            public void Constructor_WhenGlobalRegistryIsNull_ShouldThrowVContainerException()
            {
                // Arrange
                var testContainerBuilder = new ContainerBuilder();
                testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                testContainerBuilder.RegisterInstance(_testES3Service).As<IES3Service>();
                testContainerBuilder.RegisterInstance(_saveConfig).AsSelf();
                testContainerBuilder.Register<SaveSystemCoordinator>(Lifetime.Singleton)
                    .WithParameter("globalRegistry", (SaveDomainControllerRegistry)null)
                    .WithParameter("localRegistry", _localRegistry);

                // Act & Assert
                var testResolver = testContainerBuilder.Build();
                Assert.Throws<VContainerException>(() => testResolver.Resolve<SaveSystemCoordinator>());
            }

            [Test]
            public void Constructor_WhenLocalRegistryIsNull_ShouldThrowVContainerException()
            {
                // Arrange
                var testContainerBuilder = new ContainerBuilder();
                testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                testContainerBuilder.RegisterInstance(_testES3Service).As<IES3Service>();
                testContainerBuilder.RegisterInstance(_saveConfig).AsSelf();
                testContainerBuilder.Register<SaveSystemCoordinator>(Lifetime.Singleton)
                    .WithParameter("globalRegistry", _globalRegistry)
                    .WithParameter("localRegistry", (SaveDomainControllerRegistry)null);

                // Act & Assert
                var testResolver = testContainerBuilder.Build();
                Assert.Throws<VContainerException>(() => testResolver.Resolve<SaveSystemCoordinator>());
            }

            [Test]
            public void Constructor_WhenSaveConfigIsNull_ShouldThrowVContainerException()
            {
                // Arrange
                var testContainerBuilder = new ContainerBuilder();
                testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                testContainerBuilder.RegisterInstance(_testES3Service).As<IES3Service>();
                // Don't register ES3SaveConfig - let VContainer fail during resolution
                testContainerBuilder.Register<SaveSystemCoordinator>(Lifetime.Singleton)
                    .WithParameter("globalRegistry", _globalRegistry)
                    .WithParameter("localRegistry", _localRegistry);

                // Act & Assert
                var testResolver = testContainerBuilder.Build();
                Assert.Throws<VContainerException>(() => testResolver.Resolve<SaveSystemCoordinator>());
            }
        }

        #endregion

        #region Start Tests

        [TestFixture]
        public class StartTests : SaveSystemCoordinatorTests
        {
            [Test]
            public void Start_WhenCalled_ShouldInitializeSaveSystems()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();

                // Act
                _coordinator.Start();

                // Assert
                var globalSystem = SaveSystemCoordinatorReflectionHelper.GetGlobalSaveSystem(_coordinator);
                var localSystem = SaveSystemCoordinatorReflectionHelper.GetLocalSaveSystem(_coordinator);
                
                Assert.That(globalSystem, Is.Not.Null);
                Assert.That(localSystem, Is.Not.Null);
            }

            [Test]
            public void Start_WhenCalledWithControllers_ShouldCreateSystemsWithControllers()
            {
                // Arrange
                var controllers = SaveSystemCoordinatorTestData.CreateTestControllers(3);
                foreach (var controller in controllers)
                {
                    _globalRegistry.RegisterController(controller);
                }
                _coordinator = CreateCoordinatorWithDependencies();

                // Act
                _coordinator.Start();

                // Assert
                var globalSystem = SaveSystemCoordinatorReflectionHelper.GetGlobalSaveSystem(_coordinator);
                Assert.That(globalSystem, Is.Not.Null);
            }

            [Test]
            public void Start_WhenCalledMultipleTimes_ShouldBeIdempotent()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();

                // Act
                _coordinator.Start();
                _coordinator.Start();
                _coordinator.Start();

                // Assert
                var globalSystem = SaveSystemCoordinatorReflectionHelper.GetGlobalSaveSystem(_coordinator);
                var localSystem = SaveSystemCoordinatorReflectionHelper.GetLocalSaveSystem(_coordinator);
                
                Assert.That(globalSystem, Is.Not.Null);
                Assert.That(localSystem, Is.Not.Null);
            }
        }

        #endregion

        #region Tick Tests

        [TestFixture]
        public class TickTests : SaveSystemCoordinatorTests
        {
            [TestCase(SaveSystemCoordinatorTestData.ValidDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.ZeroDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.NegativeDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.LargeDeltaTime)]
            public void Tick_WhenCalledWithVariousDeltaTimes_ShouldCompleteWithoutException(float deltaTime)
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act & Assert
                Assert.DoesNotThrow(() => _coordinator.Tick(deltaTime));
            }

            [TestCase(SaveSystemCoordinatorTestData.ValidDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.ZeroDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.NegativeDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.LargeDeltaTime)]
            public void LateTick_WhenCalledWithVariousDeltaTimes_ShouldCompleteWithoutException(float deltaTime)
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act & Assert
                Assert.DoesNotThrow(() => _coordinator.LateTick(deltaTime));
            }

            [TestCase(SaveSystemCoordinatorTestData.ValidDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.ZeroDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.NegativeDeltaTime)]
            [TestCase(SaveSystemCoordinatorTestData.LargeDeltaTime)]
            public void FixedTick_WhenCalledWithVariousDeltaTimes_ShouldCompleteWithoutException(float deltaTime)
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act & Assert
                Assert.DoesNotThrow(() => _coordinator.FixedTick(deltaTime));
            }
        }

        #endregion

        #region System Switching Tests

        [TestFixture]
        public class SystemSwitchingTests : SaveSystemCoordinatorTests
        {
            [Test]
            public void SwitchToLocalSystem_WhenCalledFirstTime_ShouldSwitchToLocalSystem()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                _coordinator.SwitchToLocalSystem();

                // Assert
                Assert.That(SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator), Is.True);
            }

            [Test]
            public void SwitchToLocalSystem_WhenCalledMultipleTimes_ShouldBeIdempotent()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                _coordinator.SwitchToLocalSystem();
                _coordinator.SwitchToLocalSystem();
                _coordinator.SwitchToLocalSystem();

                // Assert
                Assert.That(SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator), Is.True);
            }

            [TestCase(true)]
            [TestCase(false)]
            public void SwitchToLocalSystem_WhenAutoSaveConfigVaries_ShouldRespectConfig(bool enableAutoSave)
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(enableAutoSave);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                _coordinator.SwitchToLocalSystem();

                // Assert
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.EqualTo(enableAutoSave));
            }

            [Test]
            public void SwitchToGlobalSystem_WhenCalledFromLocalSystem_ShouldSwitchToGlobalSystem()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem();

                // Act
                _coordinator.SwitchToGlobalSystem();

                // Assert
                Assert.That(SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator), Is.False);
            }

            [Test]
            public void SwitchToGlobalSystem_WhenCalledMultipleTimes_ShouldBeIdempotent()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                _coordinator.SwitchToGlobalSystem();
                _coordinator.SwitchToGlobalSystem();
                _coordinator.SwitchToGlobalSystem();

                // Assert
                Assert.That(SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator), Is.False);
            }

            [Test]
            public void SwitchToGlobalSystem_WhenAutoSaveRunning_ShouldStopAutoSave()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem(); // Starts auto-save

                // Act
                _coordinator.SwitchToGlobalSystem();

                // Assert
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.False);
            }

            [Test]
            public void SystemSwitching_WhenMultipleCycles_ShouldMaintainCorrectState()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act - Test multiple cycles to catch state inconsistency bugs
                _coordinator.SwitchToLocalSystem();
                _coordinator.SwitchToGlobalSystem();
                _coordinator.SwitchToLocalSystem();
                _coordinator.SwitchToGlobalSystem();

                // Assert - State must be consistent after multiple cycles
                Assert.That(SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator), Is.False);
            }
        }

        #endregion

        #region Controller Registration Tests

        [TestFixture]
        public class ControllerRegistrationTests : SaveSystemCoordinatorTests
        {
            [Test]
            public void RegisterLocalController_WhenValidControllerProvided_ShouldRegisterController()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                var controller = SaveSystemCoordinatorTestData.CreateTestController(ESaveDomain.Player);

                // Act
                _coordinator.RegisterLocalController(controller);

                // Assert
                var registeredControllers = _localRegistry.GetControllers().ToList();
                Assert.That(registeredControllers, Contains.Item(controller));
            }

            [Test]
            public void RegisterLocalController_WhenCalledMultipleTimes_ShouldRegisterMultipleControllers()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                var controllers = SaveSystemCoordinatorTestData.CreateTestControllers(3);

                // Act
                foreach (var controller in controllers)
                {
                    _coordinator.RegisterLocalController(controller);
                }

                // Assert
                var registeredControllers = _localRegistry.GetControllers().ToList();
                Assert.That(registeredControllers.Count, Is.EqualTo(3));
                foreach (var controller in controllers)
                {
                    Assert.That(registeredControllers, Contains.Item(controller));
                }
            }

            [Test]
            public void RegisterLocalController_WhenControllerIsNull_ShouldHandleGracefully()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act & Assert
                Assert.DoesNotThrow(() => _coordinator.RegisterLocalController(null));
            }
        }

        #endregion

        #region Save/Load Tests

        [TestFixture]
        public class SaveLoadTests : SaveSystemCoordinatorTests
        {
            [Test]
            public async Task SaveAsync_WhenCalledOnGlobalSystem_ShouldCallGlobalSystemSave()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                await _coordinator.SaveAsync();

                // Assert
                Assert.That(_testES3Service.SaveCalled, Is.True);
            }

            [Test]
            public async Task SaveAsync_WhenCalledOnLocalSystem_ShouldCallLocalSystemSave()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem();

                // Act
                await _coordinator.SaveAsync();

                // Assert
                Assert.That(_testES3Service.SaveCalled, Is.True);
            }

            [Test]
            public async Task LoadAsync_WhenCalledOnGlobalSystem_ShouldCallGlobalSystemLoad()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                await _coordinator.LoadAsync();

                // Assert
                Assert.That(_testES3Service.LoadCalled, Is.True);
            }

            [Test]
            public async Task LoadAsync_WhenCalledOnLocalSystem_ShouldCallLocalSystemLoad()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem();

                // Act
                await _coordinator.LoadAsync();

                // Assert
                Assert.That(_testES3Service.LoadCalled, Is.True);
            }

            [Test]
            public async Task SaveAsync_WhenCancelled_ShouldThrowOperationCanceledException()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                var cts = new CancellationTokenSource();
                cts.Cancel();

                // Act & Assert
                try
                {
                    await _coordinator.SaveAsync(cts.Token);
                    Assert.Fail("Expected OperationCanceledException to be thrown");
                }
                catch (OperationCanceledException)
                {
                    // Expected exception
                }
            }

            [Test]
            public async Task LoadAsync_WhenCancelled_ShouldThrowOperationCanceledException()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                var cts = new CancellationTokenSource();
                cts.Cancel();

                // Act & Assert
                try
                {
                    await _coordinator.LoadAsync(cts.Token);
                    Assert.Fail("Expected OperationCanceledException to be thrown");
                }
                catch (OperationCanceledException)
                {
                    // Expected exception
                }
            }
        }

        #endregion

        #region Auto-Save Tests

        [TestFixture]
        public class AutoSaveTests : SaveSystemCoordinatorTests
        {
            [Test]
            public async Task TriggerAutoSaveAsync_WhenAutoSaveDisabled_ShouldReturnEarly()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(false);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                await _coordinator.TriggerAutoSaveAsync();

                // Assert
                Assert.That(_testES3Service.SaveCallCount, Is.EqualTo(0));
            }

            [Test]
            public async Task TriggerAutoSaveAsync_WhenAutoSaveEnabled_ShouldPerformSave()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem();

                // Act
                await _coordinator.TriggerAutoSaveAsync();

                // Assert
                Assert.That(_testES3Service.SaveCallCount, Is.GreaterThan(0));
            }

            [Test]
            public async Task HandleSceneChangeAsync_WhenAutoSaveOnSceneChangeDisabled_ShouldReturnEarly()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig(true, false);
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                await _coordinator.HandleSceneChangeAsync();

                // Assert
                Assert.That(_testES3Service.SaveCallCount, Is.EqualTo(0));
            }

            [Test]
            public async Task HandleSceneChangeAsync_WhenAutoSaveOnSceneChangeEnabled_ShouldPerformSave()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig(true, true);
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem();

                // Act
                await _coordinator.HandleSceneChangeAsync();

                // Assert
                Assert.That(_testES3Service.SaveCallCount, Is.GreaterThan(0));
            }
        }

        #endregion

        #region Exception Handling Tests

        [TestFixture]
        public class ExceptionHandlingTests : SaveSystemCoordinatorTests
        {
            [Test]
            public async Task SaveAsync_WhenES3ServiceThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testES3Service.ShouldThrowOnSave = true;
                _testES3Service.ExceptionMessage = "Save failed";
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Expect the error log messages that will be generated
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ES3 save operation failed: Save failed"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Save failed using global system: Save failed"));

                // Act & Assert
                try
                {
                    await _coordinator.SaveAsync();
                    Assert.Fail("Expected InvalidOperationException to be thrown");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.That(ex.Message, Is.EqualTo("Save failed"));
                }
            }

            [Test]
            public async Task LoadAsync_WhenES3ServiceThrowsException_ShouldPropagateException()
            {
                // Arrange
                _testES3Service.ShouldThrowOnLoad = true;
                _testES3Service.ExceptionMessage = "Load failed";
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Expect the error log messages that will be generated
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ES3 load operation failed: Load failed"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Load failed using global system: Load failed"));

                // Act & Assert
                try
                {
                    await _coordinator.LoadAsync();
                    Assert.Fail("Expected InvalidOperationException to be thrown");
                }
                catch (InvalidOperationException ex)
                {
                    Assert.That(ex.Message, Is.EqualTo("Load failed"));
                }
            }

            [Test]
            public async Task ExceptionIsolation_WhenOneMethodThrows_ShouldNotAffectOtherMethods()
            {
                // Arrange - Configure service to throw only on Save
                _testES3Service.ShouldThrowOnSave = true;
                _testES3Service.ExceptionMessage = "Save failed";
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Expect the error log messages that will be generated during save
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("ES3 save operation failed: Save failed"));
                LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Save failed using global system: Save failed"));

                try
                {
                    // Act & Assert - Save should throw exception
                    try
                    {
                        await _coordinator.SaveAsync();
                        Assert.Fail("Expected InvalidOperationException to be thrown");
                    }
                    catch (InvalidOperationException ex)
                    {
                        Assert.That(ex.Message, Is.EqualTo("Save failed"));
                    }
                    
                    // Load should work normally
                    await _coordinator.LoadAsync();
                    Assert.That(_testES3Service.LoadCalled, Is.True);
                }
                finally
                {
                    // Reset exception behavior to prevent exceptions during TearDown
                    _testES3Service.ShouldThrowOnSave = false;
                    _testES3Service.ExceptionMessage = "Test exception";
                }
            }
        }

        #endregion

        #region Shutdown Tests

        [TestFixture]
        public class ShutdownTests : SaveSystemCoordinatorTests
        {
            [Test]
            public void Shutdown_WhenCalled_ShouldStopAutoSave()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem(); // Starts auto-save

                // Act
                _coordinator.Shutdown();

                // Assert
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.False);
            }

            [Test]
            public void Shutdown_WhenCalledMultipleTimes_ShouldBeIdempotent()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                _coordinator.Shutdown();
                _coordinator.Shutdown();
                _coordinator.Shutdown();

                // Assert - Should complete without exception
                Assert.Pass("Multiple shutdown calls completed successfully");
            }

            [Test]
            public void Shutdown_WhenAutoSaveRunning_ShouldCancelAutoSaveToken()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem(); // Starts auto-save

                // Verify auto-save is running before shutdown
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.True, "Auto-save should be running after SwitchToLocalSystem");

                // Get the CancellationTokenSource before shutdown
                var autoSaveCts = SaveSystemCoordinatorReflectionHelper.GetAutoSaveCts(_coordinator);
                Assert.That(autoSaveCts, Is.Not.Null, "CancellationTokenSource should not be null before shutdown");
                Assert.That(autoSaveCts.IsCancellationRequested, Is.False, "CancellationTokenSource should not be cancelled before shutdown");

                // Act
                _coordinator.Shutdown();

                // Assert - Check that the CancellationTokenSource was cancelled before disposal
                Assert.That(autoSaveCts.IsCancellationRequested, Is.True, "CancellationTokenSource should be cancelled after shutdown");
            }
        }

        #endregion

        #region Reactive Properties Tests

        [TestFixture]
        public class ReactivePropertiesTests : SaveSystemCoordinatorTests
        {
            [Test]
            public async Task ReactiveProperties_WhenSaveOperationCompletes_ShouldUpdateCorrectly()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                await _coordinator.SaveAsync();

                // Assert
                Assert.That(_coordinator.SaveCounter.Value, Is.GreaterThan(0));
                Assert.That(_coordinator.LastSaveTime.Value, Is.Not.EqualTo("Never"));
            }

            [Test]
            public async Task ReactiveProperties_WhenLoadOperationCompletes_ShouldUpdateCorrectly()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                await _coordinator.LoadAsync();

                // Assert
                Assert.That(_coordinator.LastLoadTime.Value, Is.Not.EqualTo("Never"));
            }

            [Test]
            public void ReactiveProperties_WhenAutoSaveStarts_ShouldUpdateCorrectly()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();

                // Act
                _coordinator.SwitchToLocalSystem();

                // Assert
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.True);
            }

            [Test]
            public void ReactiveProperties_WhenAutoSaveStops_ShouldUpdateCorrectly()
            {
                // Arrange
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem(); // Starts auto-save

                // Act
                _coordinator.SwitchToGlobalSystem();

                // Assert
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.False);
            }
        }

        #endregion

        #region Lifecycle State Management Tests

        [TestFixture]
        public class LifecycleStateManagementTests : SaveSystemCoordinatorTests
        {
            [Test]
            public void Lifecycle_WhenMultipleStartShutdownCycles_ShouldMaintainCorrectState()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();

                // Act - Test multiple cycles to catch state inconsistency bugs
                _coordinator.Start();
                _coordinator.Shutdown();
                _coordinator.Start();
                _coordinator.Shutdown();

                // Assert - State must be consistent after multiple cycles
                Assert.That(_coordinator.IsAutoSaveRunning.Value, Is.False);
                Assert.That(_coordinator.IsAutoSaveExecuting.Value, Is.False);
            }

            [Test]
            public void Lifecycle_WhenStartCalledAfterShutdown_ShouldReinitializeCorrectly()
            {
                // Arrange
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.Shutdown();

                // Act
                _coordinator.Start();

                // Assert
                var globalSystem = SaveSystemCoordinatorReflectionHelper.GetGlobalSaveSystem(_coordinator);
                var localSystem = SaveSystemCoordinatorReflectionHelper.GetLocalSaveSystem(_coordinator);
                
                Assert.That(globalSystem, Is.Not.Null);
                Assert.That(localSystem, Is.Not.Null);
            }
        }

        #endregion
    }
}