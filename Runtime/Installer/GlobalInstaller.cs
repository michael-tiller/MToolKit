using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using MessagePipe;
using MToolKit.Runtime.Analytics;
using MToolKit.Runtime.AssetLoader;
using MToolKit.Runtime.AssetLoader.Interfaces;
using MToolKit.Runtime.Bootstrapper;
using MToolKit.Runtime.Bootstrapper.Interfaces;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.Core.Abstractions;
using MToolKit.Runtime.Core.Config;
using MToolKit.Runtime.Core.Interfaces;
using MToolKit.Runtime.Core.Singletons;
using MToolKit.Runtime.ErrorSystem;
using MToolKit.Runtime.ErrorSystem.Messages;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Input.Interfaces;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.MessageBus;
using MToolKit.Runtime.Music;
using MToolKit.Runtime.MessageBus.Events;
using MToolKit.Runtime.Navigation;
using MToolKit.Runtime.Navigation.Events;
using MToolKit.Runtime.Persistence;
using MToolKit.Runtime.Persistence.ES3Integration;
using MToolKit.Runtime.Persistence.Interfaces;
using MToolKit.Runtime.Settings;
using MToolKit.Runtime.Settings.Ini;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.Exceptions;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;
using Object = UnityEngine.Object;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

#endif

[assembly: InternalsVisibleTo("Assembly-CSharp._MTools.Tests")]

namespace MToolKit.Runtime.Installer
{
  /// <summary>
  ///   Global installer that provides core services across all scenes.
  ///   This installer persists via DontDestroyOnLoad and provides services
  ///   that are needed before GameInstaller is available.
  /// </summary>
  public class GlobalInstaller : LifetimeScope
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GlobalInstaller>().ForFeature("Runtime.Installers"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    private GlobalPluginConfigAsset globalPluginConfig;
    private readonly bool globalPluginConfigSetByTest = false;
    private bool isInitialSetup = true;
    private static bool inputServiceInitialized; // Static to prevent duplicate initialization across scopes
#if ENABLE_INPUT_SYSTEM
    [SerializeField]
    private InputActionAsset inputActionAsset;
#endif

    public static GlobalInstaller Instance { get; private set; }

    // Global plugin instances for PluginDiagnosisWindow
    [field: ReadOnly]
    [ShowInInspector]
    public NavigationPlugin NavigationPluginInstance { get; private set; }

    [field: ReadOnly]
    [ShowInInspector]
    public SettingsPlugin SettingsPluginInstance { get; private set; }

    [field: ReadOnly]
    [ShowInInspector]
    public ES3GameSavePlugin GameSavePluginInstance { get; private set; }

    [field: ReadOnly]
    [ShowInInspector]
    public InputRebinderPlugin InputRebinderPluginInstance { get; private set; }

    [field: ReadOnly]
    [ShowInInspector]
    public AnalyticsPlugin AnalyticsPluginInstance { get; private set; }

    [field: ReadOnly]
    [ShowInInspector]
    public ErrorSystemPlugin ErrorSystemPluginInstance { get; private set; }

    [field: ReadOnly]
    [ShowInInspector]
    public IProfileManager ProfileManagerInstance { get; private set; }

    [field: ReadOnly]
    [ShowInInspector]
    public IInputService InputServiceInstance { get; private set; }

    /// <summary>
    ///   Gets the ES3GameSavePlugin instance if it exists and is ready for initialization.
    /// </summary>
    public ES3GameSavePlugin GetES3GameSavePlugin()
    {
      if (GameSavePluginInstance != null && GameSavePluginInstance.gameObject != null)
        return GameSavePluginInstance;

      // Fallback to finding in scene
      ES3GameSavePlugin sceneInstance = FindFirstObjectByType<ES3GameSavePlugin>();
      if (sceneInstance != null)
      {
        GameSavePluginInstance = sceneInstance;
        return sceneInstance;
      }

      return null;
    }

    /// <summary>
    ///   Resets the singleton to its initial state. Useful for testing.
    /// </summary>
    public static void ResetSingleton()
    {
      Instance = null;
      log.ForMethod().Verbose("GlobalInstaller singleton reset");
    }

    /// <summary>
    ///   Sets a specific instance for testing purposes. Use with caution.
    /// </summary>
    /// <param name="instance">The instance to set as the singleton</param>
    public static void SetInstanceForTesting(GlobalInstaller instance)
    {
      Instance = instance;
      log.ForMethod().Verbose("GlobalInstaller singleton set for testing");
    }

    /// <summary>
    ///   For testing purposes - allows calling OnDestroy behavior
    /// </summary>
    public void TestOnDestroy()
    {
      OnDestroy();
    }

    /// <summary>
    ///   For testing purposes - allows calling Awake behavior
    /// </summary>
    public void TestAwake()
    {
      if (DisableSingletonBehavior)
      {
        // In test mode, set this as instance if none exists
        if (Instance == null)
        {
          Instance = this;
          log.ForGameObject(gameObject).ForMethod().Verbose("GlobalInstaller set as instance (test mode)");
        }
        return;
      }

      Awake();
    }

    /// <summary>
    ///   For testing purposes - allows disabling singleton behavior
    /// </summary>
    public static bool DisableSingletonBehavior { get; set; }

    protected override void Awake()
    {
      base.Awake();

      if (DisableSingletonBehavior)
      {
        // In test mode, don't set instance automatically - let tests control when it's set
        log.ForGameObject(gameObject).ForMethod().Verbose("GlobalInstaller created (test mode)");
        return;
      }

      if (Instance == null)
      {
        Instance = this;
        DontDestroyOnLoad(gameObject);
        log.ForGameObject(gameObject).ForMethod().Debug("GlobalInstaller initialized and persisted");

        // Subscribe to scene change events
        SceneManager.sceneLoaded += OnSceneLoaded;
      }
      else if (Instance != this)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("GlobalInstaller already exists, destroying duplicate");
        Destroy(gameObject);
      }
    }

    protected override void OnDestroy()
    {
      // Unsubscribe from scene events
      SceneManager.sceneLoaded -= OnSceneLoaded;

      if (DisableSingletonBehavior)
      {
        // In test mode, don't clear the instance automatically
        log.ForGameObject(gameObject).ForMethod().Verbose("GlobalInstaller destroyed (test mode)");
        return;
      }

      if (Instance == this)
      {
        Instance = null;
        log.ForGameObject(gameObject).ForMethod().Verbose("GlobalInstaller destroyed");
      }
      base.OnDestroy();
    }

