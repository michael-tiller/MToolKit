using MToolKit.Runtime.Input;
using MToolKit.Runtime.Settings;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Ini;
using NUnit.Framework;
using UnityEngine;
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
