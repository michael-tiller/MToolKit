using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Audio.Config;
using MToolKit.Runtime.Audio.Interface;
using MToolKit.Runtime.Audio.Service;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.AssetLoader;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.AssetLoader.Interfaces;

namespace MToolKit.Runtime.Audio
{
  /// <summary>
  /// Audio Plugin that registers the AudioService globally with proper mixer integration and settings binding.
  /// Provides audio playback service with Unity Audio Mixer integration and reactive settings.
  /// Interface sounds need to persist across scenes, so this is registered globally.
  /// </summary>
  public sealed class AudioPlugin : AbstractGamePlugin, IDependencyDeclaration, IRuntimePlugin
  {
    [SerializeField, Required] private AudioConfig config;

    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioPlugin>().ForFeature("Audio"));
    private new static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    private IObjectResolver resolver;
    private CancellationTokenSource cts;
    private IAudioService audioService;
    private readonly SemaphoreSlim initializationSemaphore = new SemaphoreSlim(1, 1);

    public IEnumerable<Type> RequiredServices => new[] { typeof(ISettingsSystem) };
    public IEnumerable<Type> OptionalServices => Array.Empty<Type>();

    [ShowInInspector] [ReadOnly] private AudioService AudioService => audioService as AudioService;


    public override void Register(IContainerBuilder builder)
    {
      log.ForMethod(nameof(Register)).Debug("AudioPlugin Register called");
      
      // Register the config
      builder.RegisterInstance(config).As<AudioConfig>();

      DontDestroyOnLoad(gameObject);
      
      // Register the AudioService with factory pattern to handle dependencies
      builder.Register<IAudioService>(resolver => 
      {
        try
        {
          var audioConfig = resolver.Resolve<AudioConfig>();
          var settingsSystem = resolver.Resolve<ISettingsSystem>();
          var assetService = resolver.Resolve<IRuntimeAssetService>();
          
          // Create audio root transform
          var audioRoot = CreateAudioRoot();
          
          return new AudioService(audioRoot, audioConfig, settingsSystem, assetService);
        }
        catch (VContainerException ex)
        {
          log.ForMethod(nameof(Register)).Error(ex, "Failed to resolve dependencies for AudioService");
          throw;
        }
      }, Lifetime.Singleton);
      
      log.ForMethod(nameof(Register)).Information("AudioPlugin Register completed - IAudioService registered");
    }

    public void PerformSetup(IObjectResolver objectResolver)
    {
      log.ForMethod(nameof(PerformSetup)).Information("AudioPlugin PerformSetup called");
      resolver = objectResolver;
      cts = new CancellationTokenSource();
    }

