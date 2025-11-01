using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Serilog;
using ILogger = Serilog.ILogger;
using UnityEngine.SceneManagement;
using MToolKit.Runtime.Core.Host;
using MToolKit.Runtime.Core.Config;
using VContainer.Unity;
using MToolKit.Runtime.Core.Abstractions;
using Sirenix.OdinInspector;
using MToolKit.Runtime.Core.Interfaces;
using VContainer;
using MToolKit.Runtime.MessageBus;
using MessagePipe;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.MessageBus.Events;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.Services;
using MToolKit.Template.ExamplePlayer.Interface;
using MToolKit.Template.ExamplePlayer;
using Cysharp.Threading.Tasks;
using MToolKit.Template.UI;
using MToolKit.Runtime.Navigation.Events;
using MToolKit.Template.ExamplePlayer.Events;
using MToolKit.Runtime.ExamplePlayer.Events;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using MToolKit.Runtime.Core.Singletons;

/// <summary>
/// Namespace for the template game installer.
/// </summary>
namespace MToolKit.Template.Installer
{
  /// <summary>
  /// The template game installer.
  /// </summary>
  public class ExampleGameInstaller : LifetimeScope 
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<ExampleGameInstaller>().ForFeature("MToolKit.Template.Installer"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
    [SerializeField][Required] GameRuntimeHost gameRuntimeHostPrefab;
    [SerializeField][Required] private UIRoot uiRootPrefab;
    [SerializeField] private ES3SaveConfig saveConfig;
#if ENABLE_INPUT_SYSTEM
    [SerializeField] private InputActionAsset inputActionAsset;
#endif
    

    [ReadOnly] [ShowInInspector] public PluginConfigAsset PluginConfig => GlobalConfigLoader.Instance?.PluginConfig ?? null;
    [ReadOnly] [ShowInInspector] public List<AbstractGamePlugin> CurrentPluginInstances { get; private set; } = new();
    [ReadOnly] [ShowInInspector] public string CurrentSceneName { get; private set; }
    [ReadOnly] [ShowInInspector] public UIRoot UIRootInstance { get; private set; }
    [ReadOnly] [ShowInInspector] public GameRuntimeHost RuntimeHostInstance { get; private set; }
    
    // Track if we've already handled the initial scene load to prevent duplicate processing
    private bool hasHandledInitialSceneLoad = false;
    
    public static ExampleGameInstaller Instance { get; private set; }

    protected override void Configure(IContainerBuilder builder)
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Configuring ExampleGameInstaller");

      // Validate required fields
      if (uiRootPrefab == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("UIRootPrefab is not assigned in ExampleGameInstaller!");
        return;
      }

      if (gameRuntimeHostPrefab == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("GameRuntimeHostPrefab is not assigned in ExampleGameInstaller!");
        return;
      }

      // Set up MessagePipe for basic messaging
      RegisterMessagePipe(builder);

      // Initialize GameMessageBroker early so components can use it
      builder.RegisterBuildCallback(resolver =>
      {
        GameRoot.Initialize(resolver);
        GameMessageBroker.Initialize(resolver);
        log.ForGameObject(gameObject).ForMethod().Verbose("GameMessageBroker initialized");
      });

      // Register GameRuntime service
      builder.Register<IGameRuntime, GameRuntime>(Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Debug("Registered IGameRuntime service");

      // Register PluginRegistry
      builder.Register<PluginRegistry>(Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Debug("Registered PluginRegistry");

      // Register IExamplePlayerService using factory pattern - must be before plugin registration
      builder.Register<IExamplePlayerService>(resolver =>
      {
        var service = new MToolKit.Template.ExamplePlayer.ExamplePlayerService();
        log.ForGameObject(gameObject).ForMethod().Debug("Created IExamplePlayerService via factory");
        return service;
      }, Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Debug("Registered IExamplePlayerService for game scene");

      // Register minimal navigation service (no-op implementation for scenes without full navigation system)
      builder.Register<INavigationService, NullNavigationService>(Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Debug("Registered INavigationService with NullNavigationService (no-op implementation)");

      // Register ES3SaveConfig (use provided config or create default)
      builder.Register<ES3SaveConfig>(resolver =>
      {
        var config = saveConfig != null ? saveConfig : ScriptableObject.CreateInstance<ES3SaveConfig>();
        log.ForGameObject(gameObject).ForMethod().Debug("Using {0} ES3SaveConfig", saveConfig != null ? "provided" : "default");
        return config;
      }, Lifetime.Singleton);

      // IES3Service is already registered globally by GlobalInstaller
      // No need to register it again here

      // SaveDomainControllerRegistry is managed internally by SaveSystemCoordinator
      // No need to register it separately here

      // SaveSystemCoordinator is already registered globally by GlobalInstaller
      // No need to register it again here

      // ES3GameSaveSystem is already registered globally by GlobalInstaller
      // No need to register it again here

      // Register runtime systems collection including save system
      builder.Register<IEnumerable<IRuntimeSystem>>(resolver =>
      {
        var systems = new List<IRuntimeSystem>();
        
        // Add save system to runtime systems (using globally registered instance)
        if (resolver.TryResolve(out ES3GameSaveSystem saveSystem))
        {
          systems.Add(saveSystem);
          log.ForGameObject(gameObject).ForMethod().Debug("Added globally registered ES3GameSaveSystem to runtime systems");
        }
        else
        {
          log.ForGameObject(gameObject).ForMethod().Warning("ES3GameSaveSystem not available in resolver");
        }
        
        log.ForGameObject(gameObject).ForMethod().Debug("Runtime systems collection built with {0} systems", systems.Count);
        return systems;
      }, Lifetime.Singleton);

      // Instantiate and register UIRoot using RegisterBuildCallback for proper timing
      builder.RegisterBuildCallback(resolver =>
      {
        UIRootInstance = Instantiate(uiRootPrefab);
        UIRootInstance.name = "UIRoot";
        
        // Inject dependencies into UIRoot and its child panels
        resolver.InjectGameObject(UIRootInstance.gameObject);
        resolver.InjectGameObject(UIRootInstance.PausePanel.gameObject);
        resolver.InjectGameObject(UIRootInstance.GameOverPanel.gameObject);
        resolver.InjectGameObject(UIRootInstance.BlackoutPanel.gameObject);
        
        log.ForGameObject(gameObject).ForMethod().Debug("UIRoot instantiated and injected");
      });

      // Instantiate and register GameRuntimeHost using RegisterBuildCallback
      builder.RegisterBuildCallback(resolver =>
      {
        RuntimeHostInstance = Instantiate(gameRuntimeHostPrefab);
        RuntimeHostInstance.name = "GameRuntimeHost";
        
        // Inject dependencies into GameRuntimeHost
        resolver.InjectGameObject(RuntimeHostInstance.gameObject);
        
        log.ForGameObject(gameObject).ForMethod().Debug("GameRuntimeHost instantiated and injected");
      });

      // Register UIRoot instance for dependency injection (will be set in build callback)
      builder.Register<UIRoot>(resolver => UIRootInstance, Lifetime.Singleton);

      // Register GameRuntimeHost instance (will be set in build callback)
      builder.Register<GameRuntimeHost>(resolver => RuntimeHostInstance, Lifetime.Singleton);

      // Initialize save system coordinator after container is built
      builder.RegisterBuildCallback(resolver =>
      {
        // Use the globally registered SaveSystemCoordinator
        var coordinator = resolver.Resolve<SaveSystemCoordinator>();
        coordinator.SwitchToLocalSystem();
        log.ForGameObject(gameObject).ForMethod().Debug("SaveSystemCoordinator initialized and switched to local system");
        
        // Register plugins with PluginRegistry now that it's available
        RegisterPluginsWithRegistry();
        
        // Initialize the plugins
        if (GameRoot.PluginRegistry != null)
        {
          GameRoot.PluginRegistry.PerformPluginSetup(resolver);
          log.ForGameObject(gameObject).ForMethod().Debug("Plugin setup completed");
          
          GameRoot.PluginRegistry.PerformPluginRuntimeInitialization(resolver);
          log.ForGameObject(gameObject).ForMethod().Debug("Plugin runtime initialization completed");
        }
      });

      // Register game plugins if config is provided
      log.ForGameObject(gameObject).ForMethod().Debug("PluginConfig is {0}", PluginConfig != null ? "not null" : "null");
      if (PluginConfig != null)
      {
        log.ForGameObject(gameObject).ForMethod().Debug("PluginConfig has {0} plugins", PluginConfig.PluginPrefabs?.Count ?? 0);
        RegisterGamePlugins(builder);
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Warning("PluginConfig is null - no plugins will be registered!");
      }

      log.ForGameObject(gameObject).ForMethod().Debug("ExampleGameInstaller configuration completed");
    }

    private void RegisterMessagePipe(IContainerBuilder builder)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Setting up MessagePipe");

      MessagePipeOptions options = builder.RegisterMessagePipe();

      // Register basic message brokers that might be used by UI components
      builder.RegisterMessageBroker<QuitRequestMessage>(options);
      builder.RegisterMessageBroker<PlayerRespawnRequestMessage>(options);
      builder.RegisterMessageBroker<SceneLoadedMessage>(options);
      builder.RegisterMessageBroker<PlayerDeathMessage>(options);
      builder.RegisterMessageBroker<PauseToggledMessage>(options);
      builder.RegisterMessageBroker<FadeBlackoutMessage>(options);
      builder.RegisterMessageBroker<EnablePlayerMovementMessage>(options);

      log.ForGameObject(gameObject).ForMethod().Verbose("Basic message brokers registered");
    }

    private void RegisterGamePlugins(IContainerBuilder builder)
    {
      if (builder == null)
      {
        throw new ArgumentNullException(nameof(builder));
      }

      if (PluginConfig?.PluginPrefabs == null || PluginConfig.PluginPrefabs.Count == 0)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("No game plugins configured");
        return;
      }

      log.ForGameObject(gameObject).ForMethod().Debug("Registering {0} game plugins: {1}", PluginConfig.PluginPrefabs.Count, PluginConfig.PluginPrefabs.Any() ? string.Join(", ", PluginConfig.PluginPrefabs.Where(p => p != null).Select(p => p.name)) : "none");

      foreach (AbstractGamePlugin prefab in PluginConfig.PluginPrefabs)
      {
        if (prefab == null)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Game plugin prefab is null");
          continue;
        }

        log.ForGameObject(gameObject).ForMethod().Verbose("Instantiating game plugin: {0}", prefab.name);
        AbstractGamePlugin instance = Instantiate(prefab);
        instance.name = prefab.name;
        
        // Store the instance for lifecycle management
        CurrentPluginInstances.Add(instance);
        
        log.ForGameObject(gameObject).ForMethod().Verbose("About to call Register on: {0}", instance.name);
        instance.Register(builder);
        log.ForGameObject(gameObject).ForMethod().Verbose("Game plugin registered: {0}", instance.name);
      }

      log.ForGameObject(gameObject).ForMethod().Verbose("Game plugins registration completed.");
    }

    private void RegisterPluginsWithRegistry()
    {
      if (GameRoot.PluginRegistry == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("PluginRegistry still not available during build callback");
        return;
      }

      foreach (var plugin in CurrentPluginInstances)
      {
        if (plugin != null)
        {
          GameRoot.PluginRegistry.Register(plugin);
          log.ForGameObject(gameObject).ForMethod().Debug("Plugin registered with PluginRegistry: {0}", plugin.name);
        }
      }
    }

    protected override void Awake()
    {
      if (Instance != null && Instance != this)
      {
        Destroy(gameObject);
        return;
      }

      Instance = this;

      // Subscribe to scene changes
      SceneManager.sceneLoaded += OnSceneLoaded;
      SceneManager.sceneUnloaded += OnSceneUnloaded;

      base.Awake();
    }
    protected override void OnDestroy()
    {
      SceneManager.sceneLoaded -= OnSceneLoaded;
      SceneManager.sceneUnloaded -= OnSceneUnloaded;

      // Shutdown all systems before destroying
      ShutdownRuntimeSystems();

      // Reset the GameMessageBroker when the installer is destroyed
      GameMessageBroker.Reset();

      base.OnDestroy();
      if (Instance == this)
        Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("[SCENE_LOAD] Scene loaded: {0} with mode: {1}", scene.name, mode);
      log.ForGameObject(gameObject).ForMethod().Debug("[SCENE_LOAD] CurrentSceneName: '{0}', NewSceneName: '{1}', IsDifferent: {2}", 
          CurrentSceneName ?? "null", scene.name, !string.IsNullOrEmpty(CurrentSceneName) && scene.name != CurrentSceneName);

      // Log the stack trace to understand what triggered the scene load
      log.ForGameObject(gameObject).ForMethod().Debug("[SCENE_LOAD] Scene load triggered by: {StackTrace}", 
          System.Environment.StackTrace.Split('\n').Take(5).Aggregate((a, b) => a + "\n" + b));

      // Check if this is a duplicate scene load for the same scene
      if (string.IsNullOrEmpty(CurrentSceneName) && hasHandledInitialSceneLoad)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("[SCENE_LOAD] Duplicate initial scene load detected for '{0}', skipping to prevent player reset", scene.name);
        return;
      }

      // Always ensure only one AudioListener exists
      EnsureSingleAudioListener();

      // Handle plugin recreation for scene changes
      if (mode == LoadSceneMode.Single && !string.IsNullOrEmpty(CurrentSceneName) && scene.name != CurrentSceneName)
      {
        log.ForGameObject(gameObject).ForMethod().Debug("[SCENE_LOAD] Scene changed from {0} to {1}, recreating plugins", CurrentSceneName, scene.name);

        // Shutdown runtime systems before destroying plugin instances
        ShutdownRuntimeSystems();

        // Destroy old plugin instances
        foreach (var plugin in CurrentPluginInstances)
        {
          if (plugin != null)
          {
            log.ForGameObject(gameObject).ForMethod().Verbose("[SCENE_LOAD] Destroying old plugin instance: {0}", plugin.name);
            Destroy(plugin.gameObject);
          }
        }

        // Rebuild the container to create fresh plugin instances
        RebuildContainer();
      }
      else if (mode == LoadSceneMode.Additive)
      {
        log.ForGameObject(gameObject).ForMethod().Debug("[SCENE_LOAD] Additive scene loaded: {0}, ensuring proper plugin state", scene.name);
        // For additive scenes, we don't recreate plugins but ensure they're in a valid state
        EnsurePluginState();
      }
      else if (string.IsNullOrEmpty(CurrentSceneName))
      {
        log.ForGameObject(gameObject).ForMethod().Debug("[SCENE_LOAD] Initial scene load: {0}", scene.name);
        // This is the initial scene load, ensure AudioListener is properly set up
        EnsureSingleAudioListener();
        hasHandledInitialSceneLoad = true;
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Debug("[SCENE_LOAD] Same scene reload detected: {0}, skipping plugin recreation", scene.name);
      }

      // Update current scene name
      CurrentSceneName = scene.name;

      // Reload player position after scene load to ensure it's correct
      ReloadPlayerPositionAfterSceneLoad();

      // TODO: Reset events for plugins that need to be reset for new scenes
      // inventoryInitializationEventPublished = false;
    }
    
