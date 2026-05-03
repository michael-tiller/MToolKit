using System;
using System.Collections.Generic;
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
          log.ForMethod().Debug("Starting ES3 save operation with {0} domain controllers", domainControllers.Count());

          // Save all domain controllers in parallel
          await UniTask.WhenAll(domainControllers.Select(d => d.SaveAsync(ct)));
        }
        else
        {
          log.ForMethod().Debug("Starting ES3 save operation with config-based setup");
        }

        // Update the main save timestamp
        await es3Service.SaveAsync();

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
        log.ForMethod().Error(ex, "ES3 load operation failed: {Message}", ex.Message);
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
  }
}