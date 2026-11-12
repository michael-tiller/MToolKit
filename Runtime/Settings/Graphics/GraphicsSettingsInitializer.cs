using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Settings.Interfaces;
using MToolKit.Runtime.Settings.UI;
using MToolKit.Runtime.Settings.UI.Abstract;
using MToolKit.Runtime.UI.Scaling;
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

    [SerializeField]
    private UserUiScaleSlider uiScaleSlider;

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

      // Set options before binding: opening the view must not turn the current
      // desktop mode into a new, potentially persisted window resolution.
      Resolution[] resolutions = Screen.resolutions;
      List<string> resolutionOptions = new();
      foreach (Resolution res in resolutions)
        resolutionOptions.Add($"{res.width}x{res.height} @ {res.refreshRateRatio}Hz");
      resolutionDropdown.SetOptions(resolutionOptions);
      qualityDropdown.SetOptions(QualitySettings.names);

      resolutionDropdown.Bind(settingsController.GraphicsSettings.ResolutionIndex);
      qualityDropdown.Bind(settingsController.GraphicsSettings.QualityIndex);
      if (uiScaleSlider != null) uiScaleSlider.Bind(settingsController.GraphicsSettings.UiScale);

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

      await UniTask.CompletedTask;
    }
  }
}
