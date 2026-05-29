using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Persistence.Enums;
using MToolKit.Runtime.Persistence.Interfaces;
using Serilog;
using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   ES3-based domain controller that manages save/load for a specific domain
    /// </summary>
    public class ES3DomainController : ISaveDomainController
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ES3DomainController>().ForFeature("Persistence.ES3"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    private readonly string domainPrefix;
    private readonly IES3Service es3Service;

    private readonly IEnumerable<ISaveable> saveables;

    public ES3DomainController(
      ESaveDomain domain,
      IEnumerable<ISaveable> saveables,
      IES3Service es3Service)
    {
      Domain = domain;
      this.saveables = saveables ?? throw new ArgumentNullException(nameof(saveables));
      this.es3Service = es3Service ?? throw new ArgumentNullException(nameof(es3Service));
      domainPrefix = $"{domain.ToString().ToLower()}_";

      log.ForMethod().Debug("ES3DomainController created for domain: {0}", domain);
    }

    #region ISaveDomainController Members

    public ESaveDomain Domain { get; }

    public bool HasSaveData()
    {
      foreach (ISaveable saveable in saveables)
      {
        var key = $"{domainPrefix}{saveable.Key}";
        if (es3Service.KeyExists(key))
          return true;
      }
      return false;
    }

    public async UniTask SaveAsync(CancellationToken ct = default)
    {
      log.ForMethod().Verbose("Saving domain: {0}", Domain);

      if (ct.IsCancellationRequested)
      {
        log.ForMethod().Debug("Cancellation requested before save of domain: {0}", Domain);
        return;
      }

      foreach (ISaveable saveable in saveables)
      {
        try
        {
          string key = $"{domainPrefix}{saveable.Key}";
          object data = await saveable.SaveAsync();
          await es3Service.SaveAsync(key, data, ct);

          log.ForMethod().Verbose("Saved saveable '{0}' with key '{1}'", saveable.Key, key);
        }
        catch (OperationCanceledException)
        {
          log.ForMethod().Debug("Save operation cancelled for saveable '{0}'", saveable.Key);
          throw; // Re-throw to propagate cancellation
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Failed to save saveable '{0}': {Message}", saveable.Key, ex.Message);
          throw;
        }

        if (ct.IsCancellationRequested)
        {
          log.ForMethod().Debug("Cancellation requested during save of domain: {0}", Domain);
          break;
        }
      }

      log.ForMethod().Debug("Domain {0} save completed", Domain);
    }

    public async UniTask LoadAsync(CancellationToken ct = default)
    {
      log.ForMethod().Verbose("Loading domain: {0}", Domain);

      if (ct.IsCancellationRequested)
      {
        log.ForMethod().Debug("Cancellation requested before load of domain: {0}", Domain);
        return;
      }

      foreach (ISaveable saveable in saveables)
      {
        try
        {
          string key = $"{domainPrefix}{saveable.Key}";

          log.ForMethod().Information("Attempting to load saveable '{0}' with key '{1}'", saveable.Key, key);

          if (es3Service.KeyExists(key))
          {
            log.ForMethod().Information("Key '{0}' exists, loading data", key);
            object data = await es3Service.LoadAsync<object>(key, null, ct);
            log.ForMethod().Information("Loaded data for key '{0}': {1}", key, data != null ? data.GetType().Name : "null");

            if (data != null)
            {
              await saveable.LoadAsync(data);
              log.ForMethod().Information("Successfully loaded saveable '{0}' with key '{1}'", saveable.Key, key);
            }
            else
            {
              log.ForMethod().Warning("Data is null for key '{0}'", key);
            }
          }
          else
          {
            log.ForMethod().Information("No save data found for saveable '{0}' with key '{1}'", saveable.Key, key);
          }
        }
        catch (Exception ex)
        {
          log.ForMethod().Error(ex, "Failed to load saveable '{0}': {Message}", saveable.Key, ex.Message);
          throw;
        }

        if (ct.IsCancellationRequested)
        {
          log.ForMethod().Debug("Cancellation requested during load of domain: {0}", Domain);
          break;
        }
      }

      log.ForMethod().Debug("Domain {0} load completed", Domain);
    }

    #endregion
  }
}