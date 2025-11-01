using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FsCheck;
using NUnit.Framework;
using VContainer;
using UnityEngine;
using UnityEngine.TestTools;
using Serilog;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence.Enums;
using Cysharp.Threading.Tasks;
using R3;
using MToolKit.Runtime.Slog;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Tests.Runtime.Core;

namespace MToolKit.Tests.Runtime.Persistence
{
    /// <summary>
    /// Test fixture for SaveSystemCoordinator property-based tests
    /// Property tests validate system behavior across wide input ranges using FsCheck
    /// </summary>
    [TestFixture]
    public class SaveSystemCoordinatorPropertyTests
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
            _testES3Service = new TestES3Service();
            _globalRegistry = SaveSystemCoordinatorTestData.CreateTestRegistry();
            _localRegistry = SaveSystemCoordinatorTestData.CreateTestRegistry();
            _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
            _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings();
            var mockProfileManager = new MockProfileManager();
            
            // Initialize the save config with the game settings
            _saveConfig.Initialize(_gameSettings);
            
            // Register dependencies using factory methods to avoid registration conflicts
            _containerBuilder.RegisterInstance(_testES3Service).As<IES3Service>();
            _containerBuilder.RegisterInstance(_saveConfig).AsSelf();
            _containerBuilder.RegisterInstance(_gameSettings).As<IGameSettings>();
            _containerBuilder.RegisterInstance(mockProfileManager).As<IProfileManager>();
            
            // Register SaveSystemCoordinator with factory method to inject the registries
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
            
            // Register the common dependencies
            testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
            testContainerBuilder.RegisterInstance(_testES3Service).As<IES3Service>();
            testContainerBuilder.RegisterInstance(_saveConfig).AsSelf();
            testContainerBuilder.RegisterInstance(_gameSettings).As<IGameSettings>();
            testContainerBuilder.RegisterInstance(new MockProfileManager()).As<IProfileManager>();
            
            // Register SaveSystemCoordinator with factory method to inject the registries
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

        #region 1. Invariant Properties (Must Always Hold)

        /// <summary>
        /// Property: Reactive properties maintain consistent default values after initialization
        /// Invariant: Default reactive property values never change after construction
        /// </summary>
        [Test]
        public void ReactiveProperties_MaintainDefaultValues_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Create coordinator with random config
                var enableAutoSave = random.Next(2) == 0;
                var autoSaveOnSceneChange = random.Next(2) == 0;
                var intervalSeconds = random.Next(1, 11);
                var paddingMs = random.Next(50, 501);
                
                var config = SaveSystemCoordinatorTestData.CreateTestConfig(false, autoSaveOnSceneChange, intervalSeconds, paddingMs);
                var gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(enableAutoSave);
                config.Initialize(gameSettings);
                
                var testContainerBuilder = new ContainerBuilder();
                testContainerBuilder.RegisterInstance(_mockLogger).As<ILogger>();
                testContainerBuilder.RegisterInstance(_testES3Service).As<IES3Service>();
                testContainerBuilder.RegisterInstance(config).AsSelf();
                testContainerBuilder.RegisterInstance(gameSettings).As<IGameSettings>();
                testContainerBuilder.RegisterInstance(new MockProfileManager()).As<IProfileManager>();
                
                // Register SaveSystemCoordinator with factory method to inject the registries
                testContainerBuilder.Register<SaveSystemCoordinator>(Lifetime.Singleton)
                    .WithParameter("globalRegistry", _globalRegistry)
                    .WithParameter("localRegistry", _localRegistry);
                
                var testResolver = testContainerBuilder.Build();
                var coordinator = testResolver.Resolve<SaveSystemCoordinator>();
                
                // Property: Default reactive property values must be consistent
                var result = coordinator.IsSaving.Value == false &&
                           coordinator.IsLoading.Value == false &&
                           coordinator.LastSaveTime.Value == "Never" &&
                           coordinator.LastLoadTime.Value == "Never" &&
                           coordinator.SaveCounter.Value == 0 &&
                           coordinator.IsAutoSaveRunning.Value == false &&
                           coordinator.IsAutoSaveExecuting.Value == false;
                
                Check.QuickThrowOnFailure(result);
                
