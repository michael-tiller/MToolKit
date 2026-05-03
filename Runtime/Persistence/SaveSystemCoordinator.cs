using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Persistence
{
    /// <summary>
    ///   Coordinates between global and local save systems.
    ///   Acts as a "man in the middle" to route save/load operations to the appropriate system.
    /// </summary>
    public class SaveSystemCoordinator : IRuntimeSystem
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SaveSystemCoordinator>().ForFeature("Persistence.Coordinator"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly IES3Service es3Service;
    private readonly SaveDomainControllerRegistry globalRegistry;
    private readonly SaveDomainControllerRegistry localRegistry;
    private readonly IProfileManager profileManager;
    private readonly ES3SaveConfig saveConfig;

    // Auto-save functionality
    private CancellationTokenSource autoSaveCts;

    private ES3GameSaveSystem globalSaveSystem;
    private ES3GameSaveSystem localSaveSystem;
    private bool useLocalSystem;

    public SaveSystemCoordinator(IES3Service es3Service, SaveDomainControllerRegistry globalRegistry, SaveDomainControllerRegistry localRegistry, ES3SaveConfig saveConfig,
      IProfileManager profileManager)
    {
      this.es3Service = es3Service ?? throw new ArgumentNullException(nameof(es3Service));
      this.globalRegistry = globalRegistry ?? throw new ArgumentNullException(nameof(globalRegistry));
      this.localRegistry = localRegistry ?? throw new ArgumentNullException(nameof(localRegistry));
      this.saveConfig = saveConfig ?? throw new ArgumentNullException(nameof(saveConfig));
      this.profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));

      // Initialize reactive properties
      IsSaving = new ReactiveProperty<bool>(false);
      IsLoading = new ReactiveProperty<bool>(false);
      LastSaveTime = new ReactiveProperty<string>("Never");
      LastLoadTime = new ReactiveProperty<string>("Never");
      SaveCounter = new ReactiveProperty<int>(0);
      IsAutoSaveRunning = new ReactiveProperty<bool>(false);
      IsAutoSaveExecuting = new ReactiveProperty<bool>(false);

      log.ForMethod().Verbose("SaveSystemCoordinator created with auto-save enabled: {0}", saveConfig.EnableAutoSave);
    }

    public ReactiveProperty<bool> IsSaving { get; }
    public ReactiveProperty<bool> IsLoading { get; }
    public ReactiveProperty<string> LastSaveTime { get; }
    public ReactiveProperty<string> LastLoadTime { get; }
    public ReactiveProperty<int> SaveCounter { get; }
    public ReactiveProperty<bool> IsAutoSaveRunning { get; }
    public ReactiveProperty<bool> IsAutoSaveExecuting { get; }

    #region IRuntimeSystem Members

    public void Start()
    {
      // Initialize global save system
      List<ISaveDomainController> globalControllers = globalRegistry.GetControllers().ToList();
      globalSaveSystem = new ES3GameSaveSystem(globalControllers, es3Service);

      // Initialize local save system (will be updated when local registry is populated)
      List<ISaveDomainController> localControllers = localRegistry.GetControllers().ToList();
      localSaveSystem = new ES3GameSaveSystem(localControllers, es3Service);

      // Delegate reactive properties to the active system
      UpdateReactiveProperties();

      // Note: Auto-save will be started when switching to local system
      // This ensures auto-save only runs in game scenes, not menu scenes

      log.ForMethod().Debug("SaveSystemCoordinator started with {0} global controllers, {1} local controllers, auto-save enabled: {2}",
        globalControllers.Count, localControllers.Count, saveConfig.EnableAutoSave);
    }

    public void Tick(float deltaTime)
    {
      // No per-frame updates needed
    }

    public void LateTick(float deltaTime)
    {
      // No per-frame updates needed
    }

    public void FixedTick(float deltaTime)
    {
      // No per-frame updates needed
    }

    public void Shutdown()
    {
      log.ForMethod().Debug("SaveSystemCoordinator shutting down");

      // Stop auto-save
      StopAutoSave();

      // Dispose auto-save cancellation token source
      autoSaveCts?.Dispose();
      autoSaveCts = null;

      // Clean up reactive properties
      IsSaving?.Dispose();
      IsLoading?.Dispose();
      LastSaveTime?.Dispose();
      LastLoadTime?.Dispose();
      IsAutoSaveRunning?.Dispose();
      IsAutoSaveExecuting?.Dispose();
    }

    #endregion

        /// <summary>
        ///   Switches to using the local save system (for game scenes)
        /// </summary>
        public void SwitchToLocalSystem()
    {
      if (!useLocalSystem)
      {
        useLocalSystem = true;

        // Update local save system with current controllers
        List<ISaveDomainController> localControllers = localRegistry.GetControllers().ToList();
        localSaveSystem = new ES3GameSaveSystem(localControllers, es3Service);

        UpdateReactiveProperties();

        // Start auto-save when switching to local system
        if (saveConfig.EnableAutoSave && !IsAutoSaveRunning.Value)
          StartAutoSave();

        log.ForMethod().Information("Switched to local save system with {0} controllers, auto-save: {1}",
          localControllers.Count, saveConfig.EnableAutoSave);
      }
    }

        /// <summary>
        ///   Switches to using the global save system (for menu scenes)
        /// </summary>
        public void SwitchToGlobalSystem()
    {
      if (useLocalSystem)
      {
        useLocalSystem = false;

        // Stop auto-save when switching to global system
        if (IsAutoSaveRunning.Value)
          StopAutoSave();

        // Close the current profile when exiting the game
        profileManager.CloseCurrentProfile();

        UpdateReactiveProperties();

        log.ForMethod().Information("Switched to global save system, auto-save stopped, profile closed");
      }
    }

        /// <summary>
        ///   Registers a domain controller with the local registry
        /// </summary>
        public void RegisterLocalController(ISaveDomainController controller)
    {
      // Handle null controller gracefully
      if (controller == null)
      {
        log.ForMethod().Warning("Attempted to register null controller with local registry - ignoring");
        return;
      }

      localRegistry.RegisterController(controller);

      // If we're currently using the local system, recreate it with the new controller
      if (useLocalSystem)
      {
        List<ISaveDomainController> localControllers = localRegistry.GetControllers().ToList();
        localSaveSystem = new ES3GameSaveSystem(localControllers, es3Service);
        UpdateReactiveProperties();
        log.ForMethod().Information("Recreated local save system with {0} controllers after registering: {1}",
          localControllers.Count, controller.Domain);
      }

      log.ForMethod().Information("Registered domain controller with local registry: {0} (total local controllers: {1})",
        controller.Domain, localRegistry.Count);
    }

      /// <summary>
      ///   Returns true if any controller registered with the local registry reports
      ///   persisted save data. Used at session boot to choose between NEW-game seeding
      ///   and LOAD-from-save without coupling to any single domain.
      /// </summary>
      public bool HasAnyLocalSaveData()
      {
        foreach (ISaveDomainController controller in localRegistry.GetControllers())
          if (controller != null && controller.HasSaveData())
            return true;
        return false;
      }

      /// <summary>
      ///   Returns the currently active save system (local or global). Lazily initializes
      ///   if Start() has not yet run so callers resolved before startup still get a
      ///   working instance bound to the live registries.
      /// </summary>
      public ES3GameSaveSystem GetActiveSaveSystem()
      {
        if (useLocalSystem)
        {
          if (localSaveSystem == null)
          {
            List<ISaveDomainController> localControllers = localRegistry.GetControllers().ToList();
            localSaveSystem = new ES3GameSaveSystem(localControllers, es3Service);
          }
          return localSaveSystem;
        }

        if (globalSaveSystem == null)
        {
          List<ISaveDomainController> globalControllers = globalRegistry.GetControllers().ToList();
          globalSaveSystem = new ES3GameSaveSystem(globalControllers, es3Service);
        }
        return globalSaveSystem;
      }

    private void UpdateReactiveProperties()
    {
      ES3GameSaveSystem activeSystem = useLocalSystem ? localSaveSystem : globalSaveSystem;

      if (activeSystem != null)
      {
        IsSaving.Value = activeSystem.IsSaving.Value;
        IsLoading.Value = activeSystem.IsLoading.Value;
        LastSaveTime.Value = activeSystem.LastSaveTime.Value;
        LastLoadTime.Value = activeSystem.LastLoadTime.Value;
        SaveCounter.Value = activeSystem.SaveCounter.Value;
      }
    }

    public async UniTask SaveAsync(CancellationToken ct = default)
    {
      // Check cancellation token before proceeding
      ct.ThrowIfCancellationRequested();

      ES3GameSaveSystem activeSystem = useLocalSystem ? localSaveSystem : globalSaveSystem;

      if (activeSystem == null)
      {
        log.ForMethod().Error("No active save system available");
        throw new InvalidOperationException("No active save system available");
      }

      // Log which controllers are available for saving
      SaveDomainControllerRegistry activeRegistry = useLocalSystem ? localRegistry : globalRegistry;
      List<ISaveDomainController> controllers = activeRegistry.GetControllers().ToList();
      log.ForMethod().Verbose("Saving using {0} system with {1} controllers: {2}",
        useLocalSystem ? "local" : "global",
        controllers.Count,
        string.Join(", ", controllers.Select(c => c.Domain.ToString())));

      try
      {
        await activeSystem.SaveAsync(ct);
        UpdateReactiveProperties();

        log.ForMethod().Debug("Save completed successfully using {0} system", useLocalSystem ? "local" : "global");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Save failed using {0} system: {Message}", useLocalSystem ? "local" : "global", ex.Message);
        throw;
      }
    }

    public async UniTask LoadAsync(CancellationToken ct = default)
    {
      // Check cancellation token before proceeding
      ct.ThrowIfCancellationRequested();

      ES3GameSaveSystem activeSystem = useLocalSystem ? localSaveSystem : globalSaveSystem;

      if (activeSystem == null)
      {
        log.ForMethod().Error("No active save system available");
        throw new InvalidOperationException("No active save system available");
      }

      log.ForMethod().Information("Loading using {0} system", useLocalSystem ? "local" : "global");

      try
      {
        await activeSystem.LoadAsync(ct);
        UpdateReactiveProperties();

        log.ForMethod().Information("Load completed successfully using {0} system", useLocalSystem ? "local" : "global");
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Load failed using {0} system: {Message}", useLocalSystem ? "local" : "global", ex.Message);
        throw;
      }
    }

        /// <summary>
        ///   Starts the auto-save system using UniTask
        /// </summary>
        private void StartAutoSave()
    {
      if (IsAutoSaveRunning.Value)
      {
        log.ForMethod().Warning("Auto-save is already running");
        return;
      }

      autoSaveCts = new CancellationTokenSource();
      IsAutoSaveRunning.Value = true;

      // Start the auto-save loop as a fire-and-forget task
      AutoSaveLoopAsync(autoSaveCts.Token).Forget();

      log.ForMethod().Debug("Auto-save started with interval: {0} seconds", saveConfig.AutoSaveIntervalSeconds);
    }

        /// <summary>
        ///   Stops the auto-save system
        /// </summary>
        private void StopAutoSave()
    {
      if (!IsAutoSaveRunning.Value)
        return;

      autoSaveCts?.Cancel();
      IsAutoSaveRunning.Value = false;

      log.ForMethod().Information("Auto-save stopped");
    }

        /// <summary>
        ///   Auto-save loop that runs continuously using UniTask
        /// </summary>
        private async UniTask AutoSaveLoopAsync(CancellationToken ct)
    {
      try
      {
        while (!ct.IsCancellationRequested)
        {
          // Wait for the configured interval
          await UniTask.Delay(TimeSpan.FromSeconds(saveConfig.AutoSaveIntervalSeconds), cancellationToken: ct);

          // Skip auto-save if we're already saving or loading
          if (IsSaving.Value || IsLoading.Value)
          {
            log.ForMethod().Debug("Skipping auto-save - save/load operation in progress");
            continue;
          }

          // Only auto-save when using local system (in-game)
          if (!useLocalSystem)
          {
            log.ForMethod().Debug("Skipping auto-save - not in local system");
            continue;
          }

          try
          {
            log.ForMethod().Verbose("Setting IsAutoSaveExecuting to true");
            IsAutoSaveExecuting.Value = true;
            log.ForMethod().Verbose("Performing auto-save");
            await SaveAsync(ct);
            await UniTask.Delay(saveConfig.AutoSavePaddingMilliseconds);
            log.ForMethod().Verbose("Auto-save completed successfully");
          }
          catch (OperationCanceledException)
          {
            // Expected when shutting down
            break;
          }
          catch (Exception ex)
          {
            log.ForMethod().Error(ex, "Auto-save failed: {Message}", ex.Message);
            // Continue the loop even if auto-save fails
          }
          finally
          {
            log.ForMethod().Information("Setting IsAutoSaveExecuting to false");
            IsAutoSaveExecuting.Value = false;
          }
        }
      }
      catch (OperationCanceledException)
      {
        // Expected when shutting down
        log.ForMethod().Debug("Auto-save loop cancelled");
      }
      finally
      {
        IsAutoSaveRunning.Value = false;
      }
    }

        /// <summary>
        ///   Manually triggers an auto-save (useful for scene changes)
        /// </summary>
        public async UniTask TriggerAutoSaveAsync(CancellationToken ct = default)
    {
      if (!saveConfig.EnableAutoSave)
      {
        log.ForMethod().Debug("Auto-save is disabled, skipping manual trigger");
        return;
      }

      if (IsSaving.Value || IsLoading.Value)
      {
        log.ForMethod().Debug("Skipping manual auto-save - save/load operation in progress");
        return;
      }

      if (!useLocalSystem)
      {
        log.ForMethod().Debug("Skipping manual auto-save - not in local system");
        return;
      }

      // Check cancellation token before starting operations - return gracefully if cancelled
      if (ct.IsCancellationRequested)
      {
        log.ForMethod().Debug("Manual auto-save cancelled before starting");
        return;
      }

      try
      {
        log.ForMethod().Information("Setting IsAutoSaveExecuting to true for manual trigger");
        IsAutoSaveExecuting.Value = true;
        log.ForMethod().Information("Performing manual auto-save");
        await SaveAsync(ct);
        await UniTask.Delay(saveConfig.AutoSavePaddingMilliseconds, cancellationToken: ct);
        log.ForMethod().Information("Manual auto-save completed successfully");
      }
      catch (OperationCanceledException)
      {
        log.ForMethod().Debug("Manual auto-save was cancelled");
        // Return gracefully without rethrowing
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Manual auto-save failed: {Message}", ex.Message);
        throw;
      }
      finally
      {
        log.ForMethod().Information("Setting IsAutoSaveExecuting to false for manual trigger");
        IsAutoSaveExecuting.Value = false;
      }
    }

        /// <summary>
        ///   Handles scene change auto-save if enabled in config
        /// </summary>
        public async UniTask HandleSceneChangeAsync(CancellationToken ct = default)
    {
      if (!saveConfig.AutoSaveOnSceneChange)
      {
        log.ForMethod().Debug("Scene change auto-save is disabled");
        return;
      }

      if (!useLocalSystem)
      {
        log.ForMethod().Debug("Skipping scene change auto-save - not in local system");
        return;
      }

      try
      {
        log.ForMethod().Information("Performing scene change auto-save");
        await TriggerAutoSaveAsync(ct);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Scene change auto-save failed: {Message}", ex.Message);
        // Don't rethrow - scene change auto-save failure shouldn't block scene transitions
      }
    }
  }
}