    private void OnSceneUnloaded(Scene scene)
    {
      log.ForGameObject(gameObject).ForMethod().Debug("Scene unloaded: {0}, shutting down plugins", scene.name);

      // Shutdown all runtime systems before scene unload
      ShutdownRuntimeSystems();

      // Destroy all plugin instances when scene is unloaded
      DestroyAllPluginInstances();

      // Clear GameRuntimeHost reference since it will be destroyed with the scene
      RuntimeHostInstance = null;
      log.ForGameObject(gameObject).ForMethod().Verbose("Cleared RuntimeHostInstance reference on scene unload");
      
      // Reset the initial scene load flag for the next scene
      hasHandledInitialSceneLoad = false;
      log.ForGameObject(gameObject).ForMethod().Debug("Reset hasHandledInitialSceneLoad flag for next scene");
    }
    private void DestroyAllPluginInstances()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Destroying all plugin instances");

      foreach (var plugin in CurrentPluginInstances)
      {
        if (plugin != null)
        {
          try
          {
            log.ForGameObject(gameObject).ForMethod().Verbose("Destroying plugin instance: {0}", plugin.name);
            Destroy(plugin.gameObject);
          }
          catch (Exception ex)
          {
            log.ForGameObject(gameObject).ForMethod().Error("Error destroying plugin {0}: {1}", plugin.name, ex);
          }
        }
      }

