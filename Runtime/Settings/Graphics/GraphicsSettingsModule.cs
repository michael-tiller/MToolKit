using System;
using MToolKit.Runtime.Installer;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Interfaces;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Settings.Graphics
{
  [Serializable]
  public class GraphicsSettingsModule : ISettingsModule, IGraphicsSettings
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GraphicsSettingsModule>().ForFeature("Settings.Graphics"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    // Helper method to update the engine's resolution.
    private static void UpdateResolution(int resIndex, bool fullscreen)
    {
      if (resIndex >= 0 && resIndex < Screen.resolutions.Length)
      {
        Resolution res = Screen.resolutions[resIndex];
        Screen.SetResolution(res.width, res.height, fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed, res.refreshRateRatio);
      }
      else
      {
        log.ForMethod().Error("Resolution index out of range: " + resIndex);
      }
    }

    // Helper method to update the engine's quality setting.
    private static void UpdateQuality(int qualityLevel)
    {
      QualitySettings.SetQualityLevel(qualityLevel, true);
    }

    private static void UpdateVerticalSync(bool enabled)
    {
      QualitySettings.vSyncCount = enabled ? 1 : 0;
    }

    public GraphicsSettingsModule(ISettingsSystem settingsController = null)
    {
      ResolutionIndex = new ReactiveSetting<int>(0, "Resolution", settingsController);
      QualityIndex = new ReactiveSetting<int>(0, "Quality", settingsController);
      Fullscreen = new ReactiveSetting<bool>(true, "Fullscreen", settingsController);
      VerticalSync = new ReactiveSetting<bool>(true, "Vertical Sync", settingsController);
    }

    [ShowInInspector]
    [ReadOnly]
    public bool VSyncEnabled
    {
      get
      {
        if (GlobalInstaller.Instance != null && VerticalSync != null) return VerticalSync.Value;
        return false;
      }
    }

    [ShowInInspector]
    [ReadOnly]
    public bool FullscreenEnabled
    {
      get
      {
        if (GlobalInstaller.Instance != null && Fullscreen != null) return Fullscreen.Value;
        return false;
      }
    }

    [ShowInInspector]
    [ReadOnly]
    public int QualityIndexValue
    {
      get
      {
        if (GlobalInstaller.Instance != null && QualityIndex != null) return QualityIndex.Value;
        return 0;
      }
    }

    [ShowInInspector]
    [ReadOnly]
    public int ResolutionIndexValue
    {
      get
      {
        if (GlobalInstaller.Instance != null && ResolutionIndex != null) return ResolutionIndex.Value;
        return 0;
      }
    }

    public ReactiveSetting<int> ResolutionIndex { get; }
    public ReactiveSetting<int> QualityIndex { get; }
    public ReactiveSetting<bool> Fullscreen { get; }
    public ReactiveSetting<bool> VerticalSync { get; }

    public void OnShutdown()
    {
      ResolutionIndex.Dispose();
      QualityIndex.Dispose();
      Fullscreen.Dispose();
      VerticalSync.Dispose();
    }

    public void Apply()
    {
      if (ResolutionIndex.IsDirty || Fullscreen.IsDirty)
      {
        UpdateResolution(ResolutionIndex.Value, Fullscreen.Value);
        ResolutionIndex.OnApply();
        Fullscreen.OnApply();
      }

      if (QualityIndex.IsDirty)
      {
        UpdateQuality(QualityIndex.Value);
        QualityIndex.OnApply();
      }

      if (VerticalSync.IsDirty)
      {
        UpdateVerticalSync(VerticalSync.Value);
        VerticalSync.OnApply();
      }
    }

    public void RevertToDefaultSettings()
    {
      if (!ResolutionIndex.IsDefault)
      {
        ResolutionIndex.OnRevertToDefault();
        UpdateResolution(ResolutionIndex.Value, Fullscreen.Value);
      }

      if (!QualityIndex.IsDefault)
      {
        QualityIndex.OnRevertToDefault();
        UpdateQuality(QualityIndex.Value);
      }

      if (!Fullscreen.IsDefault) Fullscreen.OnRevertToDefault();
      if (!VerticalSync.IsDefault)
      {
        VerticalSync.OnRevertToDefault();
        UpdateVerticalSync(VerticalSync.Value);
      }
    }

    public void Cancel()
    {
      ResolutionIndex.OnCancel();
      QualityIndex.OnCancel();
      Fullscreen.OnCancel();
      VerticalSync.OnCancel();
    }
  }
}