using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Settings.UI;
using MToolKit.Runtime.Settings.UI.Abstract;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Settings.Graphics
{
  public class GraphicsSettingsInitializer : AbstractSettingsInitializer
  {
    private const double MAX_TOLERANCE = 0.001f;
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GraphicsSettingsInitializer>().ForFeature("Settings.Graphics"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [SerializeField]
    [Required]
    private IntBoundElementDropdown resolutionDropdown;

    [SerializeField]
    [Required]
    private IntBoundElementDropdown qualityDropdown;

    [SerializeField]
    [Required]
    private BoolBoundToggle fullscreenToggle;

    [SerializeField]
    [Required]
    private BoolBoundToggle vsyncToggle;

    [SerializeField]
    [Required]
    private BoolBoundToggle disableCrtToggle;

    [SerializeField]
    [Required]
    private BoolBoundToggle disableBloomToggle;

    [Inject]
    private ISettingsSystem settingsController;

    public override async UniTask ConfigureAsync()
    {
      // Check if GraphicsSettings are available
      if (settingsController?.GraphicsSettings == null)
      {
        log.ForGameObject(gameObject).ForMethod().Warning("GraphicsSettings are not available");
        return;
      }

      resolutionDropdown.Bind(settingsController.GraphicsSettings.ResolutionIndex);
      qualityDropdown.Bind(settingsController.GraphicsSettings.QualityIndex);

      fullscreenToggle.Bind(settingsController.GraphicsSettings.Fullscreen);
      vsyncToggle.Bind(settingsController.GraphicsSettings.VerticalSync);

      // [Required] is Odin editor-time only; guard the runtime path so an unwired toggle
      // degrades gracefully instead of throwing an NPE that aborts ConfigureAsync mid-method.
      if (disableCrtToggle != null)
        disableCrtToggle.Bind(settingsController.GraphicsSettings.DisableCrt);
      else
        log.ForGameObject(gameObject).ForMethod().Warning("disableCrtToggle is not wired; Disable CRT Effect toggle will not bind");

      if (disableBloomToggle != null)
        disableBloomToggle.Bind(settingsController.GraphicsSettings.DisableBloom);
      else
        log.ForGameObject(gameObject).ForMethod().Warning("disableBloomToggle is not wired; Disable Bloom toggle will not bind");

      fullscreenToggle.Value = Screen.fullScreen;
      vsyncToggle.Value = QualitySettings.vSyncCount > 0;

      // Populate resolution dropdown options
      Resolution[] resolutions = Screen.resolutions;
      List<string> resolutionOptions = new();
      int currentResolutionIndex = 0;
      for (int i = 0; i < resolutions.Length; i++)
      {
        Resolution res = resolutions[i];
        string option = $"{res.width}x{res.height} @ {res.refreshRateRatio}Hz";
        resolutionOptions.Add(option);
        // Find the currently active resolution.
        if (res.width == Screen.currentResolution.width &&
            res.height == Screen.currentResolution.height &&
            Math.Abs(res.refreshRateRatio.value - Screen.currentResolution.refreshRateRatio.value) < MAX_TOLERANCE)
          currentResolutionIndex = i;
      }
      resolutionDropdown.SetOptions(resolutionOptions);
      resolutionDropdown.Value = currentResolutionIndex;

      // Populate quality dropdown options
      string[] qualityNames = QualitySettings.names;
      List<string> qualityOptions = new(qualityNames);
      int currentQualityIndex = QualitySettings.GetQualityLevel();
      qualityDropdown.SetOptions(qualityOptions);
      qualityDropdown.Value = currentQualityIndex;

      await UniTask.CompletedTask;
    }
  }
}