      CurrentPluginInstances.Clear();
      log.ForGameObject(gameObject).ForMethod().Debug("All plugin instances destroyed");
    }

    private void EnsureSingleAudioListener()
    {
      var audioListeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

      if (audioListeners.Length > 1)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Multiple AudioListeners found ({0}), disabling extras", audioListeners.Length);

        // Keep the first one enabled, disable the rest
        for (int i = 1; i < audioListeners.Length; i++)
        {
          if (audioListeners[i] != null)
          {
            log.ForGameObject(gameObject).ForMethod().Verbose("Disabling AudioListener on: {0}", audioListeners[i].gameObject.name);
            audioListeners[i].enabled = false;
          }
        }
      }
      else if (audioListeners.Length == 0)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("No AudioListener found in scene");
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("Single AudioListener found: {0}", audioListeners[0].gameObject.name);
      }
    }


    private void EnsurePluginState()
    {
      // Ensure all plugins are in a valid state after scene changes
      foreach (var plugin in CurrentPluginInstances)
      {
        if (plugin != null && plugin is IRuntimeSystem)
        {
          try
          {
            log.ForGameObject(gameObject).ForMethod().Verbose("Ensuring plugin state: {0}", plugin.name);
            // Don't restart, just ensure they're in a valid state
          }
          catch (Exception ex)
          {
            log.ForGameObject(gameObject).ForMethod().Error("Error ensuring plugin state {0}: {1}", plugin.name, ex);
          }
        }
      }
    }

    /// <summary>
    /// Clean up a specific plugin instance
    /// </summary>
    public void CleanupPlugin(AbstractGamePlugin plugin)
    {
      if (plugin == null) return;

      try
      {
        if (plugin is IRuntimeSystem runtimeSystem)
        {
          log.ForGameObject(gameObject).ForMethod().Information("Cleaning up runtime system: {0}", plugin.name);
          runtimeSystem.Shutdown();
        }

        CurrentPluginInstances.Remove(plugin);
        log.ForGameObject(gameObject).ForMethod().Information("Plugin cleaned up: {0}", plugin.name);
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error("Error cleaning up plugin {0}: {1}", plugin.name, ex);
      }
    }
    
    private void ShutdownRuntimeSystems()
    {
      if (Container == null) return;

      log.ForGameObject(gameObject).ForMethod().Debug("Shutting down runtime systems");

      // Shutdown all IRuntimeSystem plugins
      foreach (var plugin in CurrentPluginInstances)
      {
        if (plugin != null && plugin is IRuntimeSystem runtimeSystem)
        {
          try
          {
            // Check if the plugin is still valid (not destroyed)
            if (plugin.gameObject != null)
            {
              log.ForGameObject(gameObject).ForMethod().Debug("Shutting down runtime system: {0}", plugin.name);
              runtimeSystem.Shutdown();
            }
            else
            {
              log.ForGameObject(gameObject).ForMethod().Warning("Skipping shutdown for destroyed plugin: {0}", plugin.name);
            }
          }
          catch (Exception ex)
          {
            log.ForGameObject(gameObject).ForMethod().Error("Error shutting down runtime system {0}: {1}", plugin.name, ex);
          }
        }
      }

      // Shutdown the GameRuntime if available
      if (Container.TryResolve(out IGameRuntime gameRuntime))
      {
        try
        {
          log.ForGameObject(gameObject).ForMethod().Debug("Shutting down GameRuntime");
          gameRuntime.Shutdown();
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).ForMethod().Error("Error shutting down GameRuntime: {0}", ex);
        }
      }
    }
    private void RebuildContainer()
    {
      log.ForGameObject(gameObject).ForMethod().Information("Rebuilding container for new scene");

      // Destroy old UIRoot instance if it exists
      if (UIRootInstance != null)
      {
        log.ForGameObject(gameObject).ForMethod().Information("Destroying old UIRoot instance");
        Destroy(UIRootInstance.gameObject);
        UIRootInstance = null;
      }

      // Destroy old RuntimeHost instance if it exists
      if (RuntimeHostInstance != null)
      {
        log.ForGameObject(gameObject).ForMethod().Information("Destroying old RuntimeHost instance");
        Destroy(RuntimeHostInstance.gameObject);
        RuntimeHostInstance = null;
      }

      // Don't dispose the container as it contains global services that should persist
      // across scene transitions. The container will be rebuilt with Build() call below.
      log.ForGameObject(gameObject).ForMethod().Debug("Preserving container with global services across scene transition");

      // Rebuild the container
      Build();

      log.ForGameObject(gameObject).ForMethod().Information("Container rebuilt successfully");
    }
    
    /// <summary>
    /// Reload player position after scene load to ensure it's correct
    /// </summary>
    private void ReloadPlayerPositionAfterSceneLoad()
    {
      log.ForGameObject(gameObject).ForMethod().Debug("[SCENE_LOAD] Attempting to reload player position after scene load");
      
      // Use UniTask for async operation instead of coroutines
      ReloadPlayerPositionAsync().Forget();
    }
    
    private async UniTask ReloadPlayerPositionAsync()
    {
      try
      {
        // Wait for the player plugin to be ready instead of using fixed timing
        ExamplePlayerPlugin playerPlugin = null;
        const int maxWaitTimeMs = 5000; // Maximum wait time (5 seconds)
        const int checkIntervalMs = 16; // Check every ~16ms (60fps)
        var startTime = DateTime.UtcNow;
        
        while (playerPlugin == null && !IsCancellationRequested(startTime, maxWaitTimeMs))
        {
          playerPlugin = FindFirstObjectByType<ExamplePlayerPlugin>();
          if (playerPlugin == null)
          {
            await UniTask.Delay(checkIntervalMs);
          }
        }
        
        if (playerPlugin == null)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("[SCENE_LOAD] ExamplePlayerPlugin not found after waiting {0}ms, cannot reload player position", maxWaitTimeMs);
          return;
        }
        
        // Wait for the player to be spawned and ready
        const int maxPlayerWaitTimeMs = 1000; // 1 second
        var playerStartTime = DateTime.UtcNow;
        
        while (!IsCancellationRequested(playerStartTime, maxPlayerWaitTimeMs))
        {
          // Check if player is spawned and has a valid transform
          if (playerPlugin.PlayerInstance != null && playerPlugin.PlayerTransform != null)
          {
            var elapsedMs = (DateTime.UtcNow - playerStartTime).TotalMilliseconds;
            log.ForGameObject(gameObject).ForMethod().Debug("[SCENE_LOAD] Player is ready after {0:F0}ms, proceeding with position reload", elapsedMs);
            break;
          }
          
          await UniTask.Delay(checkIntervalMs);
        }
        
        if (IsCancellationRequested(playerStartTime, maxPlayerWaitTimeMs))
        {
          log.ForGameObject(gameObject).ForMethod().Warning("[SCENE_LOAD] Player not ready after {0}ms, skipping position reload", maxPlayerWaitTimeMs);
          return;
        }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error("Error reloading player position: {0}", ex);
      }
    }
    
    private static bool IsCancellationRequested(DateTime startTime, int maxWaitTimeMs)
    {
      return (DateTime.UtcNow - startTime).TotalMilliseconds > maxWaitTimeMs;
    }

  }
}