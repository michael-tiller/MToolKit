using MToolKit.Runtime.Input;
using MToolKit.Runtime.Settings;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Ini;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.UI;
using MToolKit.Runtime.Settings.UI.Abstract;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  /// <summary>
  ///   Guards the hand-written persistence plumbing for the "Disable CRT Effect" / "Disable Bloom"
  ///   graphics settings. Their INI keys are hand-listed at three sites (load, save, populate-defaults);
  ///   a misspelled or missing key compiles cleanly, so these tests encode WHY the keys matter: both
  ///   must default to effect-on (false) and survive the module lifecycle + defaults-populate round-trip.
  /// </summary>
  public class GraphicsSettingsPersistenceTests
  {
    [Test]
    public async Task GraphicsControls_ReflectHydratedValues_WithoutChangingSettings()
    {
      var root = new GameObject("graphics initialization test");
      root.SetActive(false);
      using var settings = new SettingsSystem(new InputRebinderService());
      try
      {
        var graphics = settings.GraphicsSettings;
        // Choose values different from the current engine state without changing it.
        graphics.Fullscreen.Value = !Screen.fullScreen;
        graphics.Fullscreen.OnApply();
        graphics.VerticalSync.Value = !(QualitySettings.vSyncCount > 0);
        graphics.VerticalSync.OnApply();
        if (Screen.resolutions.Length > 1)
        {
          int desktopIndex = System.Array.FindIndex(Screen.resolutions, res =>
            res.width == Screen.currentResolution.width && res.height == Screen.currentResolution.height &&
            System.Math.Abs(res.refreshRateRatio.value - Screen.currentResolution.refreshRateRatio.value) < 0.001);
          graphics.ResolutionIndex.Value = (System.Math.Max(desktopIndex, 0) + 1) % Screen.resolutions.Length;
          graphics.ResolutionIndex.OnApply();
        }
        if (QualitySettings.names.Length > 1)
        {
          graphics.QualityIndex.Value = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
          graphics.QualityIndex.OnApply();
        }
        settings.SetDirty(false);
        bool fullscreen = graphics.Fullscreen.Value;
        bool vsync = graphics.VerticalSync.Value;
        int quality = graphics.QualityIndex.Value;
        int resolution = graphics.ResolutionIndex.Value;

        var initializer = root.AddComponent<GraphicsSettingsInitializer>();
        SetPrivate(initializer, "settingsController", settings);
        var resolutionControl = MakeDropdown(root.transform);
        var qualityControl = MakeDropdown(root.transform);
        SetPrivate(initializer, "resolutionDropdown", resolutionControl);
        SetPrivate(initializer, "qualityDropdown", qualityControl);
        SetPrivate(initializer, "fullscreenToggle", MakeToggle(root.transform));
        SetPrivate(initializer, "vsyncToggle", MakeToggle(root.transform));
        SetPrivate(initializer, "disableCrtToggle", MakeToggle(root.transform));
        SetPrivate(initializer, "disableBloomToggle", MakeToggle(root.transform));

        await initializer.ConfigureAsync().AsTask();

        Assert.That(graphics.Fullscreen.Value, Is.EqualTo(fullscreen));
        Assert.That(graphics.VerticalSync.Value, Is.EqualTo(vsync));
        Assert.That(graphics.QualityIndex.Value, Is.EqualTo(quality));
        Assert.That(graphics.ResolutionIndex.Value, Is.EqualTo(resolution));
        if (Screen.resolutions.Length > 0)
          Assert.That(resolutionControl.Dropdown.value, Is.EqualTo(resolution));
        if (QualitySettings.names.Length > 0)
          Assert.That(qualityControl.Dropdown.value, Is.EqualTo(quality));
        Assert.That(settings.IsDirty.Value, Is.False, "Opening controls must not create a draft.");

        await initializer.ConfigureAsync().AsTask();
        Assert.That(graphics.ResolutionIndex.Value, Is.EqualTo(resolution),
          "Reopening the same controls must preserve the saved resolution.");
        Assert.That(graphics.QualityIndex.Value, Is.EqualTo(quality));
        Assert.That(graphics.Fullscreen.Value, Is.EqualTo(fullscreen));
        Assert.That(graphics.VerticalSync.Value, Is.EqualTo(vsync));
        Assert.That(settings.IsDirty.Value, Is.False, "Reopening controls must not create a draft.");
      }
      finally
      {
        Object.DestroyImmediate(root);
      }
    }

    [Test]
    public void Dropdown_ModelRefreshIsSilent_AndFollowingPlayerChoiceStillUpdatesSetting()
    {
      var root = new GameObject("dropdown binding test");
      root.SetActive(false);
      using var setting = new ReactiveSetting<int>(1, "Resolution");
      try
      {
        var bound = MakeDropdown(root.transform);
        bound.SetOptions(new[] { "one", "two", "three" });
        bound.Bind(setting);
        int notifications = 0;
        bound.Dropdown.onValueChanged.AddListener(_ => notifications++);

        setting.Value = 2;
        Assert.That(bound.Dropdown.value, Is.EqualTo(2));
        Assert.That(notifications, Is.Zero, "Model-to-view synchronization must not publish player input.");

        bound.Dropdown.value = 1;
        Assert.That(setting.Value, Is.EqualTo(1), "A player may select the previously displayed option.");
        Assert.That(notifications, Is.EqualTo(1));

        using var replacement = new ReactiveSetting<int>(0, "Resolution");
        bound.Bind(replacement);
        Assert.That(notifications, Is.EqualTo(1), "Rebinding must also be silent.");
        setting.Value = 2;
        Assert.That(bound.Dropdown.value, Is.Zero, "A stale model must not control the rebound dropdown.");
        bound.Dropdown.value = 2;
        Assert.That(replacement.Value, Is.EqualTo(2));
        Assert.That(notifications, Is.EqualTo(2));
      }
      finally
      {
        Object.DestroyImmediate(root);
      }
    }

    private static IntBoundElementDropdown MakeDropdown(Transform parent)
    {
      var go = new GameObject("dropdown", typeof(RectTransform));
      go.transform.SetParent(parent);
      var dropdown = go.AddComponent<TMP_Dropdown>();
      var bound = go.AddComponent<IntBoundElementDropdown>();
      SetPrivate(bound, "dropdown", dropdown);
      SetPrivate(bound, "label", MakeLabel(go.transform));
      return bound;
    }

    private static BoolBoundToggle MakeToggle(Transform parent)
    {
      var go = new GameObject("toggle", typeof(RectTransform));
      go.transform.SetParent(parent);
      var toggle = go.AddComponent<Toggle>();
      var bound = go.AddComponent<BoolBoundToggle>();
      SetPrivate(bound, "toggle", toggle);
      SetPrivate(bound, "label", MakeLabel(go.transform));
      return bound;
    }

    private static TextMeshProUGUI MakeLabel(Transform parent)
    {
      var go = new GameObject("label", typeof(RectTransform));
      go.transform.SetParent(parent);
      return go.AddComponent<TextMeshProUGUI>();
    }

    private static void SetPrivate(object target, string name, object value)
    {
      for (var type = target.GetType(); type != null; type = type.BaseType)
      {
        var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) continue;
        field.SetValue(target, value);
        return;
      }
      Assert.Fail($"Missing fixture field {name} on {target.GetType().Name}");
    }

    [Test]
    public void DisableCrtAndBloom_DefaultToFalse_AndHonorApplyCancelRevert()
    {
      GraphicsSettingsModule module = new();
      try
      {
        // Default false = effect ON, matching the shipped CRT feature (m_Active) and Bloom override.
        Assert.That(module.DisableCrt.Value, Is.False);
        Assert.That(module.DisableBloom.Value, Is.False);
        Assert.That(module.DisableCrt.IsDefault, Is.True);

        // Apply commits a dirty change: LastValue advances and the setting is no longer dirty.
        module.DisableCrt.Value = true;
        Assert.That(module.DisableCrt.IsDirty, Is.True);
        module.Apply();
        Assert.That(module.DisableCrt.IsDirty, Is.False);
        Assert.That(module.DisableCrt.LastValue, Is.True);

        // Cancel reverts an uncommitted change back to the last applied value.
        module.DisableBloom.Value = true;
        module.Cancel();
        Assert.That(module.DisableBloom.Value, Is.False);

        // Revert-to-default returns a committed non-default setting to its default (effect on).
        module.RevertToDefaultSettings();
        Assert.That(module.DisableCrt.Value, Is.False);
        Assert.That(module.DisableCrt.IsDefault, Is.True);
      }
      finally
      {
        module.OnShutdown();
      }
    }

    [Test]
    public void IniService_RoundTripsDisableKeys_AndPopulatesDefaults()
    {
      IniConfig config = ScriptableObject.CreateInstance<IniConfig>();
      IniService ini = new(config);
      SettingsSystem settings = new(new InputRebinderService());
      try
      {
        // Bool round-trip through the INI store (guards the Load/Save key names + bool handling).
        ini.SetValue("Graphics", "DisableCrt", true);
        ini.SetValue("Graphics", "DisableBloom", true);
        Assert.That(ini.GetValue<bool>("Graphics", "DisableCrt", false), Is.True);
        Assert.That(ini.GetValue<bool>("Graphics", "DisableBloom", false), Is.True);

        // The easy-to-miss third INI site: the defaults populator must seed both keys when absent,
        // at their effect-on default (false).
        IniService fresh = new(config);
        fresh.PopulateDefaultsFromSettingsSystem(settings);
        Assert.That(fresh.KeyExists("Graphics", "DisableCrt"), Is.True);
        Assert.That(fresh.KeyExists("Graphics", "DisableBloom"), Is.True);
        Assert.That(fresh.GetValue<bool>("Graphics", "DisableCrt", true), Is.False);
        Assert.That(fresh.GetValue<bool>("Graphics", "DisableBloom", true), Is.False);
      }
      finally
      {
        settings.Dispose();
        Object.DestroyImmediate(config);
      }
    }
  }
}