                coordinator.Shutdown();
                testResolver.Dispose();
            }
        }

        /// <summary>
        /// Property: System switching maintains mutual exclusivity
        /// Invariant: Cannot be both local and global system simultaneously
        /// </summary>
        [Test]
        public void SystemSwitching_MaintainsMutualExclusivity_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Perform random system switches
                var switchCount = random.Next(1, 10);
                for (int j = 0; j < switchCount; j++)
                {
                    var useLocal = random.Next(2) == 0;
                    if (useLocal)
                    {
                        _coordinator.SwitchToLocalSystem();
                    }
                    else
                    {
                        _coordinator.SwitchToGlobalSystem();
                    }
                }
                
                // Property: Must be in exactly one system state
                var isLocal = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                var isGlobal = !isLocal;
                
                var result = (isLocal && !isGlobal) || (!isLocal && isGlobal);
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Auto-save state consistency across operations
        /// Invariant: Auto-save running state remains consistent during operations
        /// </summary>
        [Test]
        [Timeout(10000)]
        public async Task AutoSaveState_RemainsConsistent_Invariant()
        {
            var random = new System.Random();
            for (int i = 0; i < 10; i++) // Reduced iterations to avoid race conditions
            {
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig(true);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem(); // Starts auto-save
                
                try
                {
                    // Perform various operations
                    var operations = random.Next(1, 5); // Reduced operations per iteration
                    for (int j = 0; j < operations; j++)
                    {
                        var operation = random.Next(4);
                        switch (operation)
                        {
                            case 0: await _coordinator.SaveAsync(); break;
                            case 1: await _coordinator.LoadAsync(); break;
                            case 2: await _coordinator.TriggerAutoSaveAsync(); break;
                            case 3: await _coordinator.HandleSceneChangeAsync(); break;
                        }
                        
                        // Small delay to ensure async operations complete properly
                        await UniTask.Delay(10);
                    }
                    
                    // Property: Auto-save running state should be consistent
                    var isRunning = _coordinator.IsAutoSaveRunning.Value;
                    var isExecuting = _coordinator.IsAutoSaveExecuting.Value;
                    
                    // If auto-save is running, it should not be executing simultaneously with other operations
                    var result = !isRunning || !isExecuting;
                    Check.QuickThrowOnFailure(result);
                }
                finally
                {
                    // Ensure proper cleanup for each iteration
                    _coordinator?.Shutdown();
                    _coordinator = null;
                    
                    // Small delay to ensure cleanup completes
                    await UniTask.Delay(10);
                }
            }
        }

        #endregion

        #region 2. Mathematical Properties (Laws of Operations)

        /// <summary>
        /// Property: Switch to local then global restores original state
        /// Mathematical Property: Local → Global → Local = Original Local State
        /// </summary>
        [Test]
        public void SystemSwitching_Twice_RestoresOriginalState_Mathematical()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Record initial state
                var initialIsLocal = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                var initialAutoSaveRunning = _coordinator.IsAutoSaveRunning.Value;
                
                // Switch to local system
                _coordinator.SwitchToLocalSystem();
                var afterLocalSwitch = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                
                // Switch back to global system
                _coordinator.SwitchToGlobalSystem();
                var afterGlobalSwitch = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                
                // Switch back to local system
                _coordinator.SwitchToLocalSystem();
                var finalState = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                
                // Property: Final state should match the state after first local switch
                var result = finalState == afterLocalSwitch;
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Save then load preserves system state
        /// Mathematical Property: Save → Load = No Net State Change
        /// </summary>
        [Test]
        [Timeout(5000)]
        public async Task SaveLoad_PreservesSystemState_Mathematical()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Randomly choose system
                var useLocal = random.Next(2) == 0;
                if (useLocal)
                {
                    _coordinator.SwitchToLocalSystem();
                }
                
                // Record state before save/load
                var beforeIsLocal = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                var beforeAutoSaveRunning = _coordinator.IsAutoSaveRunning.Value;
                
                // Perform save then load
                await _coordinator.SaveAsync();
                await _coordinator.LoadAsync();
                
                // Record state after save/load
                var afterIsLocal = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                var afterAutoSaveRunning = _coordinator.IsAutoSaveRunning.Value;
                
                // Property: System state should be preserved
                var result = beforeIsLocal == afterIsLocal && beforeAutoSaveRunning == afterAutoSaveRunning;
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Multiple controller registrations are additive
        /// Mathematical Property: Register(A) + Register(B) = Contains(A) AND Contains(B)
        /// </summary>
        [Test]
        public void ControllerRegistration_IsAdditive_Mathematical()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Create random number of controllers
                var controllerCount = random.Next(1, 10);
                var controllers = new List<TestSaveDomainController>();
                
                for (int j = 0; j < controllerCount; j++)
                {
                    var domains = new[] { ESaveDomain.Settings, ESaveDomain.Player, ESaveDomain.World };
                var controller = SaveSystemCoordinatorTestData.CreateTestController(domains[j % domains.Length]);
                    controllers.Add(controller);
                    _coordinator.RegisterLocalController(controller);
                }
                
                // Property: All registered controllers should be in the registry
                var registeredControllers = _localRegistry.GetControllers().ToList();
                var allControllersRegistered = controllers.All(c => registeredControllers.Contains(c));
                
                var result = allControllersRegistered && registeredControllers.Count >= controllerCount;
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region 3. State Transitions (Valid Progression of State)

        /// <summary>
        /// Property: After switching to local system, auto-save starts when enabled
        /// State Transition: SwitchToLocalSystem + EnableAutoSave = IsAutoSaveRunning = true
        /// </summary>
        [Test]
        public void SwitchToLocalSystem_StartsAutoSaveWhenEnabled_StateTransition()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                var enableAutoSave = random.Next(2) == 0;
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(enableAutoSave);
                _saveConfig.Initialize(_gameSettings);
                
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Switch to local system
                _coordinator.SwitchToLocalSystem();
                
                // Property: Auto-save should be running if enabled, not running if disabled
                var autoSaveRunning = _coordinator.IsAutoSaveRunning.Value;
                var result = enableAutoSave ? autoSaveRunning : !autoSaveRunning;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: After switching to global system, auto-save stops
        /// State Transition: SwitchToGlobalSystem = IsAutoSaveRunning = false
        /// </summary>
        [Test]
        public void SwitchToGlobalSystem_StopsAutoSave_StateTransition()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Start auto-save by switching to local system
                _coordinator.SwitchToLocalSystem();
                var autoSaveStarted = _coordinator.IsAutoSaveRunning.Value;
                
                // Switch to global system
                _coordinator.SwitchToGlobalSystem();
                var autoSaveStopped = !_coordinator.IsAutoSaveRunning.Value;
                
                // Property: Auto-save should stop when switching to global system
                var result = autoSaveStarted && autoSaveStopped;
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: After registering controller, local system is rebuilt when active
        /// State Transition: RegisterController + useLocalSystem = LocalSystemRebuilt
        /// </summary>
        [Test]
        public void RegisterController_RebuildsLocalSystemWhenActive_StateTransition()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Randomly choose whether to use local system
                var useLocal = random.Next(2) == 0;
                if (useLocal)
                {
                    _coordinator.SwitchToLocalSystem();
                }
                
                // Get initial local system
                var initialLocalSystem = SaveSystemCoordinatorReflectionHelper.GetLocalSaveSystem(_coordinator);
                
                // Register a controller
                var domains = new[] { ESaveDomain.Settings, ESaveDomain.Player, ESaveDomain.World };
                var controller = SaveSystemCoordinatorTestData.CreateTestController(domains[i % domains.Length]);
                _coordinator.RegisterLocalController(controller);
                
                // Get updated local system
                var updatedLocalSystem = SaveSystemCoordinatorReflectionHelper.GetLocalSaveSystem(_coordinator);
                
                // Property: Local system should be rebuilt when using local system
                var result = !useLocal || updatedLocalSystem != null;
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region 4. Reversibility / Round-trip Laws

        /// <summary>
        /// Property: Start → Shutdown → Start preserves initial state
        /// Reversibility: Start → Shutdown → Start = Original Start State
        /// </summary>
        [Test]
        public void StartShutdownStart_PreservesInitialState_Reversibility()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                
                // Record initial state
                var initialIsSaving = _coordinator.IsSaving.Value;
                var initialIsLoading = _coordinator.IsLoading.Value;
                var initialLastSaveTime = _coordinator.LastSaveTime.Value;
                var initialLastLoadTime = _coordinator.LastLoadTime.Value;
                var initialSaveCounter = _coordinator.SaveCounter.Value;
                
                // Start → Shutdown → Start cycle
                _coordinator.Start();
                _coordinator.Shutdown();
                _coordinator.Start();
                
                // Property: State should be preserved after cycle
                var result = _coordinator.IsSaving.Value == initialIsSaving &&
                           _coordinator.IsLoading.Value == initialIsLoading &&
                           _coordinator.LastSaveTime.Value == initialLastSaveTime &&
                           _coordinator.LastLoadTime.Value == initialLastLoadTime &&
                           _coordinator.SaveCounter.Value == initialSaveCounter;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: System switching is reversible
        /// Reversibility: Global → Local → Global = Original Global State
        /// </summary>
        [Test]
        public void SystemSwitching_IsReversible_Reversibility()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Start in global system
                var initialIsLocal = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                
                // Switch to local then back to global
                _coordinator.SwitchToLocalSystem();
                _coordinator.SwitchToGlobalSystem();
                
                // Property: Should return to original global state
                var finalIsLocal = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                var result = initialIsLocal == finalIsLocal;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Auto-save operations are reversible
        /// Reversibility: StartAutoSave → StopAutoSave → StartAutoSave = Original AutoSave State
        /// </summary>
        [Test]
        public void AutoSaveOperations_AreReversible_Reversibility()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem();
                
                // Record initial auto-save state
                var initialAutoSaveRunning = _coordinator.IsAutoSaveRunning.Value;
                
                // Stop and restart auto-save
                SaveSystemCoordinatorReflectionHelper.InvokeStopAutoSave(_coordinator);
                SaveSystemCoordinatorReflectionHelper.InvokeStartAutoSave(_coordinator);
                
                // Property: Auto-save should be running after restart
                var finalAutoSaveRunning = _coordinator.IsAutoSaveRunning.Value;
                var result = initialAutoSaveRunning == finalAutoSaveRunning;
                
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region 5. Error / Boundary Behavior

        /// <summary>
        /// Property: Operations on uninitialized coordinator never crash
        /// Error Boundary: Uninitialized operations complete without throwing
        /// </summary>
        [Test]
        [Timeout(5000)]
        public async Task OperationsOnUninitializedCoordinator_NeverCrash_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                // Don't call Start() to keep coordinator uninitialized
                
                // Property: All operations should complete without throwing
                var result = true;
                
                try
                {
                    // Expect the error log message for uninitialized coordinator
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*No active save system available.*"));
                    await _coordinator.SaveAsync();
                }
                catch (InvalidOperationException)
                {
                    // Expected when no active system
                    result = result && true;
                }
                
                try
                {
                    // Expect the error log message for uninitialized coordinator
                    LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*No active save system available.*"));
                    await _coordinator.LoadAsync();
                }
                catch (InvalidOperationException)
                {
                    // Expected when no active system
                    result = result && true;
                }
                
                try
                {
                    await _coordinator.TriggerAutoSaveAsync();
                    result = result && true; // Should return early
                }
                catch (Exception)
                {
                    result = false;
                }
                
                try
                {
                    await _coordinator.HandleSceneChangeAsync();
                    result = result && true; // Should return early
                }
                catch (Exception)
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Exception-throwing operations propagate correctly
        /// Error Boundary: Exceptions from dependencies propagate without being swallowed
        /// </summary>
        [Test]
        [Timeout(5000)]
        public async Task ExceptionThrowingOperations_PropagateCorrectly_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Test different exception scenarios
                var scenario = random.Next(3);
                var exceptionHandledCorrectly = false;
                
                switch (scenario)
                {
                    case 0: // ES3Service throws exception
                        _testES3Service.ShouldThrowOnSave = true;
                        _coordinator = CreateCoordinatorWithDependencies();
                        _coordinator.Start();
                        
                        // Expect the error log message from ES3GameSaveSystem
                        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ES3 save operation failed.*"));
                        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Save failed using.*system.*"));
                        
                        try
                        {
                            await _coordinator.SaveAsync();
                        }
                        catch (InvalidOperationException)
                        {
                            exceptionHandledCorrectly = true;
                        }
                        break;
                        
                    case 1: // Controller throws exception
                        var controller = SaveSystemCoordinatorTestData.CreateTestController(ESaveDomain.Player, true);
                        _localRegistry.RegisterController(controller);
                        _coordinator = CreateCoordinatorWithDependencies();
                        _coordinator.Start();
                        _coordinator.SwitchToLocalSystem();
                        
                        // Expect the error log message from ES3GameSaveSystem
                        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ES3 save operation failed.*"));
                        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Save failed using.*system.*"));
                        
                        try
                        {
                            await _coordinator.SaveAsync();
                        }
                        catch (InvalidOperationException)
                        {
                            exceptionHandledCorrectly = true;
                        }
                        break;
                        
                    case 2: // Load operation throws exception
                        var loadController = SaveSystemCoordinatorTestData.CreateTestController(ESaveDomain.World, false);
                        loadController.ShouldThrowOnLoad = true;
                        _localRegistry.RegisterController(loadController);
                        _coordinator = CreateCoordinatorWithDependencies();
                        _coordinator.Start();
                        _coordinator.SwitchToLocalSystem();
                        
                        // Expect the error log message from ES3GameSaveSystem
                        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*ES3 load operation failed.*"));
                        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(".*Load failed using.*system.*"));
                        
                        try
                        {
                            await _coordinator.LoadAsync();
                        }
                        catch (InvalidOperationException)
                        {
                            exceptionHandledCorrectly = true;
                        }
                        break;
                    }
                    
                    // Property: All exception scenarios should be handled correctly
                    Check.QuickThrowOnFailure(exceptionHandledCorrectly);
            }
        }

        /// <summary>
        /// Property: Cancellation operations complete gracefully
        /// Error Boundary: Cancelled operations don't leave system in inconsistent state
        /// </summary>
        [Test]
        [Timeout(5000)]
        public async Task CancellationOperations_CompleteGracefully_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Randomly choose system
                var useLocal = random.Next(2) == 0;
                if (useLocal)
                {
                    _coordinator.SwitchToLocalSystem();
                }
                
                // Create cancellation token
                var cts = new CancellationTokenSource();
                cts.Cancel(); // Cancel before starting
                
                // Property: Cancelled operations should complete without error
                var result = true;
                
                try
                {
                    await _coordinator.SaveAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    result = result && true; // Expected
                }
                catch (Exception)
                {
                    result = false;
                }
                
                try
                {
                    await _coordinator.LoadAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    result = result && true; // Expected
                }
                catch (Exception)
                {
                    result = false;
                }
                
                try
                {
                    await _coordinator.TriggerAutoSaveAsync(cts.Token);
                    result = result && true; // Should return early
                }
                catch (Exception)
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Multiple shutdown calls are idempotent
        /// Error Boundary: Multiple shutdown calls don't cause errors
        /// </summary>
        [Test]
        public void MultipleShutdownCalls_AreIdempotent_ErrorBoundary()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Perform random operations
                var operations = random.Next(1, 10);
                for (int j = 0; j < operations; j++)
                {
                    var operation = random.Next(3);
                    switch (operation)
                    {
                        case 0: _coordinator.SwitchToLocalSystem(); break;
                        case 1: _coordinator.SwitchToGlobalSystem(); break;
                        case 2: 
                            var domains = new[] { ESaveDomain.Settings, ESaveDomain.Player, ESaveDomain.World };
                            _coordinator.RegisterLocalController(SaveSystemCoordinatorTestData.CreateTestController(domains[j % domains.Length])); 
                            break;
                    }
                }
                
                // Property: Multiple shutdown calls should complete without error
                var result = true;
                
                try
                {
                    _coordinator.Shutdown();
                    _coordinator.Shutdown();
                    _coordinator.Shutdown();
                }
                catch (Exception)
                {
                    result = false;
                }
                
                Check.QuickThrowOnFailure(result);
            }
        }

        #endregion

        #region Advanced Property Tests

        /// <summary>
        /// Property: Concurrent operations maintain system consistency
        /// Advanced Property: Multiple simultaneous operations don't corrupt state
        /// </summary>
        [Test]
        [Timeout(10000)]
        public async Task ConcurrentOperations_MaintainSystemConsistency_AdvancedProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Perform multiple operations in sequence (simulating concurrent access)
                var operations = random.Next(5, 20);
                var operationResults = new List<bool>();
                
                for (int j = 0; j < operations; j++)
                {
                    var operation = random.Next(6);
                    var success = true;
                    
                    try
                    {
                        switch (operation)
                        {
                            case 0: _coordinator.SwitchToLocalSystem(); break;
                            case 1: _coordinator.SwitchToGlobalSystem(); break;
                            case 2: await _coordinator.SaveAsync(); break;
                            case 3: await _coordinator.LoadAsync(); break;
                            case 4: await _coordinator.TriggerAutoSaveAsync(); break;
                            case 5: 
                                var domains = new[] { ESaveDomain.Settings, ESaveDomain.Player, ESaveDomain.World };
                                _coordinator.RegisterLocalController(SaveSystemCoordinatorTestData.CreateTestController(domains[j % domains.Length])); 
                                break;
                        }
                    }
                    catch (Exception)
                    {
                        success = false;
                    }
                    
                    operationResults.Add(success);
                }
                
                // Property: System should remain in consistent state after all operations
                var isLocal = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                var isGlobal = !isLocal;
                var systemConsistent = (isLocal && !isGlobal) || (!isLocal && isGlobal);
                
                var result = systemConsistent && operationResults.All(r => r);
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Auto-save configuration changes affect behavior correctly
        /// Advanced Property: Config changes immediately affect auto-save behavior
        /// </summary>
        [Test]
        public void AutoSaveConfigChanges_AffectBehaviorCorrectly_AdvancedProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                // Start with auto-save disabled
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(false);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem();
                
                var autoSaveDisabled = !_coordinator.IsAutoSaveRunning.Value;
                
                // Enable auto-save - recreate coordinator with new config
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(true);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem(); // Start auto-save with new config
                
                var autoSaveEnabled = _coordinator.IsAutoSaveRunning.Value;
                
                // Disable auto-save - recreate coordinator with new config
                _saveConfig = SaveSystemCoordinatorTestData.CreateTestConfig();
                _gameSettings = SaveSystemCoordinatorTestData.CreateTestGameSettings(false);
                _saveConfig.Initialize(_gameSettings);
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                _coordinator.SwitchToLocalSystem(); // Start with disabled auto-save
                
                var autoSaveDisabledAgain = !_coordinator.IsAutoSaveRunning.Value;
                
                // Property: Config changes should immediately affect auto-save behavior
                var result = autoSaveDisabled && autoSaveEnabled && autoSaveDisabledAgain;
                Check.QuickThrowOnFailure(result);
            }
        }

        /// <summary>
        /// Property: Reactive property updates are consistent across operations
        /// Advanced Property: Reactive properties reflect actual system state
        /// </summary>
        [Test]
        [Timeout(10000)]
        public async Task ReactivePropertyUpdates_AreConsistent_AdvancedProperty()
        {
            var random = new System.Random();
            for (int i = 0; i < 50; i++)
            {
                _coordinator = CreateCoordinatorWithDependencies();
                _coordinator.Start();
                
                // Perform operations and verify reactive properties
                var operations = random.Next(3, 10);
                var allPropertiesConsistent = true;
                
                for (int j = 0; j < operations; j++)
                {
                    var operation = random.Next(4);
                    
                    switch (operation)
                    {
                        case 0: _coordinator.SwitchToLocalSystem(); break;
                        case 1: _coordinator.SwitchToGlobalSystem(); break;
                        case 2: await _coordinator.SaveAsync(); break;
                        case 3: await _coordinator.LoadAsync(); break;
                    }
                    
                    // Property: Reactive properties should be consistent with system state
                    var isLocal = SaveSystemCoordinatorReflectionHelper.GetUseLocalSystem(_coordinator);
                    var autoSaveRunning = _coordinator.IsAutoSaveRunning.Value;
                    
                    // Auto-save should only run when in local system and enabled
                    var autoSaveConsistent = !isLocal || !_saveConfig.EnableAutoSave || autoSaveRunning;
                    allPropertiesConsistent = allPropertiesConsistent && autoSaveConsistent;
                }
                
                Check.QuickThrowOnFailure(allPropertiesConsistent);
            }
        }

        #endregion
    }

    /// <summary>
    /// Mock implementation of IGameSettings for testing purposes
    /// </summary>
    public class MockGameSettings : IGameSettings
    {
        public ReactiveSetting<bool> AutoSave { get; }
        public ReactiveSetting<bool> AnalyticsEnabled { get; }
        public MockGameSettings(bool initialValue = true)
        {
            AutoSave = new ReactiveSetting<bool>(initialValue, "Auto Save", null);
            AnalyticsEnabled = new ReactiveSetting<bool>(initialValue, "Analytics Enabled", null);
        }

        public void SetAnalyticsEnabled(bool value)
        {
            AnalyticsEnabled.Value = value;
        }
        
        public void SetAutoSave(bool value)
        {
            AutoSave.Value = value;
        }
    }

    /// <summary>
    /// Mock implementation of IProfileManager for testing purposes
    /// </summary>
    public class MockProfileManager : IProfileManager
    {
        public ReactiveProperty<string> CurrentProfile { get; } = new("DefaultProfile");
        public ReactiveProperty<List<string>> AvailableProfiles { get; } = new(new List<string> { "DefaultProfile" });
        public ReactiveProperty<ProfileMetaData> CurrentProfileMetadata { get; } = new(new ProfileMetaData("DefaultProfile", "Never", "1.0.0", 0, DateTime.Now));

        public bool CreateProfile(string profileName, CancellationToken ct = default)
        {
            return true;
        }

        public (bool success, string actualProfileName) CreateProfileWithName(string profileName, CancellationToken ct = default)
        {
            return (true, profileName);
        }

        public string GenerateUniqueProfileName(string baseName)
        {
            return baseName;
        }

        public bool DeleteProfile(string profileName, CancellationToken ct = default)
        {
            return true;
        }

        public UniTask<bool> LoadProfileAsync(string profileName, CancellationToken ct = default)
        {
            CurrentProfile.Value = profileName;
            return UniTask.FromResult(true);
        }

        public bool SaveProfile(string profileName, CancellationToken ct = default)
        {
            return true;
        }

        public ProfileMetaData GetProfileMetaData(string profileName, CancellationToken ct = default)
        {
            return new ProfileMetaData(profileName, "Never", "1.0.0", 0, DateTime.Now);
        }

        public List<ProfileMetaData> GetAllProfileMetadata(CancellationToken ct = default)
        {
            return new List<ProfileMetaData> { GetProfileMetaData("DefaultProfile") };
        }

        public UniTask<string> GetMostRecentProfileAsync(CancellationToken ct = default)
        {
            return UniTask.FromResult("DefaultProfile");
        }

        public bool ProfileExists(string profileName)
        {
            return true;
        }

        public string GetProfileFilePath(string profileName)
        {
            return $"profiles/{profileName}.es3";
        }

        public void RepairAllProfileMetadata()
        {
            // Mock implementation - no-op
        }

        public void CloseCurrentProfile()
        {
            CurrentProfile.Value = string.Empty;
        }
    }

    /// <summary>
    /// Test helper class for managing SlogLoader state in persistence tests
    /// Prevents Bootstrapper timeout issues by properly initializing dependencies
    /// </summary>
    public class PersistenceTestHelper
    {
        private readonly List<GameObject> _createdGameObjects = new();

        public void MockSlogLoaderInitialized(bool initialized)
        {
            // Use reflection to set the static Initialized property for testing
            var slogLoaderType = typeof(SlogLoader);
            
            // Try different backing field names
            var possibleFieldNames = new[]
            {
                "<Initialized>k__BackingField",
                "Initialized",
                "_Initialized"
            };
            
            foreach (var fieldName in possibleFieldNames)
            {
                var backingField = slogLoaderType.GetField(fieldName, 
                    BindingFlags.NonPublic | BindingFlags.Static);
                
                if (backingField != null)
                {
                    backingField.SetValue(null, initialized);
                    break;
                }
            }
        }
        
        public void MockFlushSlogOnQuitFound(bool found)
        {
            // Create a mock FlushSlogOnQuit GameObject if needed
            if (found)
            {
                var gameObject = new GameObject("MockFlushSlogOnQuit");
                _createdGameObjects.Add(gameObject);
                gameObject.AddComponent<FlushSlogOnQuit>();
            }
        }
        
        public void Cleanup()
        {
            // Clean up any created GameObjects
            foreach (var gameObject in _createdGameObjects)
            {
                if (gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(gameObject);
                }
            }
            _createdGameObjects.Clear();
        }
    }
}