    public void PerformRuntimeInitialization(IObjectResolver objectResolver)
    {
      log.ForMethod(nameof(PerformRuntimeInitialization)).Information("AudioPlugin PerformRuntimeInitialization called");
      
      // Use semaphore to ensure only one initialization happens
      bool acquired = false;
      try
      {
        // Try to acquire semaphore (with 100ms timeout to avoid deadlock)
        acquired = initializationSemaphore.Wait(100);
        if (!acquired)
        {
          log.ForMethod(nameof(PerformRuntimeInitialization)).Warning("Could not acquire initialization semaphore (timed out) - another initialization is in progress");
          return;
        }
        
        // Ensure cts is initialized
        if (cts == null)
        {
          cts = new CancellationTokenSource();
        }
        
        // Initialize immediately
        if (resolver == null)
        {
          log.ForMethod(nameof(PerformRuntimeInitialization)).Error("Resolver is NULL - cannot initialize");
          return;
        }
        
        log.ForMethod(nameof(PerformRuntimeInitialization)).Debug("Resolving IAudioService...");
        audioService = resolver.Resolve<IAudioService>();
        log.ForMethod(nameof(PerformRuntimeInitialization)).Information("Resolved IAudioService: {Type}", audioService?.GetType().Name ?? "NULL");
        
        var settingsSystem = resolver.Resolve<ISettingsSystem>();
        log.ForMethod(nameof(PerformRuntimeInitialization)).Information("Resolved ISettingsSystem: {Type}", settingsSystem?.GetType().Name ?? "NULL");
        
        if (audioService is AudioService service && settingsSystem != null)
        {
          log.ForMethod(nameof(PerformRuntimeInitialization)).Information("Calling InitializeAsync on AudioService");
          service.InitializeAsync(settingsSystem, cts.Token).Forget();
          log.ForMethod(nameof(PerformRuntimeInitialization)).Information("AudioService initialization started");
        }
        else
        {
          log.ForMethod(nameof(PerformRuntimeInitialization)).Warning("Could not initialize AudioService - service or settingsSystem is invalid");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod(nameof(PerformRuntimeInitialization)).Error(ex, "Failed to initialize Audio");
      }
      finally
      {
        if (acquired)
        {
          initializationSemaphore.Release();
        }
      }
    }
    
    public void Initialize(IObjectResolver resolver)
    {
      log.ForMethod(nameof(Initialize)).Information("AudioPlugin Initialize called");
      PerformSetup(resolver);
      if (AreDependenciesReady(resolver))
      {
        PerformRuntimeInitialization(resolver);
      }
      else
      {
        log.ForMethod(nameof(Initialize)).Warning("AudioPlugin dependencies not ready for runtime initialization");
      }
    }

    public bool AreDependenciesReady(IObjectResolver resolver)
    {
      // Check if all required services are available
      bool ready = resolver.TryResolve<ISettingsSystem>(out var settingsSystem);
      if (ready)
      {
        log.ForMethod(nameof(AreDependenciesReady)).Information("AudioPlugin dependencies are ready - ISettingsSystem found");
      }
      else
      {
        log.ForMethod(nameof(AreDependenciesReady)).Warning("AudioPlugin dependencies not ready - ISettingsSystem not found");
      }
      return ready;
    }

    private async UniTaskVoid InitializeAsync(CancellationToken ct)
    {
      log.ForMethod(nameof(InitializeAsync)).Information("AudioPlugin InitializeAsync started");
      
      if (resolver == null)
      {
        log.ForMethod(nameof(InitializeAsync)).Error("Resolver is NULL in InitializeAsync - skipping initialization");
        return;
      }
      
      // Wait a frame to ensure everything is ready
      await UniTask.Yield(ct);
      
      try
      {
        log.ForMethod(nameof(InitializeAsync)).Information("Resolving IAudioService...");
        audioService = resolver.Resolve<IAudioService>();
        log.ForMethod(nameof(InitializeAsync)).Information("Resolved IAudioService: {Type}", audioService?.GetType().Name ?? "NULL");
        
        if (audioService == null)
        {
          log.ForMethod(nameof(InitializeAsync)).Error("audioService is NULL after resolve");
          return;
        }
        
        log.ForMethod(nameof(InitializeAsync)).Information("Resolving ISettingsSystem...");
        var settingsSystem = resolver.Resolve<ISettingsSystem>();
        log.ForMethod(nameof(InitializeAsync)).Information("Resolved ISettingsSystem: {Type}", settingsSystem?.GetType().Name ?? "NULL");
        
        if (settingsSystem == null)
        {
          log.ForMethod(nameof(InitializeAsync)).Error("settingsSystem is NULL after resolve");
          return;
        }
        
        // Initialize audio service with settings binding
        if (audioService is AudioService service)
        {
          log.ForMethod(nameof(InitializeAsync)).Information("Calling InitializeAsync on AudioService");
          await service.InitializeAsync(settingsSystem, ct);
          log.ForMethod(nameof(InitializeAsync)).Information("Audio runtime initialized successfully");
        }
        else
        {
          log.ForMethod(nameof(InitializeAsync)).Error("AudioService is not AudioService type: {Type}", audioService?.GetType().Name ?? "NULL");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod(nameof(InitializeAsync)).Error(ex, "Failed to initialize Audio runtime");
      }
    }

    /// <summary>
    /// Creates the audio root transform for organizing audio sources.
    /// Global audio root persists across all scenes for interface sounds.
    /// </summary>
    private Transform CreateAudioRoot()
    {
      var audioRootObject = new GameObject("[Global] AudioRoot");
      
      // Ensure it persists across scenes - critical for interface sounds
      DontDestroyOnLoad(audioRootObject);
      
      return audioRootObject.transform;
    }

    private void OnDestroy()
    {
      cts?.Cancel();
      cts?.Dispose();
      
      initializationSemaphore?.Dispose();
      
      // Dispose audio service if it implements IDisposable
      if (audioService is IDisposable disposableService)
      {
        disposableService.Dispose();
      }
    }
  }
}
