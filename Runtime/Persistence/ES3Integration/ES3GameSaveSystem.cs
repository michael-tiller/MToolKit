using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Persistence.Interfaces;
using R3;
using Serilog;
using Serilog.Core;
using Sirenix.OdinInspector;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   ES3-based game save system that maintains compatibility with existing GameSaveSystem interface
    /// </summary>
    [Serializable]
  public class ES3GameSaveSystem : IRuntimeSystem, IES3GameSaveSystem
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ES3GameSaveSystem>().ForFeature("Persistence.ES3"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    // Per-controller save timing above this threshold (ms) escalates to Warning.
    // Tuned conservatively; can be lowered as the perf budget tightens.
    private const long SlowControllerThresholdMs = 100;

    [ShowInInspector]
    [ReadOnly]
    private readonly ES3SaveConfig config;

    private readonly IES3Service es3Service;

    [ShowInInspector]
    [ReadOnly]
    private IEnumerable<ISaveDomainController> domainControllers;

    public ES3GameSaveSystem(ES3SaveConfig config, IES3Service es3Service)
    {
      this.config = config ?? throw new ArgumentNullException(nameof(config));
      this.es3Service = es3Service ?? throw new ArgumentNullException(nameof(es3Service));

      // Delegate reactive properties to the ES3 service
      IsSaving = es3Service.IsSaving;
      IsLoading = es3Service.IsLoading;
      LastSaveTime = es3Service.LastSaveTime;
      LastLoadTime = es3Service.LastLoadTime;
      SaveCounter = es3Service.SaveCounter;

      log.ForMethod().Verbose("ES3GameSaveSystem created with config: {0}", config.name);
    }

        /// <summary>
        ///   Constructor for backward compatibility with existing code that passes domain controllers.
        /// </summary>
        public ES3GameSaveSystem(IEnumerable<ISaveDomainController> domainControllers, IES3Service es3Service)
    {
      this.domainControllers = domainControllers ?? throw new ArgumentNullException(nameof(domainControllers));
      this.es3Service = es3Service ?? throw new ArgumentNullException(nameof(es3Service));

      // Delegate reactive properties to the ES3 service
      IsSaving = es3Service.IsSaving;
      IsLoading = es3Service.IsLoading;
      LastSaveTime = es3Service.LastSaveTime;
      LastLoadTime = es3Service.LastLoadTime;
      SaveCounter = es3Service.SaveCounter;

          log.ForMethod().Verbose("ES3GameSaveSystem created with {0} domain controllers", domainControllers.Count());
    }

    [ShowInInspector]
    [ReadOnly]
    private ProfileAwareES3Service ProfileAwareES3Service => es3Service as ProfileAwareES3Service;

    public ReactiveProperty<bool> IsSaving { get; }
    public ReactiveProperty<bool> IsLoading { get; }
    public ReactiveProperty<string> LastSaveTime { get; }
    public ReactiveProperty<string> LastLoadTime { get; }
    public ReactiveProperty<int> SaveCounter { get; }

        /// <summary>
        ///   Saves all game data asynchronously using ES3
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>UniTask for the save operation</returns>
        public async UniTask SaveAsync(CancellationToken ct = default)
    {
      // Check cancellation token before proceeding
      ct.ThrowIfCancellationRequested();

      if (IsSaving.Value)
      {
        log.ForMethod().Warning("Save operation already in progress");
        return;
      }

      try
      {
        if (domainControllers != null)
        {
          List<ISaveDomainController> controllers = domainControllers.ToList();
          log.ForMethod().Information("Save iteration: domain_controller_count={0}", controllers.Count);

          // Per-controller timing instrumentation. UniTask.WhenAll runs cooperatively, so
          // individual Stopwatch readings are upper-bounds (include scheduler interleaving).
          // Use the total wall-clock at the SaveSystemCoordinator level for the user-observed cost.
          long[] elapsedMs = new long[controllers.Count];
          await UniTask.WhenAll(controllers.Select((d, i) => RunWithTimingAsync(d, i, elapsedMs, ct)));

          // Per-controller timing: Debug by default (quiet), Warning when above threshold.
          // Diagnostic confirmed Branch A on 2026-05-14: per-controller logs fire correctly via UniTask.WhenAll.
          // Demoted from Information → Debug to keep default-level output to 3 save-system lines per autosave.
          for (int i = 0; i < controllers.Count; i++)
          {
            if (elapsedMs[i] > SlowControllerThresholdMs)
              log.ForMethod().Warning("Save timing per controller (SLOW): domain={0} elapsed_ms={1} threshold_ms={2}", controllers[i].Domain, elapsedMs[i], SlowControllerThresholdMs);
            else
              log.ForMethod().Debug("Save timing per controller: domain={0} elapsed_ms={1}", controllers[i].Domain, elapsedMs[i]);
          }
        }
        else
        {
          log.ForMethod().Information("Save iteration: domainControllers is NULL (config-based setup path; per-controller iteration skipped)");
        }

        // Wrap the ES3 cache flush separately so we can attribute total wall-clock between
        // per-controller serialization and the file write. Information level by default.
        Stopwatch flushSw = Stopwatch.StartNew();
        await es3Service.SaveAsync();
        flushSw.Stop();
        log.ForMethod().Information("Save timing es3_flush: elapsed_ms={0}", flushSw.ElapsedMilliseconds);

        log.ForMethod().Information("ES3 save operation completed successfully");
      }
      catch (ObjectDisposedException ex) when (ex.ObjectName == "ES3SaveService")
      {
        log.ForMethod().Error(ex, "ES3SaveService was disposed during save operation - this should not happen with global services");
        throw new InvalidOperationException("Save service was disposed during save operation. This indicates a lifecycle management issue.", ex);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "ES3 save operation failed: {Message}", ex.Message);
        throw;
      }
    }

        /// <summary>
        ///   Loads all game data asynchronously using ES3
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>UniTask for the load operation</returns>
        public async UniTask LoadAsync(CancellationToken ct = default)
    {
      // Check cancellation token before proceeding
      ct.ThrowIfCancellationRequested();

      if (IsLoading.Value)
      {
        log.ForMethod().Warning("Load operation already in progress");
        return;
      }

      try
      {
        log.ForMethod().Verbose("Starting ES3 load operation with {0} domain controllers", domainControllers.Count());

        // Load all domain controllers in parallel
        await UniTask.WhenAll(domainControllers.Select(d => d.LoadAsync(ct)));

        // Update the main load timestamp
        await es3Service.LoadAsync(ct);

        log.ForMethod().Verbose("ES3 load operation completed successfully");
      }
      catch (ObjectDisposedException ex) when (ex.ObjectName == "ES3SaveService")
      {
        log.ForMethod().Error(ex, "ES3SaveService was disposed during load operation - this should not happen with global services");
        throw new InvalidOperationException("Save service was disposed during load operation. This indicates a lifecycle management issue.", ex);
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "ES3 load operation failed: {ExType}: {ExFull}", ex.GetType().Name, ex.ToString());
        throw;
      }
    }

        /// <summary>
        ///   Gets the number of registered save domain controllers
        /// </summary>
        /// <returns>Number of controllers</returns>
        public int GetControllerCount()
    {
      return domainControllers.Count();
    }

    public void Start()
    {
      // Initialize system state - LastSaveTime is now initialized by ES3SaveService
      LastLoadTime.Value = "Never";

      log.ForMethod().Debug("ES3GameSaveSystem started");
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
      log.ForMethod().Debug("ES3GameSaveSystem shutting down");

      // Note: We don't dispose the reactive properties here as they're owned by the ES3Service
      // The ES3Service should handle its own disposal
    }

        /// <summary>
        ///   Set the domain controllers after auto-discovery.
        /// </summary>
        public void SetDomainControllers(IEnumerable<ISaveDomainController> controllers)
    {
      domainControllers = controllers;
      log.ForMethod().Debug("ES3GameSaveSystem domain controllers set: {0}", controllers?.Count() ?? 0);
    }

    private static async UniTask RunWithTimingAsync(ISaveDomainController controller, int index, long[] elapsedMs, CancellationToken ct)
    {
      Stopwatch sw = Stopwatch.StartNew();
      try
      {
        await controller.SaveAsync(ct);
      }
      finally
      {
        sw.Stop();
        elapsedMs[index] = sw.ElapsedMilliseconds;
      }
    }
  }
}