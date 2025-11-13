using System;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Bootstrapper.Interfaces;
using MToolKit.Runtime.Core.Singletons;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.Slog;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;

#endif

namespace MToolKit.Runtime.Bootstrapper
{
  public class Bootstrapper : MonoBehaviour
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext(typeof(Bootstrapper)).ForFeature("Bootstrapper"));

    private static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private Slider progressBar;

    [SerializeField]
    [Required]
    private TextMeshProUGUI progressText;

#if ENABLE_INPUT_SYSTEM
    [SerializeField]
    private InputActionAsset inputActionAsset;
#endif

    [FormerlySerializedAs("PressAnyKeyText")]
    [SerializeField]
    private LocalizedString pressAnyKeyText;

    [FormerlySerializedAs("LoadingText")]
    [SerializeField]
    private LocalizedString loadingText;

    [FormerlySerializedAs("ErrorText")]
    [SerializeField]
    private LocalizedString errorText;

    [FormerlySerializedAs("PreparingText")]
    [SerializeField]
    private LocalizedString preparingText;

    public readonly ReactiveProperty<bool> IsBootstrapped = new();
    public readonly ReactiveProperty<bool> IsLoading = new();
#if ENABLE_INPUT_SYSTEM
    private InputAction anyKeyAction;
#endif
    private IDisposable bootstrapDisposable;
    private bool isPreloadingDependencies;

    // New properties for dependency preloading
    private bool nonUIDependenciesReady;
    private DateTime now;
    private bool userReadyToProceed;


    private void Start()
    {
      log.ForGameObject(gameObject).ForMethod().Verbose("Begin bootstrapper");
      DontDestroyOnLoad(gameObject);
      progressBar.gameObject.SetActive(false);

      // Initialize input system if available
#if ENABLE_INPUT_SYSTEM
      InitializeInputSystem();
#endif

      // Wait for localization first, then start the bootstrapper process
      WaitForLocalizationAndStartAsync().Forget();
    }

    private void LateUpdate()
    {
      // Fallback to legacy input if Input System is not available
#if ENABLE_INPUT_SYSTEM
      if (anyKeyAction == null && UnityEngine.Input.anyKey && !userReadyToProceed)
#else
      if (Input.anyKey && !userReadyToProceed)
#endif
      {
        log.ForMethod().Verbose("Using legacy input fallback");
        userReadyToProceed = true;
        CheckIfReadyToLoad();
      }
    }

    private void OnDestroy()
    {
      log.ForMethod().Verbose("Disposing of bootstrapper");
      bootstrapDisposable?.Dispose();

      // Clean up input system
#if ENABLE_INPUT_SYSTEM
      if (anyKeyAction != null)
      {
        anyKeyAction.performed -= OnAnyKeyPressed;
        anyKeyAction = null;
      }

      if (inputActionAsset != null)
        inputActionAsset.Disable();
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void InitializeInputSystem()
    {
      if (inputActionAsset == null)
      {
        log.ForMethod().Warning("InputActionAsset not assigned, falling back to legacy input");
        return;
      }

      try
      {
        InputActionMap uiActionMap = inputActionAsset.FindActionMap("UI");
        if (uiActionMap != null)
        {
          anyKeyAction = uiActionMap.FindAction("AnyKey");
          if (anyKeyAction != null)
          {
            anyKeyAction.performed += OnAnyKeyPressed;
            inputActionAsset.Enable();
            log.ForMethod().Debug("Input System initialized successfully");
          }
          else
          {
            log.ForMethod().Warning("AnyKey action not found in UI action map");
          }
        }
        else
        {
          log.ForMethod().Warning("UI action map not found in InputActionAsset");
        }
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to initialize Input System, falling back to legacy input");
      }
    }
#endif

    private async UniTask WaitForLocalizationAndStartAsync()
    {
      log.ForMethod().Verbose("Waiting for localization system to be ready");

      // Show preparing text while waiting
      progressText.SetText(GetLocalizedTextSafely(preparingText, "Preparing..."));

      // Wait for localization system to be ready
      LocalizationSystem localizationSystem = LocalizationSystem.Instance;
      if (localizationSystem != null)
      {
        if (!localizationSystem.IsInitialized)
        {
          log.ForMethod().Verbose("Waiting for LocalizationSystem to initialize");
          await UniTask.WaitUntil(() => localizationSystem.IsInitialized);
          log.ForMethod().Verbose("LocalizationSystem initialized successfully");
        }
        else
        {
          log.ForMethod().Verbose("LocalizationSystem already initialized");
        }
      }
      else
      {
        log.ForMethod().Warning("LocalizationSystem instance not found, proceeding anyway");
      }

      // Now that localization is ready, start the normal bootstrapper process
      log.ForMethod().Verbose("Localization ready, starting bootstrapper process");

      // Start preloading non-UI dependencies
      PreloadNonUIDependenciesAsync().Forget();

      // Get AutoLoad setting from GlobalConstants config
      bool autoLoad = GlobalConstants.Instance?.GlobalConstantsConfig?.AutoLoad ?? false;

      if (autoLoad)
      {
        userReadyToProceed = true;
        CheckIfReadyToLoad();
      }
      else
      {
        progressText.SetText(GetLocalizedTextSafely(pressAnyKeyText, "Press any key to continue..."));
      }
    }

    private async UniTask PreloadNonUIDependenciesAsync()
    {
      if (isPreloadingDependencies)
      {
        log.ForMethod().Verbose("Non-UI dependencies already preloading, ignoring request");
        return;
      }

      isPreloadingDependencies = true;
      log.ForMethod().Verbose("Starting non-UI dependency preloading");

      try
      {
        // Wait for non-UI required dependencies with timeout
        float timeout = GlobalConstants.Instance?.GlobalConstantsConfig?.BootstrapperTimeout ?? 5f;
        try
        {
          await WaitForNonUIRequiredDependenciesAsync().Timeout(TimeSpan.FromSeconds(timeout));
        }
        catch (TimeoutException)
        {
          log.ForMethod().Error("Non-UI required dependencies failed to initialize within {0} seconds", timeout);
          throw new Exception($"Non-UI required dependencies failed to initialize within {timeout} seconds");
        }

        nonUIDependenciesReady = true;
        log.ForMethod().Verbose("Non-UI dependencies preloaded successfully");
        CheckIfReadyToLoad();
      }
      catch (Exception e)
      {
        log.ForMethod().Error(e, "Failed to preload non-UI dependencies");
        ForceQuit();
      }
    }

    private void CheckIfReadyToLoad()
    {
      if (nonUIDependenciesReady && userReadyToProceed && !IsLoading.Value)
        LoadScene();
      else if (userReadyToProceed && !nonUIDependenciesReady)
        // User is ready but non-UI dependencies aren't - show preparing text
        progressText.SetText(GetLocalizedTextSafely(preparingText, "Preparing..."));
    }

    private void LoadScene()
    {
      if (IsLoading.Value)
      {
        log.ForMethod().Verbose("Scene load already in progress, ignoring request");
        return;
      }

      progressText.SetText(GetLocalizedTextSafely(loadingText, "Loading..."));
      now = DateTime.Now;
      IsLoading.Value = true;
      bootstrapDisposable = IsBootstrapped.Subscribe(OnBootstrapValueChangedHandler);

      // Get timeout from GlobalConstants config
      float timeout = GlobalConstants.Instance?.GlobalConstantsConfig?.BootstrapperTimeout ?? 5f;

      // Load manifest-driven content (scenes are loaded via IGameLoader from manifest.json)
      LoadSceneWithTimeout(timeout).Forget();
    }

    private void OnBootstrapValueChangedHandler(bool bootstrap)
    {
      if (bootstrap)
        OnBootstrappedAsync().Forget();
    }

    private async UniTask OnBootstrappedAsync()
    {
      progressBar.gameObject.SetActive(true);
      // Store reference early to avoid null reference in async context
      GameObject gameObjectRef = gameObject;

      // Show completion progress
      progressBar.value = 1.0f;
      progressText.SetText(string.Empty);

      // Manifest scenes are already loaded by IGameLoader (via LoadGameAssetsAsync),
      // so we just need to clean up the bootstrapper object.
      log.ForMethod().Information("Bootstrap completed in {0}ms", DateTime.Now.Subtract(now).TotalMilliseconds.ToString("F3"));

      // Delay slightly to show completion UI
      await UniTask.Delay(500);

      Destroy(gameObjectRef);
    }

#if ENABLE_INPUT_SYSTEM
    private void OnAnyKeyPressed(InputAction.CallbackContext context)
    {
      if (!userReadyToProceed)
      {
        log.ForMethod().Debug("AnyKey input received, user ready to proceed");
        userReadyToProceed = true;
        CheckIfReadyToLoad();
      }
    }
#endif

    public void OnBackgroundClicked()
    {
      userReadyToProceed = true;
      CheckIfReadyToLoad();
    }

    private async UniTask LoadSceneWithTimeout(float timeout = 10f)
    {
      try
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("Starting manifest-driven content load");

        // Non-UI dependencies should already be loaded, now load UI-affecting dependencies
        if (!nonUIDependenciesReady)
        {
          log.ForMethod().Warning("Non-UI dependencies not ready, waiting for them now");
          try
          {
            await WaitForNonUIRequiredDependenciesAsync().Timeout(TimeSpan.FromSeconds(timeout));
          }
          catch (TimeoutException)
          {
            log.ForMethod().Error("Non-UI dependencies failed to initialize within {0} seconds", timeout);
            ForceQuit();
            return;
          }
        }

        // UI-affecting dependencies (like NavigationPlugin) are now loaded after scene transition
        // by GlobalInstaller.OnSceneLoaded() to avoid interfering with bootstrapper
        log.ForMethod().Verbose("Skipping UI dependencies - will be loaded after scene transition");

        // Load game assets using IGameLoader
        await LoadGameAssetsAsync(timeout);

        // Handle optional dependencies with timeout (continue on failure)
        try
        {
          await WaitForOptionalDependencies().Timeout(TimeSpan.FromSeconds(timeout));
        }
        catch (TimeoutException)
        {
          log.ForMethod().Warning("Optional dependencies did not initialize in time, continuing anyway");
        }

        IsBootstrapped.Value = true;
      }
      catch (Exception e)
      {
        log.ForMethod().Error(e, "Required dependency failed or error loading manifest content.");
        ForceQuit();
        throw; // Re-throw the exception for required dependency failures
      }
    }

    /// <summary>
    ///   Loads game assets using IGameLoader from the DI container.
    /// </summary>
    private async UniTask LoadGameAssetsAsync(float timeout)
    {
      try
      {
        log.ForMethod().Information("Loading game assets using IGameLoader");

        // Get GlobalInstaller instance and its container
        GlobalInstaller globalInstaller = GlobalInstaller.Instance;
        if (globalInstaller == null)
        {
          log.ForMethod().Warning("GlobalInstaller not found, skipping game asset loading");
          return;
        }

        // Resolve IGameLoader from the container
        IGameLoader gameLoader = globalInstaller.Container.Resolve<IGameLoader>();
        log.ForMethod().Information("Resolved IGameLoader: {Type}", gameLoader.GetType().Name);

        // Load game assets with timeout
        log.ForMethod().Information("Calling LoadGameAsync with timeout: {Timeout}s", timeout);
        await gameLoader.LoadGameAsync().Timeout(TimeSpan.FromSeconds(timeout));

        log.ForMethod().Information("Game assets loaded successfully");
      }
      catch (TimeoutException ex)
      {
        log.ForMethod().Error(ex, "Game asset loading timed out after {0} seconds", timeout);
        // Don't throw - allow bootstrapper to continue
      }
      catch (Exception ex)
      {
        log.ForMethod().Error(ex, "Failed to load game assets");
        // Don't throw - allow bootstrapper to continue
      }
    }

    /// <summary>
    ///   Safely gets localized text with fallback
    /// </summary>
    private string GetLocalizedTextSafely(LocalizedString localizedString, string fallback)
    {
      try
      {
        return localizedString.GetLocalizedString();
      }
      catch (Exception ex)
      {
        log.ForMethod().Fatal(ex, "Failed to get localized text, using fallback: {0}", fallback);
        return fallback;
      }
    }

    private void ForceLoad(int sceneIndex)
    {
      IsLoading.Value = false;

      // Check if scene index is valid
      if (sceneIndex < 0 || sceneIndex >= SceneManager.sceneCountInBuildSettings)
      {
        log.ForMethod().Warning("Invalid scene index: {0}, calling ForceQuit", sceneIndex);
        ForceQuit();
        return;
      }

      log.ForMethod().Verbose("Force loading scene {0}", sceneIndex);
      progressText.SetText(GetLocalizedTextSafely(loadingText, "Loading..."));
    }

    private void ForceQuit()
    {
      log.ForMethod().Information("Halting execution.");
      progressText.SetText(GetLocalizedTextSafely(errorText, "Error occurred"));
      IsLoading.Value = false;

      // Don't actually quit during tests - just log the error
#if UNITY_INCLUDE_TESTS
      log.ForMethod().Warning("ForceQuit called during test - not actually quitting");
#else
#if UNITY_EDITOR
      EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
#endif
    }

    private async UniTask WaitForDependencies()
    {
      log.ForMethod().Verbose("Waiting for dependencies to initialize");

      // Wait for non-UI required dependencies
      await WaitForNonUIRequiredDependenciesAsync();

      // Wait for UI required dependencies
      await WaitForUIRequiredDependenciesAsync();

      // Wait for optional dependencies
      await WaitForOptionalDependencies();

      log.ForMethod().Verbose("All dependencies initialized");
    }

    private async UniTask WaitForNonUIRequiredDependenciesAsync()
    {
      log.ForMethod().Verbose("Checking non-UI required dependencies");

      // Wait for GlobalConstants to initialize (non-UI dependency)
      GlobalConstants globalConstants = GlobalConstants.Instance;
      if (globalConstants == null || !globalConstants.IsInitialized)
      {
        log.ForMethod().Verbose("Waiting for GlobalConstants to initialize");
        await UniTask.WaitUntil(() =>
        {
          GlobalConstants gc = GlobalConstants.Instance;
          return gc != null && gc.IsInitialized;
        });
        log.ForMethod().Verbose("GlobalConstants initialized successfully");
      }
      else
      {
        log.ForMethod().Verbose("GlobalConstants already initialized");
      }

      // Wait for SlogLoader to initialize (non-UI dependency)
      if (!SlogLoader.Initialized)
      {
        await UniTask.WaitUntil(() =>
        {
          // Only check for FlushSlogOnQuit if SlogLoader is not yet initialized
          if (SlogLoader.Initialized)
            return true;

          FlushSlogOnQuit flushObject = FindFirstObjectByType<FlushSlogOnQuit>();
          return flushObject != null && SlogLoader.Initialized;
        });

        log.ForMethod().Verbose("Slog initialized successfully");
      }
      else
      {
        log.ForMethod().Verbose("Slog already initialized");
      }

      // LocalizationSystem is already initialized before this method is called
      log.ForMethod().Verbose("Passed non-UI required dependencies");

      // Explicitly return completed task
      await UniTask.CompletedTask;
    }

    private async UniTask WaitForUIRequiredDependenciesAsync()
    {
      log.ForMethod().Verbose("Checking UI-required dependencies");

      // NavigationPlugin is not needed in the bootstrapper scene
      // It will be loaded in the target scene after transition
      // Skip all UI-affecting dependencies during bootstrapper phase

      log.ForMethod().Verbose("Skipping UI dependencies during bootstrapper phase - will load in target scene");

      // Explicitly return completed task
      await UniTask.CompletedTask;
    }

    private UniTask WaitForOptionalDependencies()
    {
      log.ForMethod().Verbose("Checking optional dependencies");

      log.ForMethod().Verbose("Passed optional dependencies");

      return UniTask.CompletedTask;
    }
  }
}