    protected override void Configure(IContainerBuilder builder)
    {
      if (builder == null)
        throw new ArgumentNullException(nameof(builder));

      log.ForGameObject(gameObject).ForMethod().Verbose("Configuring GlobalInstaller");

      // Use local field if already set (e.g., in tests), otherwise get from GlobalConfigLoader
      if (!globalPluginConfigSetByTest)
        globalPluginConfig = GlobalConfigLoaderHelper.Instance?.GlobalPluginConfig;
      log.ForGameObject(gameObject).ForMethod().Verbose("GlobalPluginConfig: {0}", globalPluginConfig != null ? "not null" : "null");

      // Set up MessagePipe for global communication
      RegisterMessagePipe(builder);

      // ES3SaveConfig will be registered by ES3GameSavePlugin via ConfigPlugin pattern
      // No need to register it here to avoid conflicts

      // Register ES3 service globally so it's available in all scenes
      // Use ProfileAwareES3Service to support profile-specific save files
      builder.Register<IES3Service>(resolver =>
      {
        try
        {
          ES3SaveConfig config = resolver.Resolve<ES3SaveConfig>();
          // Create a temporary ES3Service for ProfileManager initialization
          ES3SaveService tempES3Service = new(config.SaveFileName, config);
          ProfileManagerInstance = new ProfileManager(tempES3Service, config);
          return new ProfileAwareES3Service(config, ProfileManagerInstance);
        }
        catch (VContainerException)
        {
          // Config not available yet, create with default settings
          log.ForGameObject(gameObject).ForMethod().Warning("ES3SaveConfig not available, using default settings");
          ES3SaveConfig defaultConfig = ScriptableObject.CreateInstance<ES3SaveConfig>();
          ES3SaveService tempES3Service = new(defaultConfig.SaveFileName, defaultConfig);
          ProfileManagerInstance = new ProfileManager(tempES3Service, defaultConfig);
          return new ProfileAwareES3Service(defaultConfig, ProfileManagerInstance);
        }
      }, Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Verbose("Registered IES3Service globally");

      // Register SaveDomainControllerRegistry globally
      builder.Register<SaveDomainControllerRegistry>(Lifetime.Singleton);

      // Register PluginRegistry globally so it's available in all scenes
      builder.Register<PluginRegistry>(Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Verbose("Registered PluginRegistry globally");

      // Register ProfileManager globally so it's available in all scenes
      builder.Register(resolver =>
      {
        IES3Service es3Service = resolver.Resolve<IES3Service>();
        // Extract ProfileManager from ProfileAwareES3Service
        if (es3Service is ProfileAwareES3Service profileAwareService)
          return profileAwareService.ProfileManager;

        // Fallback: create a new ProfileManager
        ES3SaveConfig saveConfig;
        try
        {
          saveConfig = resolver.Resolve<ES3SaveConfig>();
        }
        catch (VContainerException)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("ES3SaveConfig not available for ProfileManager, using default");
          saveConfig = ScriptableObject.CreateInstance<ES3SaveConfig>();
        }

        ProfileManagerInstance = new ProfileManager(es3Service, saveConfig);
        return ProfileManagerInstance;
      }, Lifetime.Singleton);


#if USE_ADDRESSABLES
      builder.Register<IAssetLoader, AddressablesAssetLoader>(Lifetime.Singleton);
#else
      builder.Register<IAssetLoader, ResourcesAssetLoader>(Lifetime.Singleton);
#endif
      builder.Register<IRuntimeAssetService, RuntimeAssetService>(Lifetime.Singleton);
      builder.Register<IContentLoaderService, ContentLoaderService>(Lifetime.Singleton);
      builder.Register<IGameLoader, GameLoader>(Lifetime.Singleton);

      // Register INI Service globally so it's available before Settings
      builder.Register<IIniService>(resolver =>
      {
        try
        {
          // Try to load IniConfig from Resources
          IniConfig iniConfig = Resources.Load<IniConfig>("IniConfig");
          if (iniConfig == null)
          {
            log.ForGameObject(gameObject).ForMethod().Warning("IniConfig not found in Resources, creating default config");
            iniConfig = ScriptableObject.CreateInstance<IniConfig>();
          }

          return new IniService(iniConfig);
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).ForMethod().Error(ex, "Failed to create INI service: {Message}", ex.Message);
          // Create with default config as fallback
          IniConfig defaultConfig = ScriptableObject.CreateInstance<IniConfig>();
          return new IniService(defaultConfig);
        }
      }, Lifetime.Singleton);

      // Load INI file asynchronously after container is built
      builder.RegisterBuildCallback(async resolver =>
      {
        // CRITICAL: Check if gameObject is still valid before accessing it
        // This prevents MissingReferenceException when the object is destroyed
        if (this == null || gameObject == null)
        {
          return; // Early exit if object is destroyed
        }

        try
        {
          IIniService iniService = resolver.Resolve<IIniService>();
          await iniService.LoadAsync();
          log.ForGameObject(gameObject).ForMethod().Information("INI service loaded successfully");
        }
        catch (MissingReferenceException)
        {
          // Silently ignore if object was destroyed during async operation
          return;
        }
        catch (Exception ex)
        {
          // Only log if object is still valid
          if (this != null && gameObject != null)
          {
            log.ForGameObject(gameObject).ForMethod().Error(ex, "Failed to load INI file: {Message}", ex.Message);
          }
        }
      });

      log.ForGameObject(gameObject).ForMethod().Verbose("Registered IIniService globally");

      log.ForGameObject(gameObject).ForMethod().Verbose("Registered ProfileManager globally");

      // Register ILocalizationService using the adapter pattern
      builder.Register<ILocalizationService, LocalizationServiceAdapter>(Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Verbose("Registered ILocalizationService with LocalizationServiceAdapter");

      // Register IMusicManager using the adapter pattern
      builder.Register<IMusicManager, MusicManagerAdapter>(Lifetime.Singleton);
      log.ForGameObject(gameObject).ForMethod().Verbose("Registered IMusicManager with MusicManagerAdapter");

      // Register SaveSystemCoordinator globally so it's available in all scenes
      builder.Register(resolver =>
      {
        IES3Service es3Service = resolver.Resolve<IES3Service>();
        SaveDomainControllerRegistry globalRegistry = resolver.Resolve<SaveDomainControllerRegistry>();

        // Try to resolve config, fall back to default if not available
        ES3SaveConfig saveConfig;
        try
        {
          saveConfig = resolver.Resolve<ES3SaveConfig>();
        }
        catch (VContainerException)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("ES3SaveConfig not available for SaveSystemCoordinator, using default");
          saveConfig = ScriptableObject.CreateInstance<ES3SaveConfig>();
        }

        // Create a local registry that will be populated by GameInstaller
        SaveDomainControllerRegistry localRegistry = new();

        IProfileManager profileManager = resolver.Resolve<IProfileManager>();
        return new SaveSystemCoordinator(es3Service, globalRegistry, localRegistry, saveConfig, profileManager);
      }, Lifetime.Singleton);

      log.ForGameObject(gameObject).ForMethod().Verbose("Registered SaveSystemCoordinator globally");

      // Register ES3GameSaveSystem concrete type for backward compatibility
      // This is needed because some views still inject the concrete type
      builder.Register(resolver =>
      {
        IES3Service es3Service = resolver.Resolve<IES3Service>();
        SaveDomainControllerRegistry globalRegistry = resolver.Resolve<SaveDomainControllerRegistry>();

        // Create ES3GameSaveSystem with global controllers for backward compatibility
        IEnumerable<ISaveDomainController> globalControllers = globalRegistry.GetControllers();
        return new ES3GameSaveSystem(globalControllers, es3Service);
      }, Lifetime.Singleton);

      log.ForGameObject(gameObject).ForMethod().Verbose("Registered ES3GameSaveSystem concrete type for backward compatibility");

      // Register Input Service globally
      if (inputActionAsset != null)
      {
        builder.Register<IInputService, InputService>(Lifetime.Singleton);
        builder.RegisterInstance(inputActionAsset);

        // Initialize and enable InputService after container is built
        // Use a static flag to ensure we only initialize once across all scopes
        builder.RegisterBuildCallback(resolver =>
        {
          // CRITICAL: Check if gameObject is still valid before accessing it
          // This prevents MissingReferenceException when the object is destroyed
          if (this == null || gameObject == null)
          {
            return; // Early exit if object is destroyed
          }

          try
          {
            if (inputServiceInitialized)
            {
              log.ForGameObject(gameObject).ForMethod().Verbose("InputService already initialized in a previous scope, skipping duplicate initialization");

              // Still set the instance reference in case this is a child scope resolving the service
              if (InputServiceInstance == null)
              {
                InputServiceInstance = resolver.Resolve<IInputService>();
                log.ForGameObject(gameObject).ForMethod().Debug("Set InputServiceInstance reference from existing singleton");
              }
              return;
            }

            IInputService inputService = resolver.Resolve<IInputService>();

            // Additional defensive check: if InputService is already initialized, don't initialize again
            if (inputService is InputService)
            {
              InputServiceInstance = inputService;
              inputService.Initialize(inputActionAsset);
              inputService.Enable();
              inputServiceInitialized = true;
              log.ForGameObject(gameObject).ForMethod().Debug("InputService initialized and enabled");
            }
            else
            {
              log.ForGameObject(gameObject).ForMethod().Warning("Failed to resolve InputService or service is null");
            }
          }
          catch (MissingReferenceException)
          {
            // Silently ignore if object was destroyed during async operation
            return;
          }
        });

        log.ForGameObject(gameObject).ForMethod().Debug("Registered Input Service globally");
      }
      else
      {
        // Register a no-op implementation to prevent injection errors
        // This allows components to work without the Input System being configured
        builder.Register<IInputService, NullInputService>(Lifetime.Singleton);
        builder.RegisterBuildCallback(resolver =>
        {
          // CRITICAL: Check if gameObject is still valid before accessing it
          // This prevents MissingReferenceException when the object is destroyed
          if (this == null || gameObject == null)
          {
            return; // Early exit if object is destroyed
          }

          try
          {
            InputServiceInstance = resolver.Resolve<IInputService>();
            log.ForGameObject(gameObject).ForMethod().Debug("Using NullInputService (no-op implementation)");
          }
          catch (MissingReferenceException)
          {
            // Silently ignore if object was destroyed during async operation
            return;
          }
        });
        log.ForGameObject(gameObject).ForMethod().Warning("InputActionAsset not assigned in GlobalInstaller, using NullInputService (no-op implementation)");
      }

      // Register global plugins if config is provided
      if (globalPluginConfig != null)
      {
        RegisterGlobalPlugins(builder);
        RegisterAsyncEntryPoints(builder);
        InitializeGlobalRuntimePlugins(builder);
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Warning("No global plugin config provided");
      }
    }


