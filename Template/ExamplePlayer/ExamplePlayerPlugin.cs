using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Template.ExamplePlayer.Persistence;
using MToolKit.Template.ExamplePlayer.Interface;
using MToolKit.Runtime.Audio.Interface;
using Serilog;
using Sirenix.OdinInspector;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Cinemachine;
using MToolKit.Runtime.MessageBus;
using MessagePipe;
using UnityEngine.InputSystem;
using R3;
using MToolKit.Runtime.Music;
using MToolKit.Template.ExamplePlayer.Events;
using MToolKit.Runtime.ExamplePlayer.Events;

/// <summary>
/// Namespace for example player plugin.
/// </summary>
namespace MToolKit.Template.ExamplePlayer
{
    /// <summary>
    /// Example player plugin that manages player data and integrates with the save system
    /// </summary>
    public class ExamplePlayerPlugin : AbstractRuntimePlugin, IDependencyDeclaration
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ExamplePlayerPlugin>().ForFeature("Template.Player"));
        protected static new ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
        
        #region Configuration
        [SerializeField]
        [Required]
        [BoxGroup("Configuration")]
        private GameObject player;

        [SerializeField]
        [Required]
        [BoxGroup("Configuration")]
        private CinemachineVirtualCamera playerFollowCamera;
        [SerializeField]

        [Required]
        [BoxGroup("Configuration")]
        private CinemachineBrain mainCameraBrain;

        [SerializeField]
        [Required]
        [BoxGroup("Configuration")]
        private PlayerInput playerInput;

        [SerializeField]
        [Required]
        [BoxGroup("Configuration")]
        private Animator playerAnimator;


        [SerializeField]
        [Required]
        [BoxGroup("Configuration")]
        private StarterAssets.ThirdPersonController thirdPersonController;
        

        [SerializeField]
        [BoxGroup("Configuration")]
        private bool pauseToggledTime = false;
        

    [SerializeField]
    private AudioClip playerSpawnMusicClip;
        #endregion
        
        #region Injected Dependencies
        private IExamplePlayerService playerService;
        private SaveSystemCoordinator saveCoordinator;
        private IES3Service es3Service;
        private IAudioService audioService;
        #endregion
        
        #region Private Fields
        private PlayerSaveController saveController;
        private bool hasStarted = false;
        private bool isSpawning = false;
        private bool isCorrectingPosition = false;
        private bool hasCompletedPositionReload = false;
        private bool hasSpawnedPlayer = false;
        private bool isContinuousMonitoringRunning = false;
        
        // Semaphore to prevent multiple systems from modifying player position simultaneously
        private readonly SemaphoreSlim positionModificationSemaphore = new SemaphoreSlim(1, 1);
        
        private IObjectResolver resolver;
        private CompositeDisposable disposables = new();
        private CancellationTokenSource continuousMonitoringCts;
        #endregion
        
        #region Position Modification Semaphore
        
        /// <summary>
        /// Safely modifies the player position using a semaphore to prevent race conditions.
        /// Only one system can modify the position at a time.
        /// </summary>
        private async UniTask<bool> SafeModifyPlayerPosition(Func<Transform, UniTask<bool>> positionModifier, string operationName)
        {
            if (PlayerTransform == null)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(SafeModifyPlayerPosition)).Warning("[POSITION_SEMAPHORE] PlayerTransform is null, cannot modify position for operation: {Operation}", operationName);
                return false;
            }

            // Wait for semaphore with timeout to prevent deadlocks
            bool acquired = await positionModificationSemaphore.WaitAsync(1000);
            if (!acquired)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(SafeModifyPlayerPosition)).Error("[POSITION_SEMAPHORE] Failed to acquire semaphore within timeout for operation: {Operation}", operationName);
                return false;
            }

            try
            {
                log.ForGameObject(gameObject).ForMethod(nameof(SafeModifyPlayerPosition)).Debug("[POSITION_SEMAPHORE] Acquired semaphore for operation: {Operation}, current position: {Position}", 
                    operationName, PlayerTransform.position);
                
                bool success = await positionModifier(PlayerTransform);
                
                log.ForGameObject(gameObject).ForMethod(nameof(SafeModifyPlayerPosition)).Debug("[POSITION_SEMAPHORE] Completed operation: {Operation}, success: {Success}, final position: {Position}", 
                    operationName, success, PlayerTransform.position);
                
                return success;
            }
            catch (Exception ex)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(SafeModifyPlayerPosition)).Error(ex, "[POSITION_SEMAPHORE] Error during operation: {Operation}, error: {Message}", 
                    operationName, ex.Message);
                return false;
            }
            finally
            {
                positionModificationSemaphore.Release();
                log.ForGameObject(gameObject).ForMethod(nameof(SafeModifyPlayerPosition)).Debug("[POSITION_SEMAPHORE] Released semaphore for operation: {Operation}", operationName);
            }
        }
        
        #endregion
        
        #region Public Service Access
        [ShowInInspector, ReadOnly]
        public IExamplePlayerService PlayerService => playerService;
        #endregion
        
        #region IDependencyDeclaration Implementation
        public IEnumerable<Type> RequiredServices => new[]
        {
            typeof(IExamplePlayerService),
            typeof(SaveSystemCoordinator)
        };

        public IEnumerable<Type> OptionalServices => new[]
        {
            typeof(IES3Service),
            typeof(IAudioService)
        };
        #endregion
        
        #region IGamePlugin Implementation
        public override void Register(IContainerBuilder builder)
        {
            if (this == null)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(Register)).Error("this is null!");
                return;
            }

            log.ForGameObject(gameObject).ForMethod(nameof(Register)).Debug("Registering ExamplePlayerPlugin services");
            
            // Register the plugin instance
            builder.RegisterInstance(this).AsSelf();
            
            log.ForGameObject(gameObject).ForMethod(nameof(Register)).Debug("ExamplePlayerPlugin registered");
        }
        #endregion
        
        #region IRuntimeSystem Implementation
        public override void PerformSetup(IObjectResolver resolver)
        {
            log.ForGameObject(gameObject).ForMethod(nameof(PerformSetup)).Debug("[PLAYER_SPAWN] ExamplePlayerPlugin.PerformSetup called - Instance: {InstanceId}", GetHashCode());
            
            this.resolver = resolver;
            resolver.InjectGameObject(gameObject);

            // Reset the flags when the plugin is recreated for a new scene
            hasStarted = false;
            isSpawning = false;

            // Dependencies will be resolved when AreDependenciesReady returns true
            log.ForGameObject(gameObject).ForMethod(nameof(PerformSetup)).Debug("[PLAYER_SPAWN] Setup complete, dependencies will be resolved when ready - Instance: {InstanceId}", GetHashCode());

            disposables.Add(GameMessageBroker.GetSubscriber<EnablePlayerMovementMessage>().Subscribe(OnEnablePlayerMovement));
            disposables.Add(GameMessageBroker.GetSubscriber<PauseToggledMessage>().Subscribe(OnPauseToggled));

            if (playerSpawnMusicClip != null)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PerformSetup)).Information("Playing player spawn music clip: {clipName}", playerSpawnMusicClip.name);
                MusicManager.PlayMusic(playerSpawnMusicClip);
            }
            else
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PerformSetup)).Verbose("No player spawn music clip set");
            }
        }
        
        private void OnEnablePlayerMovement(EnablePlayerMovementMessage message)
        {
            if (thirdPersonController != null)
            {
                thirdPersonController.enabled = message.Enable;
            }
        }

        private void OnPauseToggled(PauseToggledMessage message)
        {
            log.ForGameObject(gameObject).ForMethod(nameof(OnPauseToggled)).Information("OnPauseToggled called - IsPaused: {IsPaused}", message.IsPaused);
            // if configured, toggle the time scale
            if (pauseToggledTime)
            {
                Time.timeScale = message.IsPaused ? 0f : 1f;
            }
            // time scale is not toggled, disable the player input
            else 
            {
                // If the game is pausing, also set the animator's speed to 0 to stop it walking
                if (message.IsPaused)
                {
                    playerAnimator.SetFloat("Speed", 0f);
                }
                playerInput.enabled = !message.IsPaused;
            }
        }
        
        private void LogInjectedDependencies()
        {
            log.ForGameObject(gameObject).ForMethod(nameof(LogInjectedDependencies)).Debug("Checking injected dependencies:");
            log.ForGameObject(gameObject).ForMethod(nameof(LogInjectedDependencies)).Debug("  - playerService: {0}", playerService != null ? "injected" : "null");
            log.ForGameObject(gameObject).ForMethod(nameof(LogInjectedDependencies)).Debug("  - saveCoordinator: {0}", saveCoordinator != null ? "injected" : "null");
        }

        public override bool AreDependenciesReady(IObjectResolver resolver)
        {
            // Check if we can resolve the required services
            bool canResolvePlayerService = false;
            bool canResolveSaveCoordinator = false;
            bool hasPlayer = player != null;

            try
            {
                // Try to resolve the services to see if they're available
                var testPlayerService = GameRoot.Resolver.Resolve<IExamplePlayerService>();
                var testSaveCoordinator = GameRoot.Resolver.Resolve<SaveSystemCoordinator>();
                
                canResolvePlayerService = testPlayerService != null;
                canResolveSaveCoordinator = testSaveCoordinator != null;
                
                // If we can resolve them and haven't stored them yet, store them now
                if (canResolvePlayerService && playerService == null)
                {
                    playerService = testPlayerService;
                }
                if (canResolveSaveCoordinator && saveCoordinator == null)
                {
                    saveCoordinator = testSaveCoordinator;
                }
                
                // Try to resolve the optional ES3Service
                if (es3Service == null)
                {
                    try
                    {
                        es3Service = GameRoot.Resolver.Resolve<IES3Service>();
                        log.ForGameObject(gameObject).ForMethod(nameof(AreDependenciesReady)).Debug("IES3Service resolved successfully");
                    }
                    catch (Exception es3Ex)
                    {
                        log.ForGameObject(gameObject).ForMethod(nameof(AreDependenciesReady)).Debug("IES3Service not available (optional): {Message}", es3Ex.Message);
                    }
                }
                
                // Try to resolve the optional IAudioService
                if (audioService == null)
                {
                    try
                    {
                        audioService = GameRoot.Resolver.Resolve<IAudioService>();
                        log.ForGameObject(gameObject).ForMethod(nameof(AreDependenciesReady)).Debug("IAudioService resolved successfully");
                    }
                    catch (Exception audioEx)
                    {
                        log.ForGameObject(gameObject).ForMethod(nameof(AreDependenciesReady)).Debug("IAudioService not available (optional): {Message}", audioEx.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(AreDependenciesReady)).Debug("Services not yet available: {Message}", ex.Message);
            }

            bool allReady = canResolvePlayerService && canResolveSaveCoordinator && hasPlayer;
            
            log.ForGameObject(gameObject).ForMethod(nameof(AreDependenciesReady)).Debug("Dependencies ready check: PlayerService={0}, SaveCoordinator={1}, Player={2}, ES3Service={3}, AudioService={4}, AllReady={5}",
                canResolvePlayerService, canResolveSaveCoordinator, hasPlayer, es3Service != null, audioService != null, allReady);

            return allReady;
        }

        public override void PerformRuntimeInitialization(IObjectResolver resolver)
        {
            log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Debug("[PLAYER_SPAWN] ExamplePlayerPlugin runtime initialization starting - Instance: {InstanceName}", gameObject.name);
            
            // At this point, AreDependenciesReady has returned true, so dependencies are available
            if (hasStarted && player != null)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Debug("[PLAYER_SPAWN] ExamplePlayerPlugin already started and player exists, skipping spawn - Instance: {InstanceName}", gameObject.name);
                return;
            }

            // Prevent duplicate spawning - if we're already spawning or have started, don't spawn again
            if (isSpawning || (hasStarted && player == null))
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Debug("[PLAYER_SPAWN] ExamplePlayerPlugin already spawning or respawning, skipping duplicate spawn - Instance: {InstanceName}", gameObject.name);
                return;
            }

            hasStarted = true;
            isSpawning = true;
            log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Debug("[PLAYER_SPAWN] ExamplePlayerPlugin runtime initialization - spawning player - Instance: {InstanceName}", gameObject.name);

            // Initialize the save controller
            InitializeSaveController();

            // Only spawn if we haven't already spawned and aren't reloading
            if (!hasSpawnedPlayer && !hasCompletedPositionReload)
            {
                SpawnPlayerAsync().Forget();
            }
            else
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Information("[PLAYER_SPAWN] Skipping spawn - already spawned: {HasSpawned}, position reloaded: {HasReloaded}", hasSpawnedPlayer, hasCompletedPositionReload);
            }
            
            log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Debug("[PLAYER_SPAWN] ExamplePlayerPlugin runtime initialization completed - Instance: {InstanceName}", gameObject.name);
        }

        public new void Tick(float deltaTime) { }
        public new void LateTick(float deltaTime) { }
        public new void FixedTick(float deltaTime) { }

        public override void Shutdown()
        {
            base.Shutdown();

            if (this == null || gameObject == null)
            {
                log.ForMethod(nameof(Shutdown)).Warning("[PLAYER_SPAWN] ExamplePlayerPlugin or gameObject is null during shutdown, skipping cleanup");
                return;
            }

            log.ForGameObject(gameObject).ForMethod(nameof(Shutdown)).Debug("[PLAYER_SPAWN] ExamplePlayerPlugin.Shutdown() called - Instance: {InstanceId}", GetHashCode());

            // Cancel continuous monitoring
            continuousMonitoringCts?.Cancel();
            continuousMonitoringCts?.Dispose();
            continuousMonitoringCts = null;
            isContinuousMonitoringRunning = false;

            // Clean up the save controller
            saveController = null;
        }
        #endregion
        
        #region Save System Integration
        private void InitializeSaveController()
        {
            try
            {
                log.ForGameObject(gameObject).ForMethod(nameof(InitializeSaveController)).Verbose("[PLAYER_SPAWN] Initializing save controller - Instance: {InstanceId}", GetHashCode());
                
                if (playerService == null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(InitializeSaveController)).Error("PlayerService is null, cannot initialize save controller");
                    return;
                }

                if (saveCoordinator == null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(InitializeSaveController)).Error("SaveSystemCoordinator is null, cannot initialize save controller");
                    return;
                }
                
                if (es3Service == null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(InitializeSaveController)).Warning("IES3Service is null, save/load functionality will be disabled");
                    return;
                }
                
                // Create the save controller
                saveController = new PlayerSaveController(playerService, es3Service);
                
                // Register with the save system coordinator
                saveCoordinator.RegisterLocalController(saveController);
                
                log.ForGameObject(gameObject).ForMethod(nameof(InitializeSaveController)).Debug("[PLAYER_SPAWN] PlayerSaveController registered with SaveSystemCoordinator - Instance: {InstanceId}", GetHashCode());
            }
            catch (Exception ex)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(InitializeSaveController)).Error(ex, "Failed to initialize PlayerSaveController: {Message}", ex.Message);
            }
        }
        
        /// <summary>
        /// Reload player position from save data - called by ExampleGameInstaller after scene loads
        /// </summary>
        public async UniTask ReloadPlayerPositionAsync()
        {
            try
            {
                // Prevent redundant calls from ExampleGameInstaller
                if (hasCompletedPositionReload)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Debug("[PLAYER_SPAWN] Position reload already completed, skipping redundant call from GameInstaller");
                    return;
                }

                // Mark as completed IMMEDIATELY to prevent any redundant calls
                hasCompletedPositionReload = true;

                if (saveController != null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Verbose("[PLAYER_SPAWN] Reloading player position after scene load - Instance: {InstanceId}", GetHashCode());
                    
                    // Use semaphore to safely reload position
                    bool success = await SafeModifyPlayerPosition(async (transform) =>
                    {
                        log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Debug("[PLAYER_SPAWN] Before ReloadPlayerPositionAsync - position: {Position}", transform.position);
                        
                        // Disable ThirdPersonController to prevent it from interfering with position loading
                        if (thirdPersonController != null)
                        {
                            thirdPersonController.enabled = false;
                            log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Debug("[PLAYER_SPAWN] Disabled ThirdPersonController during position reload");
                        }
                        
                        await saveController.ReloadPlayerPositionAsync(CancellationToken.None);
                        
                        // Keep ThirdPersonController disabled - it will be re-enabled by continuous monitoring when position is stable
                        if (thirdPersonController != null)
                        {
                            log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Debug("[PLAYER_SPAWN] Keeping ThirdPersonController disabled to prevent position resets");
                        }
                        
                        log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Debug("[PLAYER_SPAWN] After ReloadPlayerPositionAsync - position: {Position}", transform.position);
                        
                        return true;
                    }, "ReloadPlayerPositionAsync");
                    
                    if (success)
                    {
                        // Automatically handle the fade after position reload completes
                        // This ensures the fade happens even if the GameInstaller gets destroyed
                        log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Verbose("[PLAYER_SPAWN] Position reload completed, automatically triggering fade");
                        await HandlePositionReloadComplete();
                    }
                    else
                    {
                        log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Error("[PLAYER_SPAWN] Failed to reload player position safely");
                    }
                }
                else
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Warning("[PLAYER_SPAWN] SaveController is null, cannot reload player position - Instance: {InstanceId}", GetHashCode());
                }
            }
            catch (Exception ex)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(ReloadPlayerPositionAsync)).Error(ex, "[PLAYER_SPAWN] Error during ReloadPlayerPositionAsync: {Message}", ex.Message);
                throw;
            }
        }
        
        /// <summary>
        /// Handle the completion of position reload - called by ExampleGameInstaller after position is loaded
        /// </summary>
        public async UniTask HandlePositionReloadComplete()
        {
            log.ForGameObject(gameObject).ForMethod(nameof(HandlePositionReloadComplete)).Verbose("[PLAYER_SPAWN] HandlePositionReloadComplete called - starting fade process");
            
            // Log position IMMEDIATELY when method starts - before any delays
            if (PlayerTransform != null)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(HandlePositionReloadComplete)).Debug("[PLAYER_SPAWN] IMMEDIATE - position at start: {Position}", PlayerTransform.position);
            }
            
            try
            {
                // Log position immediately when method starts
                if (PlayerTransform != null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(HandlePositionReloadComplete)).Debug("[PLAYER_SPAWN] IMMEDIATE - position at start: {Position}", PlayerTransform.position);
                }
                
                // Log position immediately (no delay needed)
                if (PlayerTransform != null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(HandlePositionReloadComplete)).Debug("[PLAYER_SPAWN] Position check - no delay: {Position}", PlayerTransform.position);
                }
                
                // Log position before fade (no delay)
                if (PlayerTransform != null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(HandlePositionReloadComplete)).Debug("[PLAYER_SPAWN] Before fade - position: {Position}", PlayerTransform.position);
                }

                // Late fallback: Check if position was reset and correct it before fade
                await CorrectPositionIfNeeded();
                
                // Final safety check: Ensure position is still correct
                await FinalPreFadeSafetyCheck();
                
                // Note: Fade-in will be triggered by continuous monitoring once position is stable
                
                log.ForGameObject(gameObject).ForMethod(nameof(HandlePositionReloadComplete)).Debug("[PLAYER_SPAWN] HandlePositionReloadComplete completed successfully");
                
                // Start continuous monitoring for any late position resets
                if (!isContinuousMonitoringRunning)
                {
                    isContinuousMonitoringRunning = true;
                    continuousMonitoringCts = new CancellationTokenSource();
                    StartContinuousPositionMonitoring(continuousMonitoringCts.Token).Forget();
                }
            }
            catch (Exception ex)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(HandlePositionReloadComplete)).Error(ex, "[PLAYER_SPAWN] Error during position reload completion handling: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Late fallback mechanism to correct player position if it was reset by a race condition.
        /// This method checks if the player is at the default spawn position and reloads the saved position if needed.
        /// </summary>
        private async UniTask CorrectPositionIfNeeded()
        {
            if (PlayerTransform == null)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Warning("[PLAYER_SPAWN] PlayerTransform is null, cannot correct position");
                return;
            }

            // Prevent multiple simultaneous correction attempts
            if (isCorrectingPosition)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Debug("[PLAYER_SPAWN] Position correction already in progress, skipping");
                return;
            }

            isCorrectingPosition = true;
            
            try
            {
                var currentPosition = PlayerTransform.position;
                var currentRotation = PlayerTransform.rotation;

                // Check if player is at default spawn position (0,0,0 or close to it)
                bool isAtDefaultPosition = Vector3.Distance(currentPosition, Vector3.zero) < 0.1f;
                
                log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Debug("[PLAYER_SPAWN] Position correction check - current: {Position}, isDefault: {IsDefault}", 
                    currentPosition, isAtDefaultPosition);

                if (isAtDefaultPosition)
                {
                    // Check if this is a new game (no save data exists) or a loaded game that needs correction
                    bool hasSaveData = saveController?.HasSaveData() ?? false;
                    
                    if (hasSaveData)
                    {
                        log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Information("[PLAYER_SPAWN] Player at default position with save data, attempting to reload saved position");
                        
                        try
                        {
                            // Force reload the saved position by temporarily resetting the load state
                            await saveController.ReloadPlayerPositionAsync(CancellationToken.None);
                            
                            // Verify the position was corrected
                            var correctedPosition = PlayerTransform.position;
                            var correctedRotation = PlayerTransform.rotation;
                            
                            log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Information("[PLAYER_SPAWN] Position correction result - before: {BeforePosition}, after: {AfterPosition}", 
                                currentPosition, correctedPosition);
                            
                            // If still at default position, log a warning
                            if (Vector3.Distance(correctedPosition, Vector3.zero) < 0.1f)
                            {
                                log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Warning("[PLAYER_SPAWN] Position correction failed - player still at default position");
                            }
                        }
                        catch (Exception ex)
                        {
                            log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Error(ex, "[PLAYER_SPAWN] Error during position correction: {Message}", ex.Message);
                        }
                    }
                    else
                    {
                        log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Information("[PLAYER_SPAWN] Player at default position with no save data - this is a new game, position is correct");
                    }
                }
                else
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(CorrectPositionIfNeeded)).Debug("[PLAYER_SPAWN] Player position is valid, no correction needed");
                }
                
                // Final validation: Ensure position is correct before proceeding with fade
                await ValidateFinalPosition();
            }
            finally
            {
                isCorrectingPosition = false;
            }
        }

        /// <summary>
        /// Final validation to ensure the player position is correct before the fade begins.
        /// This provides an additional safety check against race conditions.
        /// </summary>
        private async UniTask ValidateFinalPosition()
        {
            if (PlayerTransform == null) return;

            var finalPosition = PlayerTransform.position;
            var finalRotation = PlayerTransform.rotation;
            
            log.ForGameObject(gameObject).ForMethod(nameof(ValidateFinalPosition)).Debug("[PLAYER_SPAWN] Final position validation - position: {Position}, rotation: {Rotation}", 
                finalPosition, finalRotation.eulerAngles);
            
            // Check if we're still at the default position after all corrections
            bool isStillAtDefault = Vector3.Distance(finalPosition, Vector3.zero) < 0.1f;
            
            if (isStillAtDefault)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(ValidateFinalPosition)).Warning("[PLAYER_SPAWN] Final validation failed - player still at default position. This may indicate a deeper issue with save data or player spawning.");
                
                // Additional attempt: Try to get the last known good position from save controller
                try
                {
                    // This is a last resort - we'll try one more time with a small delay
                    await UniTask.Delay(10);
                    await saveController.ReloadPlayerPositionAsync(CancellationToken.None);
                    
                    var lastAttemptPosition = PlayerTransform.position;
                    log.ForGameObject(gameObject).ForMethod(nameof(ValidateFinalPosition)).Verbose("[PLAYER_SPAWN] Last attempt position: {Position}", lastAttemptPosition);
                }
                catch (Exception ex)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(ValidateFinalPosition)).Error(ex, "[PLAYER_SPAWN] Error during final position validation: {Message}", ex.Message);
                }
            }
            else
            {
                log.ForGameObject(gameObject).ForMethod(nameof(ValidateFinalPosition)).Verbose("[PLAYER_SPAWN] Final position validation passed - player at valid position: {Position}", finalPosition);
            }
        }

        /// <summary>
        /// Final safety check right before the fade message is published.
        /// This catches any last-minute position resets that might occur.
        /// </summary>
        private async UniTask FinalPreFadeSafetyCheck()
        {
            if (PlayerTransform == null) return;

            // Prevent multiple simultaneous correction attempts
            if (isCorrectingPosition)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(FinalPreFadeSafetyCheck)).Debug("[PLAYER_SPAWN] Position correction already in progress, skipping pre-fade check");
                return;
            }

            var position = PlayerTransform.position;
            bool isAtDefaultPosition = Vector3.Distance(position, Vector3.zero) < 0.1f;
            
            log.ForGameObject(gameObject).ForMethod(nameof(FinalPreFadeSafetyCheck)).Debug("[PLAYER_SPAWN] Final pre-fade safety check - position: {Position}, isDefault: {IsDefault}", 
                position, isAtDefaultPosition);
            
            if (isAtDefaultPosition)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(FinalPreFadeSafetyCheck)).Warning("[PLAYER_SPAWN] CRITICAL: Player at default position right before fade! Attempting emergency correction...");
                
                try
                {
                    isCorrectingPosition = true;
                    
                    // Emergency correction with minimal delay
                    await UniTask.Delay(5);
                    await saveController.ReloadPlayerPositionAsync(CancellationToken.None);
                    
                    var correctedPosition = PlayerTransform.position;
                    log.ForGameObject(gameObject).ForMethod(nameof(FinalPreFadeSafetyCheck)).Information("[PLAYER_SPAWN] Emergency correction result: {Position}", correctedPosition);
                }
                catch (Exception ex)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(FinalPreFadeSafetyCheck)).Error(ex, "[PLAYER_SPAWN] Emergency correction failed: {Message}", ex.Message);
                }
                finally
                {
                    isCorrectingPosition = false;
                }
            }
        }

        /// <summary>
        /// Post-fade safety check to monitor for any late position resets.
        /// This runs after the fade message is published to catch any delayed resets.
        /// </summary>
        private async UniTask PostFadeSafetyCheck()
        {
            if (PlayerTransform == null) return;

            // Wait a bit to see if anything resets the position after the fade message
            await UniTask.Delay(10); // Reduced delay to catch the reset faster
            
            var position = PlayerTransform.position;
            bool isAtDefaultPosition = Vector3.Distance(position, Vector3.zero) < 0.1f;
            
            log.ForGameObject(gameObject).ForMethod(nameof(PostFadeSafetyCheck)).Debug("[PLAYER_SPAWN] Post-fade safety check - position: {Position}, isDefault: {IsDefault}", 
                position, isAtDefaultPosition);
            
            if (isAtDefaultPosition)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PostFadeSafetyCheck)).Warning("[PLAYER_SPAWN] CRITICAL: Player position reset after fade message! This indicates a deeper race condition. Attempting final correction...");
                
                try
                {
                    // Final attempt to correct the position
                    await saveController.ReloadPlayerPositionAsync(CancellationToken.None);
                    
                    var finalPosition = PlayerTransform.position;
                    log.ForGameObject(gameObject).ForMethod(nameof(PostFadeSafetyCheck)).Information("[PLAYER_SPAWN] Final correction result: {Position}", finalPosition);
                    
                    // If still at default, this is a serious issue
                    if (Vector3.Distance(finalPosition, Vector3.zero) < 0.1f)
                    {
                        log.ForGameObject(gameObject).ForMethod(nameof(PostFadeSafetyCheck)).Error("[PLAYER_SPAWN] FATAL: All correction attempts failed. Player remains at default position. This indicates a fundamental issue with the save system or player spawning.");
                    }
                }
                catch (Exception ex)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(PostFadeSafetyCheck)).Error(ex, "[PLAYER_SPAWN] Final correction failed: {Message}", ex.Message);
                }
            }
            else
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PostFadeSafetyCheck)).Information("[PLAYER_SPAWN] Post-fade safety check passed - player at valid position: {Position}", position);
            }
        }
        #endregion
        
        #region Player Spawning
        private UniTask SpawnPlayerAsync()
        {
            try
            {
                // Prevent multiple spawns that would reset position
                if (hasSpawnedPlayer)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(SpawnPlayerAsync)).Warning("[PLAYER_SPAWN] Player already spawned, skipping duplicate spawn to prevent position reset");
                    return UniTask.CompletedTask;
                }

                if (player == null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(SpawnPlayerAsync)).Warning("[PLAYER_SPAWN] Player is not assigned, cannot spawn player");
                    return UniTask.CompletedTask;
                }

                if (playerService == null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(SpawnPlayerAsync)).Error("PlayerService is null, cannot spawn player");
                    return UniTask.CompletedTask;
                }
                // Inject dependencies into the player instance
                if (resolver != null)
                {
                    resolver.InjectGameObject(player);
                }
                
                // Assign the transform to the service for save/load system
                playerService.PlayerTransform = player.transform;
                
                // Inject audio service into ThirdPersonController if available
                InjectAudioServiceIntoPlayer();
                
                // Player is now spawned at the default spawn position
                // Position loading will be handled by ReloadPlayerPositionAsync() to avoid race conditions
                log.ForGameObject(gameObject).ForMethod(nameof(SpawnPlayerAsync)).Verbose("[PLAYER_SPAWN] Player spawned at default position: {Position}. Position loading will be handled separately.", 
                    player.transform.position);
                
                // Mark as spawned to prevent duplicate spawns
                hasSpawnedPlayer = true;
                
                log.ForGameObject(gameObject).ForMethod(nameof(SpawnPlayerAsync)).Information("[PLAYER_SPAWN] Player spawned successfully. Final position: {Position}, rotation: {Rotation}. Save data loaded by PlayerSaveController.", 
                    player.transform.position, player.transform.rotation.eulerAngles);
                
                // Now reload the player position from save data
                log.ForGameObject(gameObject).ForMethod(nameof(SpawnPlayerAsync)).Debug("[PLAYER_SPAWN] Triggering position reload after spawn");
                ReloadPlayerPositionAsync().Forget();
            }
            catch (Exception ex)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(SpawnPlayerAsync)).Error(ex, "Failed to spawn player: {Message}", ex.Message);
            }
            finally
            {
                // Clear the spawning flag when done
                isSpawning = false;
            }
            
            return UniTask.CompletedTask;
        }
        
        /// <summary>
        /// Injects the audio service into the ThirdPersonController if available
        /// </summary>
        private void InjectAudioServiceIntoPlayer()
        {
            if (player == null || audioService == null)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(InjectAudioServiceIntoPlayer)).Debug("Skipping audio service injection - Player: {HasPlayer}, AudioService: {HasAudioService}", 
                    player != null, audioService != null);
                return;
            }

            try
            {
                // Use the direct field reference
                if (thirdPersonController != null)
                {
                    // Use the public SetAudioService method
                    thirdPersonController.SetAudioService(audioService);
                    log.ForGameObject(gameObject).ForMethod(nameof(InjectAudioServiceIntoPlayer)).Information("Successfully injected IAudioService into ThirdPersonController");
                }
                else
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(InjectAudioServiceIntoPlayer)).Warning("ThirdPersonController field is null - audio integration not available");
                }
            }
            catch (Exception ex)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(InjectAudioServiceIntoPlayer)).Error(ex, "Failed to inject audio service into ThirdPersonController: {Message}", ex.Message);
            }
        }
        
        /// <summary>
        /// Public method to respawn the player (useful for respawn functionality)
        /// </summary>
        public void RespawnPlayer()
        {
            log.ForGameObject(gameObject).ForMethod(nameof(RespawnPlayer)).Information("Respawning player");
            SpawnPlayerAsync().Forget();
        }
        
        /// <summary>
        /// Gets the current player instance (null if not spawned)
        /// </summary>
        public GameObject PlayerInstance => player;
        
        /// <summary>
        /// Gets the player transform with position change tracking
        /// </summary>
        public Transform PlayerTransform 
        {
            get 
            {
                if (player == null) return null;
                
                // Track when position is being accessed
                var transform = player.transform;
                if (transform != null)
                {
                    // Log if position is zero (this will help us track when it gets reset)
                    // Silenced because the deadlock was solved by disabling the third person controller
                    // during position reload
                    if (Vector3.Distance(transform.position, Vector3.zero) < 0.1f)
                    {
                        log.ForGameObject(gameObject).ForMethod("PlayerTransform.get").Verbose("[PLAYER_SPAWN] CRITICAL: PlayerTransform.position is being accessed and it's ZERO! Stack trace will show what's accessing it.");
                    }
                }
                return transform;
            }
        }
        
        /// <summary>
        /// Continuous monitoring for position resets that happen after all our safety checks.
        /// This runs for a few seconds to catch any late resets.
        /// </summary>
        private async UniTask StartContinuousPositionMonitoring(CancellationToken cancellationToken = default)
        {
            const int maxMonitoringTimeMs = 2000; // Monitor for 2 seconds
            const int checkIntervalMs = 10; // Check every 10ms
            const int stablePositionTimeMs = 50; // Position must be stable for .05 seconds before re-enabling controller
            var startTime = DateTime.UtcNow;
            var lastStablePositionTime = DateTime.UtcNow;
            bool thirdPersonControllerReEnabled = false;
            
            log.ForGameObject(gameObject).ForMethod(nameof(StartContinuousPositionMonitoring)).Verbose("[PLAYER_SPAWN] Starting continuous position monitoring for {0}ms", maxMonitoringTimeMs);
            
            while ((DateTime.UtcNow - startTime).TotalMilliseconds < maxMonitoringTimeMs && !cancellationToken.IsCancellationRequested)
            {
                await UniTask.Delay(checkIntervalMs, cancellationToken: cancellationToken);
                
                if (PlayerTransform != null)
                {
                    var position = PlayerTransform.position;
                    bool isAtDefaultPosition = Vector3.Distance(position, Vector3.zero) < 0.1f;
                    
                    if (isAtDefaultPosition)
                    {
                        // Check if this is a new game (no save data) or a loaded game that needs correction
                        bool hasSaveData = saveController?.HasSaveData() ?? false;
                        
                        if (hasSaveData)
                        {
                            log.ForGameObject(gameObject).ForMethod(nameof(StartContinuousPositionMonitoring)).Warning("[PLAYER_SPAWN] CONTINUOUS MONITOR: Position reset detected! Attempting correction...");
                            
                            try
                            {
                                await saveController.ReloadPlayerPositionAsync(CancellationToken.None);
                                var correctedPosition = PlayerTransform.position;
                                log.ForGameObject(gameObject).ForMethod(nameof(StartContinuousPositionMonitoring)).Debug("[PLAYER_SPAWN] CONTINUOUS MONITOR: Correction result: {Position}", correctedPosition);
                                
                                // Reset stable position timer after correction
                                lastStablePositionTime = DateTime.UtcNow;
                            }
                            catch (Exception ex)
                            {
                                log.ForGameObject(gameObject).ForMethod(nameof(StartContinuousPositionMonitoring)).Error(ex, "[PLAYER_SPAWN] CONTINUOUS MONITOR: Correction failed: {Message}", ex.Message);
                            }
                        }
                        else
                        {
                            log.ForGameObject(gameObject).ForMethod(nameof(StartContinuousPositionMonitoring)).Debug("[PLAYER_SPAWN] CONTINUOUS MONITOR: Player at default position with no save data - this is a new game, position is correct");
                            
                            // For new games, the default position (0,0,0) is correct, so we can trigger fade-in
                            if (!thirdPersonControllerReEnabled && 
                                (DateTime.UtcNow - lastStablePositionTime).TotalMilliseconds >= stablePositionTimeMs)
                            {
                                log.ForGameObject(gameObject).ForMethod(nameof(StartContinuousPositionMonitoring)).Verbose("[PLAYER_SPAWN] CONTINUOUS MONITOR: New game - position stable for {0}ms, triggering fade-in", stablePositionTimeMs);
                                GameMessageBroker.Publish(new FadeBlackoutMessage(0f, 0.1f));
                                await UniTask.Delay(100);
                                GameMessageBroker.Publish(new EnablePlayerMovementMessage(true));
                                thirdPersonControllerReEnabled = true;
                            }
                        }
                    }
                    else
                    {
                        // Position is stable, check if we can re-enable ThirdPersonController
                        if (!thirdPersonControllerReEnabled && 
                            (DateTime.UtcNow - lastStablePositionTime).TotalMilliseconds >= stablePositionTimeMs)
                        {
                            // First trigger the fade-in now that position is stable
                            log.ForGameObject(gameObject).ForMethod(nameof(StartContinuousPositionMonitoring)).Verbose("[PLAYER_SPAWN] CONTINUOUS MONITOR: Position stable for {0}ms, triggering fade-in", stablePositionTimeMs);
                            GameMessageBroker.Publish(new FadeBlackoutMessage(0f, 0.1f));
                            await UniTask.Delay(100);
                            GameMessageBroker.Publish(new EnablePlayerMovementMessage(true));
                            thirdPersonControllerReEnabled = true;
                        }
                    }
                }
            }
            
            log.ForGameObject(gameObject).ForMethod(nameof(StartContinuousPositionMonitoring)).Debug("[PLAYER_SPAWN] Continuous position monitoring completed");
            isContinuousMonitoringRunning = false;
        }
        
        #endregion
        
        #region Async Initialization
        public async UniTask InitializeAsync(IObjectResolver resolver, CancellationToken ct = default)
        {
            // Perform setup first
            PerformSetup(resolver);

            // Wait for dependencies to be ready with timeout
            const int maxWaitTimeMs = 5000; // 5 second timeout
            const int checkIntervalMs = 50; // Check every 50ms
            var startTime = DateTime.UtcNow;
            
            while (!AreDependenciesReady(resolver) && !ct.IsCancellationRequested)
            {
                var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                if (elapsedMs > maxWaitTimeMs)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(InitializeAsync)).Warning("Dependencies not ready after {0}ms timeout", maxWaitTimeMs);
                    break;
                }
                
                await UniTask.Delay(checkIntervalMs, cancellationToken: ct);
            }

            if (ct.IsCancellationRequested)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(InitializeAsync)).Warning("Initialization cancelled");
                return;
            }

            // Perform runtime initialization
            PerformRuntimeInitialization(resolver);

            // Trigger initial fade-in and enable player movement after a short delay
            // This ensures the game starts visible and playable even if position monitoring delays
            TriggerInitialFadeInAsync().Forget();

            log.ForGameObject(gameObject).ForMethod(nameof(InitializeAsync)).Debug("ExamplePlayerPlugin async initialization completed");
        }
        
        private async UniTask TriggerInitialFadeInAsync()
        {
            // Wait a bit to ensure everything is initialized
            await UniTask.Delay(500);
            
            // Check if fade-in already happened (continuous monitoring might have triggered it)
            if (thirdPersonController != null && thirdPersonController.enabled)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(TriggerInitialFadeInAsync)).Verbose("[PLAYER_SPAWN] Player movement already enabled, skipping initial fade-in trigger");
                return;
            }
            
            log.ForGameObject(gameObject).ForMethod(nameof(TriggerInitialFadeInAsync)).Information("[PLAYER_SPAWN] Triggering initial fade-in and enabling player movement");
            GameMessageBroker.Publish(new FadeBlackoutMessage(0f, 0.5f));
            await UniTask.Delay(100);
            GameMessageBroker.Publish(new EnablePlayerMovementMessage(true));
        }

        private void OnDestroy()
        {
            continuousMonitoringCts?.Cancel();
            continuousMonitoringCts?.Dispose();
            isContinuousMonitoringRunning = false;
            positionModificationSemaphore?.Dispose();

            disposables.Dispose();
            disposables = null;
        }
        #endregion
    }
}
