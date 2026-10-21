using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Persistence.Migration;
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
    public class SaveSystemCoordinator : IRuntimeSystem, IOrderedSaveLoadExecutor
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SaveSystemCoordinator>().ForFeature("Persistence.Coordinator"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    public event Action<ES3FlushReceipt> DurableSaveCompleted;

    private readonly IES3Service es3Service;
    private readonly SaveDomainControllerRegistry globalRegistry;
    private readonly SaveDomainControllerRegistry localRegistry;
    private readonly IProfileManager profileManager;
    private readonly ES3SaveConfig saveConfig;
    private readonly ITruncationReporter truncationReporter;

    // Serializes concurrent LoadAsync invocations so the truncation reporter's load scope
    // is not corrupted by overlapping callers. AvailableWaitHandle is never accessed, so
    // disposal is not required (see Shutdown).
    private readonly SemaphoreSlim loadGate = new(1, 1);
    private readonly object sessionBoundaryGate = new();
    private int activePersistenceOperations;
    private readonly List<IRequiredSavePreparationParticipant> requiredSavePreparations = new();
    private PersistenceOperation requiredPreparationOwner;
    private long persistenceSessionGeneration;

    // Auto-save functionality
    private CancellationTokenSource autoSaveCts;

    private ES3GameSaveSystem globalSaveSystem;
    private ES3GameSaveSystem localSaveSystem;
    private bool useLocalSystem;
    private bool failedLocalRestore;
    private bool applicationSaveBlocked;

    public SaveSystemCoordinator(IES3Service es3Service, SaveDomainControllerRegistry globalRegistry, SaveDomainControllerRegistry localRegistry, ES3SaveConfig saveConfig,
      IProfileManager profileManager, ITruncationReporter truncationReporter)
    {
      this.es3Service = es3Service ?? throw new ArgumentNullException(nameof(es3Service));
      this.globalRegistry = globalRegistry ?? throw new ArgumentNullException(nameof(globalRegistry));
      this.localRegistry = localRegistry ?? throw new ArgumentNullException(nameof(localRegistry));
      this.saveConfig = saveConfig ?? throw new ArgumentNullException(nameof(saveConfig));
      this.profileManager = profileManager ?? throw new ArgumentNullException(nameof(profileManager));
      this.truncationReporter = truncationReporter ?? throw new ArgumentNullException(nameof(truncationReporter));

      // Initialize reactive properties
      IsSaving = new ReactiveProperty<bool>(false);
      IsLoading = new ReactiveProperty<bool>(false);
      LastSaveTime = new ReactiveProperty<string>("Never");
      LastLoadTime = new ReactiveProperty<string>("Never");
      SaveCounter = new ReactiveProperty<int>(0);
      IsAutoSaveRunning = new ReactiveProperty<bool>(false);
      IsAutoSaveExecuting = new ReactiveProperty<bool>(false);
      IsSaveBlocked = new ReactiveProperty<bool>(false);
      RestoreInProgress = new ReactiveProperty<bool>(false);

      log.ForMethod().Verbose("SaveSystemCoordinator created with auto-save enabled: {0}", saveConfig.EnableAutoSave);
    }

    public ReactiveProperty<bool> IsSaving { get; }
    public ReactiveProperty<bool> IsLoading { get; }
    public ReactiveProperty<string> LastSaveTime { get; }
    public ReactiveProperty<string> LastLoadTime { get; }
    public ReactiveProperty<int> SaveCounter { get; }
    public ReactiveProperty<bool> IsAutoSaveRunning { get; }
    public ReactiveProperty<bool> IsAutoSaveExecuting { get; }

    /// <summary>
    ///   Sticky-true after a successful load drains a <see cref="SaveTruncatedOnLoadMessage.Severity.BlockOverwrite"/>
    ///   truncation report. Set by THIS coordinator (not the UI) so the save-safety invariant holds even when
    ///   no presenter is alive. Severe truncation and global restore failures require app exit /
    ///   global container teardown. A failed local restore can recover only through
    ///   <see cref="BeginLocalSessionAfterDiscard"/> after the entire gameplay scope is discarded.
    ///   Blocks BOTH local and global save paths uniformly (Phase F boundary F2).
    /// </summary>
    public ReactiveProperty<bool> IsSaveBlocked { get; }

    /// <summary>
    /// Prevents save writes after a failed required restore. Local failure recovery requires
    /// discarding the entire gameplay scope; global failure remains blocked for this lifetime.
    /// Existing application-level blocks are never downgraded to recoverable local failures.
    /// </summary>
    public void BlockSavesForFailedLoad()
      => BlockSavesForFailedLoad(useLocalSystem);

    private void BlockSavesForFailedLoad(bool wasLocalRestore)
    {
      if (IsSaveBlocked.Value && !failedLocalRestore)
        applicationSaveBlocked = true;
      if (wasLocalRestore)
        failedLocalRestore = true;
      else
        applicationSaveBlocked = true;

      IsSaveBlocked.Value = true;
      StopAutoSave();
      log.ForMethod().Error("Save blocked: required controller failed during load; discard the current gameplay scope before retrying");
    }

    /// <summary>
    /// Begins a new local persistence session after the application has destroyed the previous
    /// gameplay container, unregistered every local controller and unsubscribed its hydrators.
    /// Rejects busy or retained scopes. Clears only a failed-local-restore block; severe truncation,
    /// global failure and unclassified existing blocks remain sticky for the coordinator lifetime.
    /// This method does not dispose gameplay objects or authorize reuse of a partially loaded world.
    /// </summary>
    public void BeginLocalSessionAfterDiscard()
    {
      lock (sessionBoundaryGate)
      {
        if (activePersistenceOperations != 0)
          throw new InvalidOperationException("Cannot begin a local session while controller persistence is active or queued.");
        if (!loadGate.Wait(0))
          throw new InvalidOperationException("Cannot begin a local session while a load transaction is active.");

        try
        {
          if (localRegistry.Count != 0 || PostLoadHydrate != null)
            throw new InvalidOperationException("Discard the old gameplay scope and unregister its controllers and hydrators first.");
          if (IsSaving.Value || IsLoading.Value || RestoreInProgress.Value || IsAutoSaveExecuting.Value ||
              es3Service.IsSaving.Value || es3Service.IsLoading.Value ||
              (localSaveSystem?.IsSaving.Value ?? false) || (localSaveSystem?.IsLoading.Value ?? false) ||
              (globalSaveSystem?.IsSaving.Value ?? false) || (globalSaveSystem?.IsLoading.Value ?? false))
            throw new InvalidOperationException("Cannot begin a local session while persistence or restore is active.");

          if (IsSaveBlocked.Value && !failedLocalRestore)
            applicationSaveBlocked = true;
          StopAutoSave();
          localSaveSystem = new ES3GameSaveSystem(Array.Empty<ISaveDomainController>(), es3Service);
          persistenceSessionGeneration++;
          failedLocalRestore = false;
          IsSaveBlocked.Value = applicationSaveBlocked;
          LastLoadTruncationReport = TruncationReport.Empty;
          UpdateReactiveProperties();
          log.ForMethod().Information("Began local persistence session after gameplay discard; application save block: {0}", applicationSaveBlocked);
        }
        finally
        {
          loadGate.Release();
        }
      }
    }

    /// <summary>Registers required preparation for subsequent controller snapshots and saves.</summary>
    public void RegisterRequiredSavePreparationParticipant(IRequiredSavePreparationParticipant participant)
    {
      if (participant == null) throw new ArgumentNullException(nameof(participant));
      if (string.IsNullOrWhiteSpace(participant.ParticipantId))
        throw new ArgumentException("A stable preparation participant ID is required.", nameof(participant));
      lock (sessionBoundaryGate)
      {
        if (requiredSavePreparations.Contains(participant)) return;
        if (requiredSavePreparations.Any(current => string.Equals(current.ParticipantId, participant.ParticipantId, StringComparison.Ordinal)))
          throw new ArgumentException("A preparation participant with this ID is already registered.", nameof(participant));
        requiredSavePreparations.Add(participant);
      }
    }

    public void UnregisterRequiredSavePreparationParticipant(IRequiredSavePreparationParticipant participant)
    {
      lock (sessionBoundaryGate) requiredSavePreparations.Remove(participant);
    }

    public async UniTask<PreparedSaveCaptureScope> BeginPreparedProvisionalSaveCaptureAsync(CancellationToken ct = default)
    {
      ct.ThrowIfCancellationRequested();
      PersistenceOperation operation = BeginSaveOperation(true, out var participants);
      var context = new SavePreparationContext(sessionBoundaryGate);
      var entered = new List<IDisposable>();
      try
      {
        await PrepareForSaveAsync(context, participants, ct);
        ct.ThrowIfCancellationRequested();
        ValidatePreparedOperation(operation);
        SaveDomainControllerRegistry registry = operation.WasLocal ? localRegistry : globalRegistry;
        foreach (IProvisionalSaveCaptureParticipant participant in registry.GetControllers().OfType<IProvisionalSaveCaptureParticipant>())
          entered.Add(participant.BeginProvisionalSaveCapture()
            ?? throw new InvalidOperationException("A provisional save participant returned no scope."));
        return new PreparedSaveCaptureScope(this, operation.SessionGeneration, operation, sessionBoundaryGate,
          () => ReleasePreparedCapture(operation, context, entered));
      }
      catch
      {
        try { DisposeProvisionalParticipants(entered, throwOnFailure: false); }
        finally
        {
          try { ClosePreparation(context, throwOnFailure: false); }
          finally { operation.Dispose(); }
        }
        throw;
      }
    }

    public async UniTask<ES3FlushReceipt> SavePreparedAsync(PreparedSaveCaptureScope scope, CancellationToken ct = default)
    {
      ct.ThrowIfCancellationRequested();
      if (scope == null) throw new ArgumentNullException(nameof(scope));
      PersistenceOperation operation;
      lock (sessionBoundaryGate)
      {
        if (!ReferenceEquals(scope.Owner, this) || scope.SessionGeneration != persistenceSessionGeneration)
          throw new InvalidOperationException("The prepared capture belongs to another coordinator or persistence session.");
        operation = scope.Operation as PersistenceOperation;
        if (!ReferenceEquals(requiredPreparationOwner, operation))
          throw new InvalidOperationException("The prepared capture no longer owns persistence admission.");
        if (IsSaveBlocked.Value) throw new InvalidOperationException("Save is blocked because the prior load reported severe truncation.");
        scope.BeginWrite();
      }
      try { return await SaveCoreAsync(operation, requireReceipt: true, ct); }
      finally { scope.EndWrite(); }
    }

    private PersistenceOperation BeginSaveOperation(bool preparedCapture, out IRequiredSavePreparationParticipant[] participants)
    {
      lock (sessionBoundaryGate)
      {
        participants = requiredSavePreparations.OrderBy(participant => participant.Order)
          .ThenBy(participant => participant.ParticipantId, StringComparer.Ordinal).ToArray();
        bool exclusive = preparedCapture || participants.Length != 0;
        if (exclusive && activePersistenceOperations != 0)
          throw new InvalidOperationException("Busy: persistence is active or queued.");
        PersistenceOperation operation = BeginPersistenceOperation();
        if (exclusive) requiredPreparationOwner = operation;
        return operation;
      }
    }

    private void ValidatePreparedOperation(PersistenceOperation operation)
    {
      lock (sessionBoundaryGate)
      {
        if (!ReferenceEquals(requiredPreparationOwner, operation) || operation.SessionGeneration != persistenceSessionGeneration)
          throw new InvalidOperationException("The persistence session changed during required save preparation.");
      }
    }

    private static async UniTask PrepareForSaveAsync(SavePreparationContext context,
      IReadOnlyList<IRequiredSavePreparationParticipant> participants, CancellationToken ct)
    {
      if (participants.Count == 0) return;
      using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
      deadline.CancelAfter(TimeSpan.FromSeconds(2));
      Task aborted = Task.Delay(Timeout.Infinite, deadline.Token);
      foreach (IRequiredSavePreparationParticipant participant in participants)
      {
        ThrowIfPreparationStopped(deadline, ct);
        Task pending = participant.PrepareForSaveAsync(context, deadline.Token).AsTask();
        if (await Task.WhenAny(pending, aborted) != pending)
        {
          // Observe an uncooperative participant's eventual failure without resuming preparation.
          _ = pending.ContinueWith(completed => { _ = completed.Exception; }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
          ThrowIfPreparationStopped(deadline, ct);
        }
        try { await pending; }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        { ThrowIfPreparationStopped(deadline, ct); throw; }
        ThrowIfPreparationStopped(deadline, ct);
      }
    }

    private static void ThrowIfPreparationStopped(CancellationTokenSource deadline, CancellationToken ct)
    {
      ct.ThrowIfCancellationRequested();
      if (deadline.IsCancellationRequested) throw new TimeoutException("Timeout: required save preparation exceeded its two-second wall-clock deadline.");
    }

    private static void ClosePreparation(SavePreparationContext context, bool throwOnFailure)
    {
      try { context?.Close(); }
      catch (Exception failure)
      {
        log.ForMethod().Error(failure, "Save preparation admission cleanup failed: {Message}", failure.Message);
        if (throwOnFailure) throw;
      }
    }

    private static void ReleasePreparedCapture(PersistenceOperation operation, SavePreparationContext context, IReadOnlyList<IDisposable> entered)
    {
      try { DisposeProvisionalParticipants(entered, throwOnFailure: true); }
      finally
      {
        try { ClosePreparation(context, throwOnFailure: true); }
        finally { operation.Dispose(); }
      }
    }

    /// <summary>
    /// Pins active controllers' live save bindings until the caller restores its source profile.
    /// The scope prevents session discard but permits the nested save used to produce the capture.
    /// </summary>
    public IDisposable BeginProvisionalSaveCapture()
    {
      PersistenceOperation operation;
      lock (sessionBoundaryGate)
      {
        if (requiredSavePreparations.Count != 0)
          throw new InvalidOperationException("PreparationRequired: use asynchronous prepared provisional capture.");
        if (activePersistenceOperations != 0)
          throw new InvalidOperationException("Cannot begin provisional capture while persistence is active or queued.");
        operation = BeginPersistenceOperation();
      }
      var entered = new List<IDisposable>();
      try
      {
        SaveDomainControllerRegistry registry = operation.WasLocal ? localRegistry : globalRegistry;
        IProvisionalSaveCaptureParticipant[] participants = registry.GetControllers()
          .OfType<IProvisionalSaveCaptureParticipant>().ToArray();
        foreach (IProvisionalSaveCaptureParticipant participant in participants)
          entered.Add(participant.BeginProvisionalSaveCapture()
            ?? throw new InvalidOperationException("A provisional save participant returned no scope."));
        return new ProvisionalSaveCaptureScope(operation, entered);
      }
      catch
      {
        try { DisposeProvisionalParticipants(entered, throwOnFailure: false); }
        finally { operation.Dispose(); }
        throw;
      }
    }

    private static void DisposeProvisionalParticipants(IReadOnlyList<IDisposable> entered, bool throwOnFailure)
    {
      List<Exception> failures = null;
      for (int i = entered.Count - 1; i >= 0; i--)
      {
        try { entered[i].Dispose(); }
        catch (Exception error)
        {
          (failures ??= new List<Exception>()).Add(error);
          log.ForMethod().Error(error, "Provisional save participant cleanup failed: {Message}", error.Message);
        }
      }
      if (throwOnFailure && failures != null)
        throw new AggregateException("Provisional save participant cleanup failed.", failures);
    }

    private sealed class ProvisionalSaveCaptureScope : IDisposable
    {
      private PersistenceOperation operation;
      private readonly IReadOnlyList<IDisposable> entered;

      public ProvisionalSaveCaptureScope(PersistenceOperation operation, IReadOnlyList<IDisposable> entered)
      {
        this.operation = operation;
        this.entered = entered;
      }

      public void Dispose()
      {
        PersistenceOperation current = Interlocked.Exchange(ref operation, null);
        if (current == null) return;
        try { DisposeProvisionalParticipants(entered, throwOnFailure: true); }
        finally { current.Dispose(); }
      }
    }

    private PersistenceOperation BeginPersistenceOperation()
    {
      lock (sessionBoundaryGate)
      {
        if (requiredPreparationOwner != null)
          throw new InvalidOperationException("Busy: a required save preparation or prepared capture owns persistence admission.");
        bool wasLocal = useLocalSystem;
        ES3GameSaveSystem system = wasLocal ? localSaveSystem : globalSaveSystem;
        if (system == null)
          throw new InvalidOperationException("No active save system available");
        var operation = new PersistenceOperation(this, system, wasLocal);
        activePersistenceOperations++;
        return operation;
      }
    }

    private sealed class PersistenceOperation : IDisposable
    {
      private SaveSystemCoordinator owner;
      public ES3GameSaveSystem System { get; }
      public bool WasLocal { get; }
      public long SessionGeneration { get; }

      public PersistenceOperation(SaveSystemCoordinator owner, ES3GameSaveSystem system, bool wasLocal)
      {
        this.owner = owner;
        System = system;
        WasLocal = wasLocal;
        SessionGeneration = owner.persistenceSessionGeneration;
      }

      public void Dispose()
      {
        SaveSystemCoordinator current = Interlocked.Exchange(ref owner, null);
        if (current == null) return;
        lock (current.sessionBoundaryGate)
        {
          current.activePersistenceOperations--;
          if (ReferenceEquals(current.requiredPreparationOwner, this)) current.requiredPreparationOwner = null;
        }
      }
    }

    private void BlockSavesForSevereTruncation()
    {
      applicationSaveBlocked = true;
      IsSaveBlocked.Value = true;
      StopAutoSave();
    }

    /// <summary>
    ///   True while a session restore is in progress, spanning a WIDER window than
    ///   <see cref="IsLoading"/>: opened by the startup orchestrator the moment it commits to the
    ///   LOAD path — BEFORE world generation streams its initial chunks — and closed after
    ///   <see cref="LoadAsync"/> completes. <see cref="IsLoading"/> only covers the
    ///   <see cref="LoadAsync"/> call itself, which runs AFTER world-gen, so content generators
    ///   that react to chunk streaming (e.g. surface scatter) must gate on THIS flag to avoid
    ///   replaying generation on top of restored save data. Driven externally by the orchestrator;
    ///   this coordinator only holds it.
    /// </summary>
    public ReactiveProperty<bool> RestoreInProgress { get; }


    /// <summary>
    ///   Snapshot of truncation entries reported during the most recent successful
    ///   <see cref="LoadAsync"/>. Set to <see cref="TruncationReport.Empty"/> when the last
    ///   load was clean, threw, or has not yet run. Pull-mechanism for callers that cannot
    ///   subscribe to <see cref="SaveTruncatedOnLoadMessage"/> through
    ///   <c>GameMessageBroker</c> (e.g., menu-scene global loads where the game-scope broker
    ///   may not be available, or post-load hydrate handlers reading state directly).
    /// </summary>
    public TruncationReport LastLoadTruncationReport { get; private set; } = TruncationReport.Empty;

    /// <summary>Monotonically increasing counter incremented on each <see cref="LoadAsync"/> attempt.</summary>
    public int LoadGeneration { get; private set; }

    /// <summary>Controller type names that failed during the last <see cref="LoadAsync"/>. Empty on success or before first load.</summary>
    public IReadOnlyList<string> LastFailedControllers =>
        (useLocalSystem ? localSaveSystem : globalSaveSystem)?.FailedControllers
        ?? Array.Empty<string>();

    /// <summary>
    ///   Fires after a successful <see cref="LoadAsync"/> completes, allowing subscribers
    ///   to perform post-load cross-controller hydration (e.g. resolve references that
    ///   span multiple SaveData domains). Handlers are awaited sequentially; per-handler
    ///   failures are logged and isolated so one bad hydrator does not break others.
    /// </summary>
    public event Func<CancellationToken, UniTask> PostLoadHydrate;

    #region IRuntimeSystem Members

    public void Start()
    {
      lock (sessionBoundaryGate) persistenceSessionGeneration++;
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
      lock (sessionBoundaryGate) persistenceSessionGeneration++;
      log.ForMethod().Debug("SaveSystemCoordinator shutting down");

      // Stop auto-save
      StopAutoSave();

      // Dispose auto-save cancellation token source
      autoSaveCts?.Dispose();
      autoSaveCts = null;

      // Clean up reactive properties
      IsSaving?.Dispose();
      IsLoading?.Dispose();
      IsSaveBlocked?.Dispose();
      RestoreInProgress?.Dispose();
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
        lock (sessionBoundaryGate) persistenceSessionGeneration++;
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
        lock (sessionBoundaryGate) persistenceSessionGeneration++;
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
        ///   Unregisters a domain controller from the local registry. Called by plugins on
        ///   Shutdown so controllers holding scene-scoped Unity references (cameras, services,
        ///   etc.) don't outlive the scene that owns them and break subsequent save/load cycles.
        /// </summary>
        public void UnregisterLocalController(ISaveDomainController controller)
        {
          if (controller == null)
          {
            log.ForMethod().Warning("Attempted to unregister null controller from local registry - ignoring");
            return;
          }

          if (!localRegistry.UnregisterController(controller))
            return;

          if (useLocalSystem)
          {
            List<ISaveDomainController> localControllers = localRegistry.GetControllers().ToList();
            localSaveSystem = new ES3GameSaveSystem(localControllers, es3Service);
            UpdateReactiveProperties();
            log.ForMethod().Information("Recreated local save system with {0} controllers after unregistering: {1}",
              localControllers.Count, controller.Domain);
          }

          log.ForMethod().Information("Unregistered domain controller from local registry: {0} (total local controllers: {1})",
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
      ct.ThrowIfCancellationRequested();

      // Shutdown callers use the existing skip-and-warn contract for blocked saves.
      if (IsSaveBlocked.Value)
      {
        log.ForMethod().Warning("Save blocked: prior load reported severe truncation; quit and restart the game to use a different save");
        return;
      }

      using PersistenceOperation operation = BeginSaveOperation(false, out var participants);
      var context = new SavePreparationContext(sessionBoundaryGate);
      bool succeeded = false;
      try
      {
        await PrepareForSaveAsync(context, participants, ct);
        if (participants.Length != 0) ValidatePreparedOperation(operation);
        await SaveCoreAsync(operation, operation.System.SupportsDurableReceipts, ct);
        succeeded = true;
      }
      finally { ClosePreparation(context, throwOnFailure: succeeded); }
    }

    /// <summary>Returns the completed durable flush receipt; blocked saves cannot produce a receipt.</summary>
    public async UniTask<ES3FlushReceipt> SaveWithReceiptAsync(CancellationToken ct = default)
    {
      ct.ThrowIfCancellationRequested();
      if (IsSaveBlocked.Value)
        throw new InvalidOperationException("Save is blocked because the prior load reported severe truncation.");

      using PersistenceOperation operation = BeginSaveOperation(false, out var participants);
      var context = new SavePreparationContext(sessionBoundaryGate);
      bool succeeded = false;
      try
      {
        await PrepareForSaveAsync(context, participants, ct);
        if (participants.Length != 0) ValidatePreparedOperation(operation);
        ES3FlushReceipt receipt = await SaveCoreAsync(operation, requireReceipt: true, ct);
        succeeded = true;
        return receipt;
      }
      finally { ClosePreparation(context, throwOnFailure: succeeded); }
    }

    private async UniTask<ES3FlushReceipt> SaveCoreAsync(
      PersistenceOperation operation, bool requireReceipt, CancellationToken ct)
    {
      ES3GameSaveSystem activeSystem = operation.System;
      SaveDomainControllerRegistry activeRegistry = operation.WasLocal ? localRegistry : globalRegistry;
      List<ISaveDomainController> controllers = activeRegistry.GetControllers().ToList();
      log.ForMethod().Verbose("Saving using {0} system with {1} controllers: {2}",
        operation.WasLocal ? "local" : "global", controllers.Count,
        string.Join(", ", controllers.Select(c => c.Domain.ToString())));

      try
      {
        Stopwatch sw = Stopwatch.StartNew();
        ES3FlushReceipt receipt = null;
        if (requireReceipt) receipt = await activeSystem.SaveWithReceiptAsync(ct);
        else await activeSystem.SaveAsync(ct);
        sw.Stop();
        UpdateReactiveProperties();

        log.ForMethod().Information("Save timing total: system={0} wall_clock_ms={1} controller_count={2}",
          operation.WasLocal ? "local" : "global", sw.ElapsedMilliseconds, controllers.Count);
        if (receipt != null)
        {
          try { DurableSaveCompleted?.Invoke(receipt); }
          catch (Exception observerError)
          {
            log.ForMethod().Error(observerError, "Durable save receipt observer failed: {Message}", observerError.Message);
          }
        }
        return receipt;
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Save failed using {0} system: {Message}",
          operation.WasLocal ? "local" : "global", ex.Message);
        throw;
      }
    }

    public async UniTask LoadAsync(CancellationToken ct = default)
    {
      // Check cancellation token before proceeding
      ct.ThrowIfCancellationRequested();

      using PersistenceOperation operation = BeginPersistenceOperation();
      ES3GameSaveSystem activeSystem = operation.System;

      LoadGeneration++;
      log.ForMethod().Information("Loading using {0} system (generation {1})", operation.WasLocal ? "local" : "global", LoadGeneration);

      // Validate first; only then take the load gate and touch the truncation reporter.
      // The gate serializes overlapping LoadAsync calls so the reporter's load scope is not
      // corrupted by a second caller swapping its queue mid-load.
      await loadGate.WaitAsync(ct);
      try
      {
        truncationReporter.BeginLoadScope();
        bool loadSucceeded = false;
        TruncationReport drained = TruncationReport.Empty;
        try
        {
          await activeSystem.LoadAsync(ct);
          UpdateReactiveProperties();
          loadSucceeded = true;

          log.ForMethod().Information("Load completed successfully using {0} system", operation.WasLocal ? "local" : "global");

          // Phase E.2 Wave 3: hydrate runs INSIDE the load scope so cross-reference rot
          // reported by handlers merges into the single drained report. OperationCanceledException
          // propagates (cancellation = load failure); other exceptions are isolated per-handler
          // inside InvokePostLoadHydrateAsync so one bad hydrator does not block others.
          await InvokePostLoadHydrateAsync(ct);
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Load failed using {0} system: {ExType}: {ExFull}", operation.WasLocal ? "local" : "global", ex.GetType().Name, ex.ToString());
          throw;
        }
        finally
        {
          // Always drain so leaked entries don't poison the next load. Publish only on success.
          drained = truncationReporter.DrainReport();
          LastLoadTruncationReport = loadSucceeded ? drained : TruncationReport.Empty;
        }

        if (drained.HasEntries)
        {
          // Phase F (F5): set the sticky save block BEFORE publishing so any subscriber that reads
          // IsSaveBlocked sees the consistent post-block state. One-way ratchet: no path inside this
          // coordinator clears a truncation block. Recovery requires app exit (F1).
          if (drained.Severity == SaveTruncatedOnLoadMessage.Severity.BlockOverwrite)
            BlockSavesForSevereTruncation();
          GameMessageBroker.Publish(new SaveTruncatedOnLoadMessage(drained.Severity, drained.Entries));
          log.ForMethod().Warning("Save loaded with {0} truncation entries; published SaveTruncatedOnLoadMessage at severity {1}", drained.Entries.Count, drained.Severity);
        }
      }
      finally
      {
        loadGate.Release();
      }
    }

    /// <summary>
    /// Executes a declared controller transaction rather than registry insertion order.  A
    /// required-controller failure is returned after execution stops; optional failures are
    /// returned after the remaining independent steps finish.  Post-load hydration is invoked
    /// only after a transaction without required failures.
    /// </summary>
    public async UniTask<IReadOnlyList<string>> LoadAsync(
      IReadOnlyList<string> orderedControllerTypeNames,
      Func<string, bool> isRequired,
      CancellationToken ct = default)
    {
      if (orderedControllerTypeNames == null) throw new ArgumentNullException(nameof(orderedControllerTypeNames));
      if (isRequired == null) throw new ArgumentNullException(nameof(isRequired));
      ct.ThrowIfCancellationRequested();

      using PersistenceOperation operation = BeginPersistenceOperation();
      ES3GameSaveSystem activeSystem = operation.System;

      LoadGeneration++;
      await loadGate.WaitAsync(ct);
      try
      {
        truncationReporter.BeginLoadScope();
        bool loadSucceeded = false;
        TruncationReport drained = TruncationReport.Empty;
        IReadOnlyList<string> failures;
        try
        {
          failures = await activeSystem.LoadAsync(orderedControllerTypeNames, isRequired, ct);
          if (failures.Any(isRequired))
          {
            BlockSavesForFailedLoad(operation.WasLocal);
            LastLoadTruncationReport = TruncationReport.Empty;
            return failures;
          }

          UpdateReactiveProperties();
          await InvokePostLoadHydrateAsync(ct);
          loadSucceeded = true;
          return failures;
        }
        finally
        {
          drained = truncationReporter.DrainReport();
          LastLoadTruncationReport = loadSucceeded ? drained : TruncationReport.Empty;
          if (loadSucceeded && drained.HasEntries)
          {
            if (drained.Severity == SaveTruncatedOnLoadMessage.Severity.BlockOverwrite)
              BlockSavesForSevereTruncation();
            GameMessageBroker.Publish(new SaveTruncatedOnLoadMessage(drained.Severity, drained.Entries));
          }
        }
      }
      finally
      {
        loadGate.Release();
      }
    }

    private async UniTask InvokePostLoadHydrateAsync(CancellationToken ct)
    {
      Func<CancellationToken, UniTask> handlers = PostLoadHydrate;
      if (handlers == null)
        return;

      foreach (Delegate del in handlers.GetInvocationList())
      {
        ct.ThrowIfCancellationRequested();
        Func<CancellationToken, UniTask> handler = (Func<CancellationToken, UniTask>)del;
        try
        {
          await handler(ct);
        }
        catch (OperationCanceledException)
        {
          throw;
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "PostLoadHydrate handler failed: {Message}", ex.Message);
        }
      }
    }

        /// <summary>When true, the auto-save loop and scene-change auto-save are suspended (dev/test hook for save-migration smokes that stamp the on-disk save and reload it). Not persisted; runtime-only.</summary>
        private bool autoSaveSuspended;

        /// <summary>
        ///   Suspends or resumes auto-save without mutating <c>saveConfig.EnableAutoSave</c>. A save-migration
        ///   smoke uses this to stamp a controller's on-disk schema version and reload it without the auto-save
        ///   loop (or a scene-change auto-save) re-stamping over it back to current via <c>PrepareForSave</c>.
        /// </summary>
        /// <param name="suspended">True to suspend auto-save; false to resume (when <c>EnableAutoSave</c>).</param>
        public void SetAutoSaveSuspended(bool suspended)
        {
            autoSaveSuspended = suspended;
            if (suspended) StopAutoSave();
            else if (saveConfig.EnableAutoSave && !IsAutoSaveRunning.Value) StartAutoSave();
            log.ForMethod().Information("Auto-save {0}", suspended ? "SUSPENDED (dev/test hook)" : "resumed");
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

          // Skip auto-save if suspended (dev/test hook), already saving or loading, or the save block is set (F2).
          if (autoSaveSuspended || IsSaving.Value || IsLoading.Value || IsSaveBlocked.Value)
          {
            log.ForMethod().Verbose("Skipping auto-save - save/load in progress or save blocked");
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
      if (autoSaveSuspended)
      {
        log.ForMethod().Debug("Auto-save suspended (dev/test hook), skipping manual trigger");
        return;
      }
      if (!saveConfig.EnableAutoSave)
      {
        log.ForMethod().Debug("Auto-save is disabled, skipping manual trigger");
        return;
      }

      // Phase F (F2): respect the sticky save block.
      if (IsSaveBlocked.Value)
      {
        log.ForMethod().Debug("Skipping manual auto-save - save blocked");
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

      // Phase F (F2): respect the sticky save block. Covers ApplicationQuitSaveHandler too —
      // it calls HandleSceneChangeAsync on OS quit, so guarding here transitively protects that path.
      if (IsSaveBlocked.Value)
      {
        log.ForMethod().Debug("Skipping scene change auto-save - save blocked");
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