    private void RegisterMessagePipe(IContainerBuilder builder)
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Setting up global MessagePipe");

      MessagePipeOptions options = builder.RegisterMessagePipe();

      // Set the global provider for MessagePipe diagnostics
      builder.RegisterBuildCallback(resolver =>
      {
        // CRITICAL: Check if gameObject is still valid before accessing it
        // This prevents MissingReferenceException when the object is destroyed
        if (this == null || gameObject == null)
        {
          return; // Early exit if object is destroyed
        }

        try
        {
          // Get the MessagePipe provider from the resolver
          IServiceProvider provider = resolver.Resolve<IServiceProvider>();
          GlobalMessagePipe.SetProvider(provider);
          log.ForGameObject(gameObject).ForMethod().Verbose("GlobalMessagePipe provider set");
        }
        catch (MissingReferenceException)
        {
          // Silently ignore if object was destroyed during async operation
          return;
        }
      });

      // Register global message brokers that need to persist across scenes
      builder.RegisterMessageBroker<BackRequestMessage>(options);
      builder.RegisterMessageBroker<QuitRequestMessage>(options);
      builder.RegisterMessageBroker<NavigationRequestMessage>(options);
      builder.RegisterMessageBroker<ClearRequestMessage>(options);
      builder.RegisterMessageBroker<ErrorRequestMessage>(options);
      builder.RegisterMessageBroker<InterstitialAlertRequestMessage>(options);

