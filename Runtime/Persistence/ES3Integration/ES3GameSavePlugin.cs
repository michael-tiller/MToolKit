using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Persistence.Interfaces;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
  /// <summary>
  ///   ES3-based game save plugin that manages save system configuration and auto-discovery.
  ///   Uses ConfigPlugin pattern for consistent service management and configuration.
  ///   Implements IAsyncStartable for VContainer's UniTask integration.
  /// </summary>
  public class ES3GameSavePlugin : ConfigPlugin<ES3GameSaveSystem, IES3GameSaveSystem, ES3SaveConfig>, IAsyncStartable
  {
    [SerializeField]
    [Required]
    private GameObject autoSaveExecutingAnimatedObject;

    private readonly List<IDisposable> disposables = new();

    [Inject]
    private IES3Service es3Service;

    [Inject]
    private SaveSystemCoordinator saveSystemCoordinator;

    [ShowInInspector]
    [ReadOnly]
    private ES3GameSaveSystem Service => GetService();

    /// <summary>
    ///   Required services for dependency validation.
    /// </summary>
    public override IEnumerable<Type> RequiredServices => new[]
{
      typeof(SaveSystemCoordinator),
      typeof(IES3Service)
    };

    /// <summary>
    ///   Optional services for dependency validation.
    /// </summary>
    public override IEnumerable<Type> OptionalServices => Array.Empty<Type>();

    /// <summary>
    ///   Async startup method for VContainer's UniTask integration.
    ///   Replaces PerformRuntimeInitialization with proper async/await support.
    /// </summary>
    public async UniTask StartAsync(CancellationToken cancellation)
    {
      if (isRuntimeInitialized)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose(
          "ES3GameSavePlugin already runtime initialized, skipping");
        return;
      }

      log.ForGameObject(gameObject).ForMethod().Debug(
        "Starting ES3GameSavePlugin async initialization...");

      try
      {
        // Get the service instance (created by ConfigPlugin)
        // Since we're in StartAsync, we can use the injected fields
        service = GetService();

        if (service == null)
        {
          log.ForGameObject(gameObject).ForMethod().Error(
            "Failed to resolve ES3GameSaveSystem");
          return;
        }

        // Use the injected SaveSystemCoordinator
        SaveSystemCoordinator resolvedSaveSystemCoordinator = saveSystemCoordinator;

        log.ForGameObject(gameObject).ForMethod().Information(
          "Setting up subscription to IsAutoSaveExecuting. Current value: {CurrentValue}",
          resolvedSaveSystemCoordinator.IsAutoSaveExecuting.Value);

        // Subscribe to auto-save execution state changes
        resolvedSaveSystemCoordinator.IsAutoSaveExecuting.Subscribe(isAutoSaveExecuting =>
        {
          log.ForGameObject(gameObject).ForMethod().Information(
            "Auto-save execution state changed: {IsExecuting}", isAutoSaveExecuting);

          // Only set active if the animated object is assigned
          if (autoSaveExecutingAnimatedObject != null)
          {
            autoSaveExecutingAnimatedObject.SetActive(isAutoSaveExecuting);
            log.ForGameObject(gameObject).ForMethod().Debug(
              "Updated autoSaveExecutingAnimatedObject visibility to: {IsActive}", isAutoSaveExecuting);
          }
          else
          {
            log.ForGameObject(gameObject).ForMethod().Warning(
              "autoSaveExecutingAnimatedObject is not assigned, cannot update visibility");
          }
        }).AddTo(disposables);

        // Trigger initial subscription callback to ensure UI is in sync with current state
        log.ForGameObject(gameObject).ForMethod().Information(
          "Triggering initial subscription callback with current value: {CurrentValue}",
          resolvedSaveSystemCoordinator.IsAutoSaveExecuting.Value);

        // Manually trigger the subscription callback with the current value
        bool currentValue = resolvedSaveSystemCoordinator.IsAutoSaveExecuting.Value;
        if (autoSaveExecutingAnimatedObject != null)
        {
          autoSaveExecutingAnimatedObject.SetActive(currentValue);
          log.ForGameObject(gameObject).ForMethod().Debug(
            "Set initial autoSaveExecutingAnimatedObject visibility to: {IsActive}", currentValue);
        }

        // Perform any async initialization if needed
        await PerformAsyncInitialization(cancellation);

        isRuntimeInitialized = true;
        log.ForGameObject(gameObject).ForMethod().Debug(
          "ES3GameSavePlugin async initialization completed");
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex,
          "Error during ES3GameSavePlugin async initialization: {Message}", ex.Message);
        throw;
      }
    }

    /// <summary>
    ///   Create the ES3GameSaveSystem service with configuration.
    /// </summary>
    protected override ES3GameSaveSystem CreateService(IObjectResolver resolver)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Creating ES3GameSaveSystem with config");

      // Resolve IES3Service from the resolver instead of using injected field
      // The injected field is only available after service creation, not during creation
      IES3Service resolvedEs3Service = resolver.Resolve<IES3Service>();

      // Domain controllers will be set during runtime initialization
      return new ES3GameSaveSystem(config, resolvedEs3Service);
    }

    /// <summary>
    ///   Performs any additional async initialization specific to ES3GameSavePlugin.
    ///   Override this method to add custom async initialization logic.
    /// </summary>
    protected virtual async UniTask PerformAsyncInitialization(CancellationToken cancellation)
    {
      // Default implementation - no additional async work needed
      await UniTask.CompletedTask;
    }

    public override void Shutdown()
    {
      disposables.ForEach(d => d.Dispose());
      disposables.Clear();
      base.Shutdown();
    }

    /// <summary>
    ///   Gets the service, ensuring it's properly resolved for this instance.
    ///   This ensures the Inspector shows the correct service even if this instance
    ///   wasn't properly initialized during the normal flow.
    /// </summary>
    private ES3GameSaveSystem GetService()
    {
      // If this instance has a service, return it
      if (service != null)
        return service;

      // Try to resolve the service from the container if this instance doesn't have one
      // This can happen if the scene instance wasn't properly initialized
      try
      {
        // Find the GlobalInstaller to get access to the container
        GlobalInstaller globalInstaller = FindFirstObjectByType<GlobalInstaller>();
        if (globalInstaller != null)
        {
          // Try to resolve the service from the container
          IObjectResolver resolver = globalInstaller.Container.Resolve<IObjectResolver>();
          ES3GameSaveSystem resolvedService = resolver.Resolve<IES3GameSaveSystem>() as ES3GameSaveSystem;
          if (resolvedService != null)
          {
            // Cache it for future use
            service = resolvedService;
            return resolvedService;
          }
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Warning(ex, "Failed to resolve ES3GameSaveSystem from container: {Message}", ex.Message);
      }

      // Fallback to null if we can't resolve
      return null;
    }
  }
}