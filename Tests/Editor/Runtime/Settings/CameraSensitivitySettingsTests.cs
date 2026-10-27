using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Settings;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Ini;
using MToolKit.Runtime.Settings.Input;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  public sealed class CameraSensitivitySettingsTests
  {
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void IndependentMultipliers_DefaultToOne_AndHonorApplyCancelAndConfirmedDefaults()
    {
      using var rebinder = new InputRebinderService();
      using var settings = new SettingsSystem(rebinder);
      var pan = Setting(settings.InputSettings, "CameraPanSensitivity");
      var zoom = Setting(settings.InputSettings, "CameraZoomSensitivity");
      Assert.That(pan.Default, Is.EqualTo(1f));
      Assert.That(zoom.Default, Is.EqualTo(1f));
      Assert.That(pan.Value, Is.EqualTo(1f));
      Assert.That(zoom.Value, Is.EqualTo(1f));

      pan.Value = 0.5f;
      Assert.That(zoom.Value, Is.EqualTo(1f), "Pan must not change zoom.");
      zoom.Value = 2.5f;
      Assert.That(settings.IsDirty.Value, Is.True);
      settings.Apply(autoFinish: true, gotoMenu: false);
      Assert.That(pan.LastValue, Is.EqualTo(0.5f));
      Assert.That(zoom.LastValue, Is.EqualTo(2.5f));
      Assert.That(settings.IsDirty.Value, Is.False);

      pan.Value = 0.8f;
      zoom.Value = 0.9f;
      settings.Cancel();
      Assert.That(pan.Value, Is.EqualTo(0.5f));
      Assert.That(zoom.Value, Is.EqualTo(2.5f));
      Assert.That(settings.IsDirty.Value, Is.False);

      settings.DefaultSettings();
      Assert.That(pan.Value, Is.EqualTo(1f));
      Assert.That(zoom.Value, Is.EqualTo(1f));
      Assert.That(pan.LastValue, Is.EqualTo(1f));
      Assert.That(zoom.LastValue, Is.EqualTo(1f));
      Assert.That(settings.IsDirty.Value, Is.False);
    }

    [TestCase(-2f, 0.1f)]
    [TestCase(8f, 3f)]
    [TestCase(float.NaN, 1f)]
    [TestCase(float.PositiveInfinity, 1f)]
    [TestCase(float.NegativeInfinity, 1f)]
    public void InvalidLiveValues_AreFiniteAndClamped(float supplied, float expected)
    {
      using var rebinder = new InputRebinderService();
      var module = new InputSettingsModule(rebinder);
      try
      {
        foreach (string name in new[] { "CameraPanSensitivity", "CameraZoomSensitivity" })
        {
          var setting = Setting(module, name);
          setting.Value = supplied;
          Assert.That(setting.Value, Is.EqualTo(expected).Within(0.0001f), name);
          Assert.That(float.IsNaN(setting.Value) || float.IsInfinity(setting.Value), Is.False, name);
        }
      }
      finally
      {
        module.OnShutdown();
      }
    }

    [Test]
    public async Task LegacyIniWithoutCameraKeys_SeedsNeutralDefaults()
    {
      using var fixture = new IniFixture("[Audio]\nMusicVolume=0.25\n");
      await fixture.Settings.Initialization.AsTask();
      Assert.That(Setting(fixture.Settings.InputSettings, "CameraPanSensitivity").Value, Is.EqualTo(1f));
      Assert.That(Setting(fixture.Settings.InputSettings, "CameraZoomSensitivity").Value, Is.EqualTo(1f));
      Assert.That(fixture.Ini.KeyExists("Input", "CameraPanSensitivity"), Is.True);
      Assert.That(fixture.Ini.KeyExists("Input", "CameraZoomSensitivity"), Is.True);
      Assert.That(fixture.Ini.GetValue<float>("Input", "CameraPanSensitivity", -1f), Is.EqualTo(1f));
      Assert.That(fixture.Ini.GetValue<float>("Input", "CameraZoomSensitivity", -1f), Is.EqualTo(1f));
      Assert.That(fixture.Settings.IsDirty.Value, Is.False);
    }

    [Test]
    public async Task ApplyAndConfirmedDefaults_SurviveRealIniAndFreshSettingsReload()
    {
      using var fixture = new IniFixture();
      await fixture.Settings.Initialization.AsTask();
      var pan = Setting(fixture.Settings.InputSettings, "CameraPanSensitivity");
      var zoom = Setting(fixture.Settings.InputSettings, "CameraZoomSensitivity");
      pan.Value = 0.4f;
      zoom.Value = 2.6f;
      fixture.Settings.Apply(autoFinish: true, gotoMenu: false);
      await fixture.WaitForWrites();
      await fixture.AssertReload(0.4f, 2.6f);

      string appliedFile = File.ReadAllText(fixture.Config.GetIniFilePath());
      pan.Value = 1.7f;
      zoom.Value = 0.7f;
      fixture.Settings.Cancel();
      Assert.That(File.ReadAllText(fixture.Config.GetIniFilePath()), Is.EqualTo(appliedFile),
        "Cancel must not persist the abandoned sensitivity draft.");
      Assert.That(pan.Value, Is.EqualTo(0.4f));
      Assert.That(zoom.Value, Is.EqualTo(2.6f));

      fixture.Settings.DefaultSettings();
      await fixture.WaitForWrites();
      await fixture.AssertReload(1f, 1f);
    }

    [TestCase("NaN", "-5", 1f, 0.1f)]
    [TestCase("not-a-number", "99", 1f, 3f)]
    public async Task InvalidPersistedValues_LoadSafeFiniteMultipliers(string pan, string zoom,
      float expectedPan, float expectedZoom)
    {
      using var fixture = new IniFixture($"[Input]\nCameraPanSensitivity={pan}\nCameraZoomSensitivity={zoom}\n");
      await fixture.Settings.Initialization.AsTask();
      var panSetting = Setting(fixture.Settings.InputSettings, "CameraPanSensitivity");
      var zoomSetting = Setting(fixture.Settings.InputSettings, "CameraZoomSensitivity");
      Assert.That(panSetting.Value, Is.EqualTo(expectedPan).Within(0.0001f));
      Assert.That(zoomSetting.Value, Is.EqualTo(expectedZoom).Within(0.0001f));
      Assert.That(panSetting.IsDirty, Is.False, "Loaded normalized values must be the cancellation baseline.");
      Assert.That(zoomSetting.IsDirty, Is.False);
      Assert.That(fixture.Settings.IsDirty.Value, Is.False);
    }

    private static ReactiveSetting<float> Setting(InputSettingsModule module, string name)
    {
      var property = typeof(InputSettingsModule).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
      Assert.That(property, Is.Not.Null, $"Input settings must expose {name}.");
      Assert.That(property.PropertyType, Is.EqualTo(typeof(ReactiveSetting<float>)),
        "Camera sensitivity must use the existing reactive settings lifecycle.");
      return (ReactiveSetting<float>)property.GetValue(module);
    }

    private sealed class IniFixture : IDisposable
    {
      private readonly InputRebinderService rebinder = new();
      public IniConfig Config { get; }
      public IniService Ini { get; }
      public SettingsSystem Settings { get; }

      public IniFixture(string content = null)
      {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp",
          "automation-scratch", "camera-sensitivity-settings", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        Config = ScriptableObject.CreateInstance<IniConfig>();
        typeof(IniConfig).GetField("iniDirectory", PrivateInstance).SetValue(Config, directory);
        typeof(IniConfig).GetField("iniFileName", PrivateInstance).SetValue(Config, "settings.ini");
        typeof(IniConfig).GetField("usePersistentDataPath", PrivateInstance).SetValue(Config, false);
        Assert.That(Config.GetIniFilePath(), Is.EqualTo(Path.Combine(directory, "settings.ini")));
        if (content != null) File.WriteAllText(Config.GetIniFilePath(), content);
        Ini = new IniService(Config);
        Settings = new SettingsSystem(rebinder, Ini);
      }

      public async Task WaitForWrites()
      {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (Ini.IsSaving.Value && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.That(Ini.IsSaving.Value, Is.False, "The isolated INI write did not complete.");
      }

      public async Task AssertReload(float pan, float zoom)
      {
        using var reader = new IniService(Config);
        using var newRebinder = new InputRebinderService();
        using var reloaded = new SettingsSystem(newRebinder, reader);
        await reloaded.Initialization.AsTask();
        Assert.That(reader.GetValue<float>("Input", "CameraPanSensitivity", -1f), Is.EqualTo(pan).Within(0.0001f));
        Assert.That(reader.GetValue<float>("Input", "CameraZoomSensitivity", -1f), Is.EqualTo(zoom).Within(0.0001f));
        var actualPan = Setting(reloaded.InputSettings, "CameraPanSensitivity");
        var actualZoom = Setting(reloaded.InputSettings, "CameraZoomSensitivity");
        Assert.That(actualPan.Value, Is.EqualTo(pan).Within(0.0001f));
        Assert.That(actualZoom.Value, Is.EqualTo(zoom).Within(0.0001f));
        Assert.That(actualPan.IsDirty, Is.False);
        Assert.That(actualZoom.IsDirty, Is.False);
        Assert.That(reloaded.IsDirty.Value, Is.False);
      }

      public void Dispose()
      {
        Settings.Dispose();
        rebinder.Dispose();
        Ini.Dispose();
        Object.DestroyImmediate(Config);
      }
    }
  }
}