      log.ForGameObject(gameObject).ForMethod().Verbose("Global MessagePipe setup completed");
    }

    private void RegisterGlobalPlugins(IContainerBuilder builder)
    {
      if (builder == null)
        throw new ArgumentNullException(nameof(builder));

      if (globalPluginConfig?.GlobalPluginPrefabs == null || globalPluginConfig.GlobalPluginPrefabs.Count == 0)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("No global plugins configured");
        return;
      }

      log.ForGameObject(gameObject).ForMethod().Debug("Registering {0} global plugins: {1}", globalPluginConfig.GlobalPluginPrefabs.Count,
        globalPluginConfig.GlobalPluginPrefabs.Any() ? string.Join(", ", globalPluginConfig.GlobalPluginPrefabs.Where(p => p != null).Select(p => p.name)) : "none");

      foreach (AbstractGamePlugin prefab in globalPluginConfig.GlobalPluginPrefabs)
      {
        if (prefab == null)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Global plugin prefab is null");
          continue;
        }

        // Check if this type of plugin already exists (especially for persistent plugins)
        if (ShouldSkipPluginInstantiation(prefab))
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("Skipping instantiation of {0} - instance already exists and persists", prefab.name);

          // For persistent plugins, they were registered when first instantiated
          // The container should already have their services registered from the initial bootstrapper scene
          // No need to re-register as that would cause duplicate registration errors
          continue;
        }

        log.ForGameObject(gameObject).ForMethod().Verbose("Instantiating global plugin: {0}", prefab.name);
        AbstractGamePlugin instance = Instantiate(prefab);
        instance.name = prefab.name;
        log.ForGameObject(gameObject).ForMethod().Verbose("About to call Register on: {0}", instance.name);
        instance.Register(builder);
        log.ForGameObject(gameObject).ForMethod().Verbose("Global plugin registered: {0}", instance.name);

        // Store plugin instances for PluginDiagnosisWindow
        StorePluginInstance(instance);
      }

      log.ForGameObject(gameObject).ForMethod().Verbose("Global plugins registration completed.");
    }

    /// <summary>
    ///   Registers plugins that implement IAsyncStartable as entry points for VContainer's UniTask integration.
    ///   This replaces manual async initialization with VContainer's automatic async startup.
    /// </summary>
    private void RegisterAsyncEntryPoints(IContainerBuilder builder)
    {
      if (builder == null)
        throw new ArgumentNullException(nameof(builder));

      if (globalPluginConfig?.GlobalPluginPrefabs == null || globalPluginConfig.GlobalPluginPrefabs.Count == 0)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("No global plugins to register as async entry points");
        return;
      }

      log.ForGameObject(gameObject).ForMethod().Debug("Registering async entry points for global plugins");

      foreach (AbstractGamePlugin prefab in globalPluginConfig.GlobalPluginPrefabs)
      {
        if (prefab == null)
          continue;

        // Check if this plugin implements IAsyncStartable
        if (prefab is IAsyncStartable)
        {
          // Get the existing instance (plugins are already instantiated in RegisterGlobalPlugins)
          AbstractGamePlugin existingInstance = GetExistingPluginInstance(prefab);

          if (existingInstance != null && existingInstance is IAsyncStartable)
          {
            // Register the instance as an entry point - VContainer will automatically call StartAsync
            builder.RegisterEntryPoint(resolver => existingInstance as IAsyncStartable, Lifetime.Singleton);
            log.ForGameObject(gameObject).ForMethod().Debug("Registered {0} as async entry point", existingInstance.name);
          }
        }
      }

      log.ForGameObject(gameObject).ForMethod().Verbose("Async entry points registration completed");
    }

    /// <summary>
    ///   Gets an existing plugin instance of the specified type.
    /// </summary>
    /// <param name="prefab">The plugin prefab to find an existing instance for</param>
    /// <returns>The existing plugin instance, or null if none exists</returns>
    private AbstractGamePlugin GetExistingPluginInstance(AbstractGamePlugin prefab)
    {
      // Special handling for NavigationPlugin in bootstrapper scene
      if (prefab is NavigationPlugin)
      {
        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.buildIndex == 0)
          return null; // Skip NavigationPlugin in bootstrapper scene
      }

      // For persistent plugins, check stored reference first (they survive scene changes)
      AbstractGamePlugin persistentInstance = GetStoredPluginInstance(prefab);
      if (persistentInstance != null && persistentInstance.gameObject != null)
        return persistentInstance;

      // For all plugins, try to find anywhere (FindFirstObjectByType searches all loaded objects including DontDestroyOnLoad)
      AbstractGamePlugin sceneInstance = FindFirstObjectByType(prefab.GetType()) as AbstractGamePlugin;
      if (sceneInstance != null)
        return sceneInstance;

      return null;
    }

    /// <summary>
    ///   Gets stored plugin instance for backward compatibility.
    /// </summary>
    private AbstractGamePlugin GetStoredPluginInstance(AbstractGamePlugin prefab)
    {
      return prefab switch
      {
        ES3GameSavePlugin => GameSavePluginInstance,
        NavigationPlugin => NavigationPluginInstance,
        SettingsPlugin => SettingsPluginInstance,
        ErrorSystemPlugin => ErrorSystemPluginInstance,
        _ => null
      };
    }

    /// <summary>
    ///   Determines if a plugin should be skipped during instantiation because it already exists.
    ///   This prevents duplicate instantiation of persistent plugins like ES3GameSavePlugin.
    /// </summary>
    /// <param name="prefab">The plugin prefab to check</param>
    /// <returns>True if the plugin should be skipped, false if it should be instantiated</returns>
    private bool ShouldSkipPluginInstantiation(AbstractGamePlugin prefab)
    {
      // Special handling for NavigationPlugin in bootstrapper scene
      if (prefab is NavigationPlugin)
      {
        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.buildIndex == 0)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("NavigationPlugin not needed in bootstrapper scene (index 0), skipping entirely");
          return true;
        }
      }

      // Special handling for ES3GameSavePlugin (persistent)
      if (prefab is ES3GameSavePlugin)
      {
        ES3GameSavePlugin existingGameSavePlugin = FindFirstObjectByType<ES3GameSavePlugin>();
        if (existingGameSavePlugin != null)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("ES3GameSavePlugin already exists in scene, skipping instantiation but ensuring initialization");
          GameSavePluginInstance = existingGameSavePlugin;
          return true;
        }

        if (GameSavePluginInstance != null && GameSavePluginInstance.gameObject != null)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("ES3GameSavePlugin already exists (stored reference), skipping instantiation");
          return true;
        }
        return false;
      }

      // Special handling for ErrorSystemPlugin (persistent)
      if (prefab is ErrorSystemPlugin)
      {
        // First, check if we have a stored reference
        if (ErrorSystemPluginInstance != null && ErrorSystemPluginInstance.gameObject != null)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("ErrorSystemPlugin already exists (stored reference), skipping instantiation");
          return true;
        }

        // Then check if it exists in the scene
        ErrorSystemPlugin existingErrorSystemPlugin = FindFirstObjectByType<ErrorSystemPlugin>();
        if (existingErrorSystemPlugin != null)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("ErrorSystemPlugin already exists in scene, using existing instance");
          ErrorSystemPluginInstance = existingErrorSystemPlugin;
          return true;
        }

        return false;
      }

      // For all other plugin types (including persistent plugins that use DontDestroyOnLoad),
      // check if any instance of this type already exists
      Object existingInstance = FindFirstObjectByType(prefab.GetType());
      if (existingInstance != null)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("{0} already exists, skipping instantiation", prefab.GetType().Name);
        return true;
      }

      return false;
    }

    /// <summary>
    ///   Stores plugin instances for PluginDiagnosisWindow access.
    /// </summary>
    private void StorePluginInstance(AbstractGamePlugin instance)
    {
      switch (instance)
      {
        case NavigationPlugin navigationPlugin:
          NavigationPluginInstance = navigationPlugin;
          log.ForGameObject(gameObject).ForMethod().Verbose("Stored NavigationPlugin instance (scene-specific)");
          break;
        case SettingsPlugin settingsPlugin:
          SettingsPluginInstance = settingsPlugin;
          log.ForGameObject(gameObject).ForMethod().Verbose("Stored SettingsPlugin instance (scene-specific)");
          break;
        case ES3GameSavePlugin gameSavePlugin:
          GameSavePluginInstance = gameSavePlugin;
          // Make GameSavePlugin persist between scenes
          DontDestroyOnLoad(gameSavePlugin.gameObject);
          log.ForGameObject(gameObject).ForMethod().Verbose("Stored GameSavePlugin instance (persistent)");
          break;
        case InputRebinderPlugin inputRebinderPlugin:
          InputRebinderPluginInstance = inputRebinderPlugin;
          DontDestroyOnLoad(inputRebinderPlugin.gameObject);
          log.ForGameObject(gameObject).ForMethod().Verbose("Stored InputRebinderPlugin instance");
          break;
        case AnalyticsPlugin analyticsPlugin:
          AnalyticsPluginInstance = analyticsPlugin;
          DontDestroyOnLoad(analyticsPlugin.gameObject);
          log.ForGameObject(gameObject).ForMethod().Verbose("Stored AnalyticsPlugin instance");
          break;
        case ErrorSystemPlugin errorSystemPlugin:
          ErrorSystemPluginInstance = errorSystemPlugin;
          DontDestroyOnLoad(errorSystemPlugin.gameObject);
          log.ForGameObject(gameObject).ForMethod().Verbose("Stored ErrorSystemPlugin instance (persistent)");
          break;
        default:
          log.ForGameObject(gameObject).ForMethod().Verbose("Stored {0} instance (generic)", instance.GetType().Name);
          break;
      }
    }

    /// <summary>
    ///   Refreshes plugin instance references by finding them in the current scene.
    ///   This is needed because scene-specific plugin GameObjects are destroyed when scenes change,
    ///   but GlobalInstaller persists via DontDestroyOnLoad.
    ///   GameSavePlugin is persistent and doesn't need refreshing.
    /// </summary>
    public void RefreshPluginReferences()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Refreshing global plugin references");

      // Find NavigationPlugin in scene (scene-specific)
      NavigationPlugin navigationPlugin = FindFirstObjectByType<NavigationPlugin>();
      if (navigationPlugin != null)
      {
        NavigationPluginInstance = navigationPlugin;
        log.ForGameObject(gameObject).ForMethod().Verbose("Refreshed NavigationPlugin reference");

        // NavigationPlugin is now handled by RegisterEntryPoint - no manual initialization needed

        // Check if we need to hide canvases based on current scene
        // This handles the case where NavigationPlugin is created after scene load
        CheckAndApplyNavigationCanvasVisibility(navigationPlugin);
      }
      else
      {
        NavigationPluginInstance = null;
        log.ForGameObject(gameObject).ForMethod().Verbose("NavigationPlugin not found in scene - this is normal for scenes that don't need navigation");
      }

      // Find SettingsPlugin in scene (scene-specific)
      SettingsPlugin settingsPlugin = FindFirstObjectByType<SettingsPlugin>();
      if (settingsPlugin != null)
      {
        SettingsPluginInstance = settingsPlugin;
        DontDestroyOnLoad(settingsPlugin.gameObject);
        log.ForGameObject(gameObject).ForMethod().Verbose("Refreshed SettingsPlugin reference");
      }
      else
      {
        SettingsPluginInstance = null;
        log.ForGameObject(gameObject).ForMethod().Verbose("SettingsPlugin not found in scene");
      }

      // GameSavePlugin is persistent - check if it's still valid
      if (GameSavePluginInstance != null && GameSavePluginInstance.gameObject != null)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("GameSavePlugin reference is still valid (persistent)");
      }
      else
      {
        // Try to find it in case it was recreated
        ES3GameSavePlugin gameSavePlugin = FindFirstObjectByType<ES3GameSavePlugin>();
        if (gameSavePlugin != null)
        {
          GameSavePluginInstance = gameSavePlugin;
          DontDestroyOnLoad(gameSavePlugin.gameObject);
          log.ForGameObject(gameObject).ForMethod().Verbose("Found and refreshed GameSavePlugin reference");
        }
        else
        {
          GameSavePluginInstance = null;
          log.ForGameObject(gameObject).ForMethod().Warning("GameSavePlugin not found anywhere");
        }
      }

      // ErrorSystemPlugin is persistent - check if it's still valid
      if (ErrorSystemPluginInstance != null && ErrorSystemPluginInstance.gameObject != null)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("ErrorSystemPlugin reference is still valid (persistent)");
      }
      else
      {
        // Try to find it in case it was recreated
        ErrorSystemPlugin errorSystemPlugin = FindFirstObjectByType<ErrorSystemPlugin>();
        if (errorSystemPlugin != null)
        {
          ErrorSystemPluginInstance = errorSystemPlugin;
          DontDestroyOnLoad(errorSystemPlugin.gameObject);
          log.ForGameObject(gameObject).ForMethod().Verbose("Found and refreshed ErrorSystemPlugin reference");
        }
        else
        {
          ErrorSystemPluginInstance = null;
          log.ForGameObject(gameObject).ForMethod().Warning("ErrorSystemPlugin not found anywhere");
        }
      }
    }

    private void InitializeGlobalRuntimePlugins(IContainerBuilder builder)
    {
      if (builder == null)
        throw new ArgumentNullException(nameof(builder));

      log.ForGameObject(gameObject).ForMethod().Verbose("Received call to initialize global runtime plugins");

      // Initialize global runtime plugins after container is built
      builder.RegisterBuildCallback(resolver =>
      {
        // CRITICAL: Check if gameObject is still valid before accessing it
        // This prevents MissingReferenceException when the object is destroyed
        if (this == null || gameObject == null)
        {
          return; // Early exit if object is destroyed
        }

        try
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("Initializing global runtime plugins");

          // Initialize the GlobalAsyncMessageBroker
          GlobalAsyncMessageBroker.Initialize(resolver);
          log.ForGameObject(gameObject).ForMethod().Verbose("GlobalAsyncMessageBroker initialized");

          if (globalPluginConfig?.GlobalPluginPrefabs != null)
            foreach (AbstractGamePlugin plugin in globalPluginConfig.GlobalPluginPrefabs)
              if (plugin is IRuntimePlugin runtimePlugin)
              {
                // Skip plugins that implement IAsyncStartable - they're handled by RegisterEntryPoint
                if (plugin is IAsyncStartable)
                {
                  log.ForGameObject(gameObject).ForMethod().Verbose("Skipping {0} initialization - handled by RegisterEntryPoint", plugin.name);
                  continue;
                }

                runtimePlugin.Initialize(resolver);
                log.ForGameObject(gameObject).ForMethod().Verbose("Initialized global runtime plugin: {0}", plugin.name);
              }

          log.ForGameObject(gameObject).ForMethod().Verbose("Global runtime plugins initialization completed");
        }
        catch (MissingReferenceException)
        {
          // Silently ignore if object was destroyed during async operation
          return;
        }
      });

      log.ForGameObject(gameObject).ForMethod().Verbose("GlobalInstaller configuration completed");

      // Mark initial setup as complete
      isInitialSetup = false;
    }

    /// <summary>
    ///   Called when a new scene is loaded. Refreshes scene-specific plugin references.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
      if (isInitialSetup)
        // Skip the first scene load (initial setup)
        return;

      log.ForGameObject(gameObject).ForMethod().Information("=== Scene loaded: {0} (path: {1}) ===", scene.name, scene.path ?? "null");
      RefreshPluginReferences();

      // Ensure scene-specific plugins are properly initialized
      InitializeScenePlugins(scene);

      // Handle navigation canvas visibility based on scene type
      // Use a coroutine to delay slightly so NavigationPlugin has time to be created/initialized
      HandleNavigationCanvasVisibilityDelayed(scene);

      GlobalAsyncMessageBroker.Publish(new SceneLoadedMessage(scene.name));
    }

    /// <summary>
    ///   Hides navigation canvases when gameplay scene is loaded, shows them when menu scene is loaded.
    ///   Uses a delayed check to ensure NavigationPlugin has been created/initialized first.
    /// </summary>
    private void HandleNavigationCanvasVisibilityDelayed(Scene loadedScene)
    {
      // Delay slightly to ensure NavigationPlugin is created and initialized
      HandleNavigationCanvasVisibilityDelayedAsync(loadedScene).Forget();
    }

    private async UniTaskVoid HandleNavigationCanvasVisibilityDelayedAsync(Scene loadedScene)
    {
      // Wait a few frames to ensure NavigationPlugin is created
      await UniTask.NextFrame();
      await UniTask.NextFrame();

      // Try to find NavigationPlugin after it's been created
      RefreshPluginReferences();

      // If NavigationPlugin still doesn't exist, wait a bit more and try again
      // (handles async plugin instantiation)
      if (NavigationPluginInstance == null)
      {
        await UniTask.Delay(100); // Wait 100ms for async plugin creation
        RefreshPluginReferences();
      }

      // Now handle visibility
      HandleNavigationCanvasVisibility(loadedScene);
    }

    /// <summary>
    ///   Checks if navigation canvases should be hidden/shown based on current scene and applies it.
    ///   This is called when NavigationPlugin is found/refreshed to ensure canvases are hidden in gameplay scenes.
    /// </summary>
    private void CheckAndApplyNavigationCanvasVisibility(NavigationPlugin navigationPlugin)
    {
      if (navigationPlugin == null || navigationPlugin.gameObject == null)
        return;

      // Get scene references from GlobalConstants
      GlobalConstantsConfigAsset globalConstants = GlobalConstantsHelper.Instance?.GlobalConstantsConfig;
      if (globalConstants == null)
        return;

      Scene currentScene = SceneManager.GetActiveScene();
      AssetReferenceScene gameplaySceneRef = globalConstants.GameplaySceneReference;
      AssetReferenceScene menuSceneRef = globalConstants.MenuSceneReference;

      bool isGameplayScene = IsSceneMatch(gameplaySceneRef, currentScene);
      bool isMenuScene = IsSceneMatch(menuSceneRef, currentScene);

      if (isGameplayScene)
      {
        log.ForGameObject(gameObject).ForMethod().Information("NavigationPlugin found in gameplay scene, hiding canvases");
        SetNavigationCanvasesVisibility(navigationPlugin, false);
      }
      else if (isMenuScene)
      {
        log.ForGameObject(gameObject).ForMethod().Information("NavigationPlugin found in menu scene, showing canvases");
        SetNavigationCanvasesVisibility(navigationPlugin, true);
      }
      else
      {
        // Fallback: if we can't match the scene but menu scene reference is valid, hide by default
        if (menuSceneRef != null && menuSceneRef.RuntimeKeyIsValid())
        {
          log.ForGameObject(gameObject).ForMethod().Warning("NavigationPlugin found but scene matching failed - hiding canvases as fallback");
          SetNavigationCanvasesVisibility(navigationPlugin, false);
        }
      }
    }

    /// <summary>
    ///   Hides navigation canvases when gameplay scene is loaded, shows them when menu scene is loaded.
    /// </summary>
    private void HandleNavigationCanvasVisibility(Scene loadedScene)
    {
      // Find NavigationPlugin - try stored reference first, then search in scene
      NavigationPlugin navigationPlugin = NavigationPluginInstance;
      if (navigationPlugin == null || navigationPlugin.gameObject == null)
      {
        // Try to find it in the scene or DontDestroyOnLoad
        navigationPlugin = FindFirstObjectByType<NavigationPlugin>();
        if (navigationPlugin != null)
        {
          NavigationPluginInstance = navigationPlugin;
          log.ForGameObject(gameObject).ForMethod().Debug("Found NavigationPlugin via FindFirstObjectByType");
        }
      }

      if (navigationPlugin == null || navigationPlugin.gameObject == null)
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("NavigationPlugin not found, skipping canvas visibility handling");
        return;
      }

      // Get scene references from GlobalConstants
      GlobalConstantsConfigAsset globalConstants = GlobalConstantsHelper.Instance?.GlobalConstantsConfig;
      if (globalConstants == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("GlobalConstantsConfig not available, cannot determine scene type");
        return;
      }

      AssetReferenceScene gameplaySceneRef = globalConstants.GameplaySceneReference;
      AssetReferenceScene menuSceneRef = globalConstants.MenuSceneReference;

      // Check if this is the gameplay scene
      bool isGameplayScene = IsSceneMatch(gameplaySceneRef, loadedScene);
      // Check if this is the menu scene
      bool isMenuScene = IsSceneMatch(menuSceneRef, loadedScene);

      log.ForGameObject(gameObject).ForMethod().Debug("Scene loaded: {0}, isGameplayScene: {1}, isMenuScene: {2}", loadedScene.name, isGameplayScene, isMenuScene);

      if (isGameplayScene)
      {
        log.ForGameObject(gameObject).ForMethod().Information("Gameplay scene loaded, hiding navigation canvases");
        SetNavigationCanvasesVisibility(navigationPlugin, false);
      }
      else if (isMenuScene)
      {
        log.ForGameObject(gameObject).ForMethod().Information("Menu scene loaded, showing navigation canvases");
        SetNavigationCanvasesVisibility(navigationPlugin, true);
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Warning("Scene {0} is neither gameplay nor menu scene, leaving navigation canvases as-is", loadedScene.name);
        // Debug: log what the scene references are for troubleshooting
        if (gameplaySceneRef != null && gameplaySceneRef.RuntimeKeyIsValid())
          log.ForGameObject(gameObject).ForMethod().Warning("GameplaySceneReference RuntimeKey: {0}", gameplaySceneRef.RuntimeKey);
        else
          log.ForGameObject(gameObject).ForMethod().Warning("GameplaySceneReference is null or invalid");
        if (menuSceneRef != null && menuSceneRef.RuntimeKeyIsValid())
          log.ForGameObject(gameObject).ForMethod().Warning("MenuSceneReference RuntimeKey: {0}", menuSceneRef.RuntimeKey);
        else
          log.ForGameObject(gameObject).ForMethod().Warning("MenuSceneReference is null or invalid");
        log.ForGameObject(gameObject).ForMethod().Warning("Loaded scene name: {0}, path: {1}", loadedScene.name, loadedScene.path);

        // Fallback: If scene matching failed, assume it's gameplay if it's not the menu scene
        // This is a safety fallback - we'll hide canvases by default unless we're sure it's the menu scene
        if (menuSceneRef != null && menuSceneRef.RuntimeKeyIsValid())
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Scene matching failed - assuming gameplay scene and hiding navigation canvases as fallback");
          SetNavigationCanvasesVisibility(navigationPlugin, false);
        }
      }
    }

    /// <summary>
    ///   Checks if a loaded scene matches an AssetReferenceScene by comparing scene names/paths.
    ///   Handles GUID-based RuntimeKeys by resolving them through Addressables.
    /// </summary>
    private bool IsSceneMatch(AssetReferenceScene sceneRef, Scene loadedScene)
    {
      if (sceneRef == null || !sceneRef.RuntimeKeyIsValid())
      {
        log.ForGameObject(gameObject).ForMethod().Debug("SceneRef is null or invalid");
        return false;
      }

      // Get the runtime key (could be GUID, address, or label)
      object runtimeKey = sceneRef.RuntimeKey;
      string sceneKey = runtimeKey?.ToString() ?? string.Empty;

      log.ForGameObject(gameObject).ForMethod().Debug("Comparing scene - RuntimeKey: '{0}', Loaded scene name: '{1}', path: '{2}'",
        sceneKey, loadedScene.name, loadedScene.path ?? "null");

      // If RuntimeKey looks like a GUID (32 hex characters), resolve it through Addressables
      // GUID format: exactly 32 hexadecimal characters
      bool looksLikeGUID = sceneKey.Length == 32 && Regex.IsMatch(sceneKey, "^[0-9a-fA-F]{32}$");

      if (looksLikeGUID)
      {
        // Resolve GUID through Addressables to get the actual scene path
#if USE_ADDRESSABLES
        try
        {
          AsyncOperationHandle<IList<IResourceLocation>> locationHandle = Addressables.LoadResourceLocationsAsync(sceneKey);
          if (locationHandle.IsDone && locationHandle.Status == AsyncOperationStatus.Succeeded)
          {
            IList<IResourceLocation> locations = locationHandle.Result;
            foreach (IResourceLocation location in locations)
              if (location != null)
              {
                // Check InternalId which should contain the asset path
                string internalId = location.InternalId;
                if (!string.IsNullOrEmpty(internalId))
                {
                  // Extract scene name from path (e.g., "Assets/_Template/Data/Scenes/Menu.unity" -> "Menu")
                  string sceneNameFromPath = Path.GetFileNameWithoutExtension(internalId);

                  // Compare with loaded scene name
                  if (string.Equals(sceneNameFromPath, loadedScene.name, StringComparison.OrdinalIgnoreCase))
                  {
                    log.ForGameObject(gameObject).ForMethod().Debug("Scene match found via GUID resolution: '{0}' -> '{1}'", sceneKey, internalId);
                    return true;
                  }

                  // Also compare with full path if available
                  if (!string.IsNullOrEmpty(loadedScene.path))
                  {
                    // Normalize paths for comparison (remove leading/trailing slashes, handle different separators)
                    string normalizedInternalId = internalId.Replace('\\', '/').Trim('/');
                    string normalizedLoadedPath = loadedScene.path.Replace('\\', '/').Trim('/');

                    if (string.Equals(normalizedInternalId, normalizedLoadedPath, StringComparison.OrdinalIgnoreCase))
                    {
                      log.ForGameObject(gameObject).ForMethod().Debug("Scene match found via GUID resolution (full path): '{0}' -> '{1}'", sceneKey, internalId);
                      return true;
                    }
                  }
                }
              }
          }
        }
        catch (InvalidKeyException ex)
        {
          // Expected in test environments where Addressables catalogs may not be initialized
          // Silently handle - this is not an error condition
          log.ForGameObject(gameObject).ForMethod().Verbose("Addressable key not found for GUID {0} (expected in test environments): {1}", sceneKey, ex.Message);
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("Error resolving Addressable location for GUID {0}: {1}", sceneKey, ex.Message);
        }
#else
        // In non-Addressables builds, try to resolve GUID directly using Unity's AssetDatabase (editor only)
#if UNITY_EDITOR
        try
        {
          string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(sceneKey);
          if (!string.IsNullOrEmpty(assetPath))
          {
            string sceneNameFromPath = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (string.Equals(sceneNameFromPath, loadedScene.name, StringComparison.OrdinalIgnoreCase))
            {
              log.ForGameObject(gameObject).ForMethod().Debug("Scene match found via GUID (editor): '{0}' -> '{1}'", sceneKey, assetPath);
              return true;
            }
          }
        }
        catch (Exception ex)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("Error resolving GUID via AssetDatabase: {0}", ex.Message);
        }
#endif
#endif
      }
      else
      {
        // RuntimeKey is not a GUID, try direct comparison
        // Compare with loaded scene name (most reliable)
        if (string.Equals(sceneKey, loadedScene.name, StringComparison.OrdinalIgnoreCase))
        {
          log.ForGameObject(gameObject).ForMethod().Debug("Scene match found by name: '{0}'", loadedScene.name);
          return true;
        }

        // Also check scene path if available
        if (!string.IsNullOrEmpty(loadedScene.path))
        {
          string scenePath = loadedScene.path;
          // Extract scene name from path (e.g., "Assets/Scenes/GameplayScene.unity" -> "GameplayScene")
          string sceneNameFromPath = Path.GetFileNameWithoutExtension(scenePath);
          if (string.Equals(sceneKey, sceneNameFromPath, StringComparison.OrdinalIgnoreCase))
          {
            log.ForGameObject(gameObject).ForMethod().Debug("Scene match found by path name: '{0}'", sceneNameFromPath);
            return true;
          }

          // Also compare with full path
          if (string.Equals(sceneKey, scenePath, StringComparison.OrdinalIgnoreCase))
          {
            log.ForGameObject(gameObject).ForMethod().Debug("Scene match found by full path: '{0}'", scenePath);
            return true;
          }
        }
      }

      log.ForGameObject(gameObject).ForMethod().Debug("No scene match found for RuntimeKey: '{0}'", sceneKey);
      return false;
    }

    /// <summary>
    ///   Sets the visibility of all navigation canvas GameObjects.
    /// </summary>
    private void SetNavigationCanvasesVisibility(NavigationPlugin navigationPlugin, bool visible)
    {
      if (navigationPlugin == null || navigationPlugin.gameObject == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("NavigationPlugin is null, cannot set canvas visibility");
        return;
      }

      // Hide/show the main canvas children (MainCanvas, AlertCanvas, OverlayCanvas)
      // These are direct children of the NavigationPlugin GameObject
      Transform pluginTransform = navigationPlugin.transform;

      int canvasCount = 0;
      for (int i = 0; i < pluginTransform.childCount; i++)
      {
        Transform child = pluginTransform.GetChild(i);
        string childName = child.name;

        // Hide/show canvas GameObjects (MainCanvas, AlertCanvas, OverlayCanvas, etc.)
        if (childName.Contains("Canvas", StringComparison.OrdinalIgnoreCase))
        {
          child.gameObject.SetActive(visible);
          canvasCount++;
          log.ForGameObject(gameObject).ForMethod().Debug("{0} navigation canvas {1} (active: {2})", visible ? "Showed" : "Hid", childName, child.gameObject.activeSelf);
        }
      }

      if (canvasCount == 0)
        log.ForGameObject(gameObject).ForMethod().Warning("No canvas children found in NavigationPlugin to hide/show");
      else
        log.ForGameObject(gameObject).ForMethod().Information("Set visibility for {0} navigation canvas(es) to {1}", canvasCount, visible);
    }

    /// <summary>
    ///   Initializes scene-specific plugins that may exist in the loaded scene.
    ///   This ensures plugins like NavigationPlugin are properly instantiated, registered, and started
    ///   even when they were skipped during bootstrapper initialization (scene index 0).
    /// </summary>
    private void InitializeScenePlugins(Scene scene)
    {
      try
      {
        // Skip if we're still in bootstrapper scene
        if (scene.buildIndex == 0)
          return;

        IObjectResolver resolver = Container;
        if (resolver == null)
        {
          log.ForGameObject(gameObject).ForMethod().Warning("Container not available for scene plugin initialization");
          return;
        }

        // Check if we need to instantiate plugins that were skipped during bootstrapper initialization
        if (globalPluginConfig?.GlobalPluginPrefabs == null)
        {
          log.ForGameObject(gameObject).ForMethod().Verbose("No global plugin config available");
          return;
        }

        // First, check for plugins that exist in the scene
        List<AbstractGamePlugin> scenePlugins = FindObjectsByType<AbstractGamePlugin>(FindObjectsSortMode.None)
          .Where(p => p.gameObject.scene == scene && p is IAsyncStartable)
          .ToList();

        // Then, check for plugins from GlobalPluginConfig that should be instantiated now
        // (they were skipped in bootstrapper scene but are needed in this scene)
        foreach (AbstractGamePlugin prefab in globalPluginConfig.GlobalPluginPrefabs)
        {
          if (prefab == null || !(prefab is IAsyncStartable))
            continue;

          // Check if this plugin was supposed to be instantiated but was skipped
          // (e.g., NavigationPlugin in bootstrapper scene)
          bool shouldInstantiate = ShouldInstantiatePluginNow(prefab, scene);

          if (shouldInstantiate)
          {
            // Check if it already exists in the scene
            AbstractGamePlugin existingInScene = scenePlugins.FirstOrDefault(p => p.GetType() == prefab.GetType());
            if ((object)existingInScene == null)
            {
              log.ForGameObject(gameObject).ForMethod().Information("Instantiating {0} for scene {1} (was skipped during bootstrapper)", prefab.name, scene.name);

              // Instantiate the plugin
              AbstractGamePlugin instance = Instantiate(prefab);
              instance.name = prefab.name;

              // Register and start it
              if (instance is IAsyncStartable asyncStartable)
              {
                RegisterAndStartScenePlugin(instance, asyncStartable, resolver).Forget();
                scenePlugins.Add(instance);
              }
            }
          }
        }

        // Also handle any plugins that exist in the scene but aren't registered
        foreach (AbstractGamePlugin plugin in scenePlugins)
          if (plugin is IAsyncStartable asyncStartable)
          {
            bool isRegistered = IsPluginRegistered(plugin, resolver);

            if (!isRegistered)
            {
              log.ForGameObject(gameObject).ForMethod().Information("Found {0} in scene {1} that needs registration and initialization", plugin.GetType().Name, scene.name);
              RegisterAndStartScenePlugin(plugin, asyncStartable, resolver).Forget();
            }
          }
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex, "Error initializing scene plugins for scene {0}", scene.name);
      }
    }

    /// <summary>
    ///   Determines if a plugin should be instantiated now for the current scene.
    ///   Returns true if the plugin was skipped during bootstrapper but should exist in this scene.
    /// </summary>
    private bool ShouldInstantiatePluginNow(AbstractGamePlugin prefab, Scene currentScene)
    {
      // NavigationPlugin was skipped in bootstrapper scene (index 0) but should exist in other scenes
      if (prefab is NavigationPlugin)
        return currentScene.buildIndex != 0;

      // Add other scene-specific plugin logic here as needed
      // For now, other plugins are handled normally
      return false;
    }

    /// <summary>
    ///   Checks if a plugin instance is already registered with the container.
    /// </summary>
    private bool IsPluginRegistered(AbstractGamePlugin plugin, IObjectResolver resolver)
    {
      try
      {
        // Try to resolve the plugin type - if it succeeds and matches, it's registered
        object resolved = resolver.Resolve(plugin.GetType());
        return (object)resolved == plugin;
      }
      catch (VContainerException)
      {
        // Not registered
        return false;
      }
    }

    /// <summary>
    ///   Registers and starts a scene plugin asynchronously by creating a child scope.
    /// </summary>
    private async UniTaskVoid RegisterAndStartScenePlugin(AbstractGamePlugin plugin, IAsyncStartable asyncStartable, IObjectResolver parentResolver)
    {
      try
      {
        log.ForGameObject(gameObject).ForMethod().Debug("Registering and starting {0} from scene", plugin.GetType().Name);

        // Create a child scope attached to the plugin's GameObject
        // This allows the plugin to register its services while accessing parent container services
        ScenePluginLifetimeScope childScope = plugin.gameObject.AddComponent<ScenePluginLifetimeScope>();
        childScope.SetPluginForConfiguration(plugin);

        // Wait for Unity to initialize the component and build the scope
        await UniTask.Yield();

        // Wait for the scope container to be built
        DateTime maxWaitTime = DateTime.UtcNow.AddSeconds(5);
        while (childScope.Container == null && DateTime.UtcNow < maxWaitTime)
          await UniTask.Yield();

        if (childScope.Container == null)
        {
          log.ForGameObject(gameObject).ForMethod().Error("Child scope for {0} failed to build", plugin.GetType().Name);
          return;
        }

        // Register as entry point so StartAsync gets called automatically
        // Since we can't use RegisterEntryPoint after build, we'll manually call StartAsync
        CancellationToken cancellationToken = childScope.GetCancellationTokenOnDestroy();
        await asyncStartable.StartAsync(cancellationToken);

        log.ForGameObject(gameObject).ForMethod().Information("{0} started successfully for scene", plugin.GetType().Name);
      }
      catch (Exception ex)
      {
        log.ForGameObject(gameObject).ForMethod().Error(ex, "Failed to register and start {0} from scene", plugin.GetType().Name);
      }
    }

    /// <summary>
    ///   Internal LifetimeScope class for scene-specific plugins.
    ///   Allows plugins in scenes to have their own container with access to parent services.
    /// </summary>
    private class ScenePluginLifetimeScope : LifetimeScope
    {
      private AbstractGamePlugin pluginToRegister;

      public void SetPluginForConfiguration(AbstractGamePlugin plugin)
      {
        pluginToRegister = plugin;
      }

      protected override void Configure(IContainerBuilder builder)
      {
        if (pluginToRegister != null)
        {
          // Register the plugin with its installer (registers its services)
          pluginToRegister.Register(builder);

          // Register the plugin instance itself
          builder.RegisterInstance(pluginToRegister).AsSelf().AsImplementedInterfaces();
        }
      }
    }

#if UNITY_EDITOR
    [ContextMenu("Context Error")]
    private void ContextError()
    {
      GlobalAsyncMessageBroker.Publish(new ErrorRequestMessage("An error occurred in the context of the current scene"));
    }

    [ContextMenu("Context Error Fatal")]
    private void ContextErrorFatal()
    {
      GlobalAsyncMessageBroker.Publish(new ErrorRequestMessage("An error occurred in the context of the current scene", true));
    }
#endif
  }
}