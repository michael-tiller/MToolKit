using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Settings;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Ini;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  public sealed class UiScaleSettingsTests
  {
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string SceneStateKey = "MToolKit.UiScaleSettingsTests.SceneState";

    private static void PrepareEmptyPlayMode()
    {
      if (SessionState.GetString(SceneStateKey, "").Length != 0)
        throw new InvalidOperationException("Restore the previous UI scale test scene snapshot before starting another PlayMode proof.");
      for (int i = 0; i < SceneManager.sceneCount; i++)
      {
        var scene = SceneManager.GetSceneAt(i);
        if (scene.isDirty || string.IsNullOrEmpty(scene.path) && scene.rootCount > 0)
          throw new InvalidOperationException("Preserve unsaved scene before UI scale PlayMode proof.");
      }
      var setup = EditorSceneManager.GetSceneManagerSetup();
      var state = new SavedScenes
      {
        paths = setup.Select(scene => scene.path).ToArray(),
        loaded = setup.Select(scene => scene.isLoaded).ToArray(),
        active = setup.Select(scene => scene.isActive).ToArray(),
        startScene = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene)
      };
      SessionState.SetString(SceneStateKey, JsonUtility.ToJson(state));
      EditorSceneManager.playModeStartScene = null;
      EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
    }

    [UnityTearDown]
    public IEnumerator RestoreScenes()
    {
      string saved = SessionState.GetString(SceneStateKey, "");
      if (saved.Length == 0) yield break;
      try { if (Application.isPlaying) yield return new ExitPlayMode(); }
      finally
      {
        var state = JsonUtility.FromJson<SavedScenes>(saved);
        var setup = state.paths.Select((scene, index) => new SceneSetup
          { path = scene, isLoaded = state.loaded[index], isActive = state.active[index] }).Where(scene => !string.IsNullOrEmpty(scene.path)).ToArray();
        if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(state.startScene) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(state.startScene);
        SessionState.EraseString(SceneStateKey);
      }
    }

    [Serializable]
    private sealed class SavedScenes { public string[] paths; public bool[] loaded; public bool[] active; public string startScene; }

    private static ReactiveSetting<float> Setting(GraphicsSettingsModule module)
    {
      var property = typeof(GraphicsSettingsModule).GetProperty("UiScale");
      Assert.That(property, Is.Not.Null, "UI scale must use the existing reactive graphics settings lifecycle.");
      return (ReactiveSetting<float>)property.GetValue(module);
    }

    private static Type RuntimeType(string name)
    {
      Type type = typeof(SettingsSystem).Assembly.GetType("MToolKit.Runtime.UI.Scaling." + name);
      Assert.That(type, Is.Not.Null, "Reusable scale infrastructure must live in MTK Runtime.");
      return type;
    }

    [Test]
    public void LivePreference_DefaultApplyCancelAndReset_UseOneSetting()
    {
      using var rebinder = new InputRebinderService();
      using var settings = new SettingsSystem(rebinder);
      var scale = Setting(settings.GraphicsSettings);
      Assert.That(scale.Value, Is.EqualTo(1f));
      Assert.That(scale.Default, Is.EqualTo(1f));
      scale.Value = 1.5f;
      Assert.That(settings.IsDirty.Value, Is.True);
      settings.Apply(gotoMenu: false);
      scale.Value = 3f;
      settings.Cancel();
      Assert.That(scale.Value, Is.EqualTo(1.5f));
      settings.DefaultSettings();
      Assert.That(scale.Value, Is.EqualTo(1f));
      Assert.That(scale.LastValue, Is.EqualTo(1f));
      Assert.That(settings.IsDirty.Value, Is.False);
    }

    [TestCase(-1f, 0.75f)]
    [TestCase(5f, 3f)]
    [TestCase(float.NaN, 1f)]
    [TestCase(float.PositiveInfinity, 1f)]
    [TestCase(float.NegativeInfinity, 1f)]
    public void InvalidLivePreference_IsFiniteAndClamped(float supplied, float expected)
    {
      var module = new GraphicsSettingsModule();
      try
      {
        var scale = Setting(module);
        scale.Value = supplied;
        Assert.That(scale.Value, Is.EqualTo(expected));
      }
      finally { module.OnShutdown(); }
    }

    [TestCase(null, 1f)]
    [TestCase("not-a-number", 1f)]
    [TestCase("NaN", 1f)]
    [TestCase("0.1", 0.75f)]
    [TestCase("50", 3f)]
    public async Task MissingOrCorruptIni_HasSafeCancellationBaseline(string supplied, float expected)
    {
      using var fixture = new IniFixture(supplied == null ? "[Audio]\nMasterVolume=0.3\n" : "[Graphics]\nUiScale=" + supplied + "\n");
      await fixture.Settings.Initialization.AsTask();
      var scale = Setting(fixture.Settings.GraphicsSettings);
      Assert.That(scale.Value, Is.EqualTo(expected));
      Assert.That(scale.LastValue, Is.EqualTo(expected));
      Assert.That(fixture.Settings.IsDirty.Value, Is.False);
      Assert.That(fixture.Ini.KeyExists("Graphics", "UiScale"), Is.True);
    }

    [Test]
    public async Task ApplyCancelAndReset_SurviveActualIniAndFreshSettingsReload()
    {
      using var fixture = new IniFixture();
      await fixture.Settings.Initialization.AsTask();
      var scale = Setting(fixture.Settings.GraphicsSettings);
      scale.Value = 2.37f;
      fixture.Settings.Apply(gotoMenu: false);
      await fixture.WaitForWrites();
      await fixture.AssertReload(2.37f);
      string appliedFile = File.ReadAllText(fixture.Config.GetIniFilePath());
      scale.Value = 3f;
      fixture.Settings.Cancel();
      Assert.That(scale.Value, Is.EqualTo(2.37f));
      Assert.That(File.ReadAllText(fixture.Config.GetIniFilePath()), Is.EqualTo(appliedFile));
      fixture.Settings.DefaultSettings();
      await fixture.WaitForWrites();
      await fixture.AssertReload(1f);
    }

    [TestCase("fr-FR", 1.5f, "1.5")]
    [TestCase("fr-FR", 0.75f, "0.75")]
    [TestCase("fr-FR", 2.99f, "2.99")]
    [TestCase("de-DE", 1.5f, "1.5")]
    [TestCase("de-DE", 0.75f, "0.75")]
    [TestCase("de-DE", 2.99f, "2.99")]
    public async Task UiScaleIniRoundTrip_IsInvariantUnderCommaDecimalCulture(
      string culture, float expected, string serialized)
    {
      CultureInfo previousCulture = CultureInfo.CurrentCulture;
      CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;
      try
      {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        using var fixture = new IniFixture();
        await fixture.Settings.Initialization.AsTask();
        await fixture.WaitForWrites();
        Assert.That(fixture.Ini.GetValue("Graphics", "UiScale"), Is.EqualTo("1"));
        var scale = Setting(fixture.Settings.GraphicsSettings);
        scale.Value = expected;
        Assert.That(CultureInfo.CurrentCulture.Name, Is.EqualTo(culture));
        Assert.That(CultureInfo.CurrentUICulture.Name, Is.EqualTo("en-US"));
        fixture.Settings.Apply(gotoMenu: false);
        await fixture.WaitForWrites();

        using var reader = new IniService(fixture.Config);
        using var reloadedRebinder = new InputRebinderService();
        using var reloaded = new SettingsSystem(reloadedRebinder, reader);
        await reloaded.Initialization.AsTask();
        var restoredScale = Setting(reloaded.GraphicsSettings);
        Assert.That(fixture.Ini.GetValue("Graphics", "UiScale"), Is.EqualTo(serialized),
          "The saved scale uses invariant decimal notation independently of UI language.");
        Assert.That(File.ReadAllText(fixture.Config.GetIniFilePath()),
          Does.Contain("UiScale=" + serialized));
        Assert.That(reader.GetValue("Graphics", "UiScale"), Is.EqualTo(serialized));
        Assert.That(restoredScale.Value, Is.EqualTo(expected).Within(0.0001f),
          "A new settings lifetime must restore the selected multiplier.");
        Assert.That(restoredScale.LastValue, Is.EqualTo(expected).Within(0.0001f));
        Assert.That(restoredScale.IsDirty, Is.False);
      }
      finally
      {
        CultureInfo.CurrentCulture = previousCulture;
        CultureInfo.CurrentUICulture = previousUiCulture;
      }
    }

    [Test]
    public void EmergencyChord_EditModeFixtureDoesNotEnableOrRegisterRebindableAction()
    {
      Assert.That(Application.isPlaying, Is.False);
      using var rebinder = new InputRebinderService();
      using var settings = new SettingsSystem(rebinder);
      object chord = typeof(SettingsSystem).GetField("recoverUiScaleAction", PrivateInstance)
        .GetValue(settings);
      Assert.That(chord, Is.Not.Null);
      Assert.That((bool)chord.GetType().GetProperty("enabled").GetValue(chord), Is.False,
        "Creating settings for INI tests must not arm a keyboard action in EditMode.");
      Assert.That(settings.InputSettings.RegisteredActions.Count, Is.Zero,
        "The fixed recovery chord must remain outside editable bindings and their reset lifecycle.");
    }

    [Test]
    public void EffectiveReferenceResolution_PreservesBaselineTimesUserScale_ForEntireMatrix()
    {
      MethodInfo method = RuntimeType("UiScalePolicy").GetMethod("ReferenceResolution");
      Assert.That(method, Is.Not.Null);
      Vector2 authored = new(1920, 1080);
      foreach (Vector2 screen in new[] { new Vector2(1280, 720), new Vector2(1366, 768),
        new Vector2(1600, 900), new Vector2(1920, 1080), new Vector2(1920, 1200),
        new Vector2(2560, 1440), new Vector2(3440, 1440), new Vector2(3840, 2160) })
      foreach (float scale in new[] { 0.75f, 1f, 1.25f, 1.5f, 2f, 2.5f, 3f })
      {
        Vector2 effective = (Vector2)method.Invoke(null, new object[] { authored, scale });
        float baseline = Mathf.Sqrt(screen.x / authored.x * screen.y / authored.y);
        float actual = Mathf.Sqrt(screen.x / effective.x * screen.y / effective.y);
        Assert.That(actual, Is.EqualTo(baseline * scale).Within(0.0001f), screen + " at " + scale);
      }
    }

    [UnityTest]
    public IEnumerator RootCanvas_LiveAndLateBinding_NoCompounding_AndRestoresOnDisable()
    {
      PrepareEmptyPlayMode();
      yield return new EnterPlayMode();
      Type bindingType = RuntimeType("UserUiScaleCanvas");
      using var setting = new ReactiveSetting<float>(1f, "UI Scale");
      GameObject root = new("Scale test", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
      root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
      Assert.That(root.GetComponent<Canvas>().isRootCanvas, Is.True, "The fixture must exercise a screen-space root.");
      try
      {
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var component = (Behaviour)root.AddComponent(bindingType);
        bindingType.GetMethod("Configure").Invoke(component, new object[] { setting });
        yield return null;
        float baseline = root.GetComponent<Canvas>().scaleFactor;
        setting.Value = 3f;
        yield return null;
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(640, 360)));
        Assert.That(root.GetComponent<Canvas>().scaleFactor, Is.EqualTo(baseline * 3f).Within(0.0001f));
        setting.Value = 1.5f;
        yield return null;
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1280, 720)));
        component.enabled = false;
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1920, 1080)));
        setting.Value = 2f;
        component.enabled = true;
        yield return null;
        Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(960, 540)));
      }
      finally { Object.DestroyImmediate(root); }
    }

    [TestCase(RenderMode.WorldSpace)]
    [TestCase(RenderMode.ScreenSpaceOverlay)]
    public void WorldSpaceAndNestedCanvases_AreNotScaled(RenderMode renderMode)
    {
      Type bindingType = RuntimeType("UserUiScaleCanvas");
      using var setting = new ReactiveSetting<float>(3f, "UI Scale");
      GameObject parent = new("Parent canvas", typeof(Canvas));
      GameObject root = new("Excluded canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
      try
      {
        root.GetComponent<Canvas>().renderMode = renderMode;
        if (renderMode != RenderMode.WorldSpace) root.transform.SetParent(parent.transform, false);
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        Vector2 authored = scaler.referenceResolution;
        var component = root.AddComponent(bindingType);
        bindingType.GetMethod("Configure").Invoke(component, new object[] { setting });
        Assert.That(scaler.referenceResolution, Is.EqualTo(authored));
      }
      finally { Object.DestroyImmediate(root); Object.DestroyImmediate(parent); }
    }


    [Test]
    public void PercentageSlider_UsesOnePercentSteps_ReflectsCancelAndDisposesPriorBindings()
    {
      Type bindingType = RuntimeType("UserUiScaleSlider");
      using var setting = new ReactiveSetting<float>(1f, "UI Scale");
      using var replacement = new ReactiveSetting<float>(1.5f, "UI Scale");
      GameObject go = new("Scale slider", typeof(RectTransform), typeof(Slider));
      try
      {
        var slider = go.GetComponent<Slider>();
        var component = go.AddComponent(bindingType);
        bindingType.GetMethod("Configure").Invoke(component, new object[] { slider, null, setting });
        Assert.That(slider.minValue, Is.EqualTo(75));
        Assert.That(slider.maxValue, Is.EqualTo(300));
        Assert.That(slider.wholeNumbers, Is.True);
        setting.Value = 2.37f;
        Assert.That(slider.value, Is.EqualTo(237));
        slider.value = 238;
        Assert.That(setting.Value, Is.EqualTo(2.38f));
        setting.OnCancel();
        Assert.That(slider.value, Is.EqualTo(100));
        bindingType.GetMethod("Bind").Invoke(component, new object[] { replacement });
        setting.Value = 3f;
        Assert.That(slider.value, Is.EqualTo(150), "Rebinding must dispose the old setting subscription.");
        slider.value = 151;
        Assert.That(replacement.Value, Is.EqualTo(1.51f));
        bindingType.GetMethod("ResetToDefault").Invoke(component, null);
        Assert.That(replacement.Value, Is.EqualTo(1f));
        Assert.That(replacement.LastValue, Is.EqualTo(1.5f), "Reset is a draft; Apply/Cancel own persistence.");
      }
      finally { Object.DestroyImmediate(go); }
    }

    [UnityTest]
    public IEnumerator ConstantPixelCanvas_UsesAuthoredFactor_AndLateBindingStartsAtCurrentPreference()
    {
      PrepareEmptyPlayMode();
      yield return new EnterPlayMode();
      Type bindingType = RuntimeType("UserUiScaleCanvas");
      using var setting = new ReactiveSetting<float>(3f, "UI Scale");
      GameObject go = new("Pixel root", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
      go.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
      Assert.That(go.GetComponent<Canvas>().isRootCanvas, Is.True, "The fixture must exercise a screen-space root.");
      try
      {
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1.25f;
        var component = (Behaviour)go.AddComponent(bindingType);
        bindingType.GetMethod("Configure").Invoke(component, new object[] { setting });
        Assert.That(scaler.scaleFactor, Is.EqualTo(3.75f));
        setting.Value = 0.75f;
        Assert.That(scaler.scaleFactor, Is.EqualTo(0.9375f));
        component.enabled = false;
        Assert.That(scaler.scaleFactor, Is.EqualTo(1.25f));
      }
      finally { Object.DestroyImmediate(go); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task EmergencyRecovery_PersistsOnlyUiScale_AndPreservesOtherDraftsAndBaselines(bool otherDrafts)
    {
      using var fixture = new IniFixture("[Graphics]\nUiScale=2\nDisableBloom=false\n[Audio]\nMusicVolume=0.4\n[Input]\nCameraPanSensitivity=1.5\n");
      await fixture.Settings.Initialization.AsTask();
      await fixture.WaitForWrites();
      var settings = fixture.Settings;
      var scale = Setting(settings.GraphicsSettings);
      scale.Value = 3f;
      if (otherDrafts)
      {
        settings.AudioSettings.MusicVolume.Value = 0.7f;
        settings.GraphicsSettings.DisableBloom.Value = true;
        settings.InputSettings.CameraPanSensitivity.Value = 2.5f;
      }
      var before = new System.Collections.Generic.Dictionary<string, string>();
      foreach (var pair in fixture.Ini.AllValues) before[pair.Key] = pair.Value;
      MethodInfo recover = typeof(SettingsSystem).GetMethod("RecoverUiScale");
      Assert.That(recover, Is.Not.Null, "UI scale must be recoverable without navigating the scaled UI.");
      Assert.That(recover.ReturnType, Is.EqualTo(typeof(UniTask)));
      await ((UniTask)recover.Invoke(settings, null)).AsTask();
      Assert.That(scale.Value, Is.EqualTo(1f));
      Assert.That(scale.LastValue, Is.EqualTo(1f));
      Assert.That(settings.IsDirty.Value, Is.EqualTo(otherDrafts));
      Assert.That(settings.AudioSettings.MusicVolume.Value, Is.EqualTo(otherDrafts ? 0.7f : 0.4f));
      Assert.That(settings.AudioSettings.MusicVolume.LastValue, Is.EqualTo(0.4f));
      Assert.That(settings.GraphicsSettings.DisableBloom.Value, Is.EqualTo(otherDrafts));
      Assert.That(settings.GraphicsSettings.DisableBloom.LastValue, Is.False);
      Assert.That(settings.InputSettings.CameraPanSensitivity.Value, Is.EqualTo(otherDrafts ? 2.5f : 1.5f));
      Assert.That(settings.InputSettings.CameraPanSensitivity.LastValue, Is.EqualTo(1.5f));
      foreach (var pair in before)
        if (pair.Key != "Graphics.UiScale")
          Assert.That(fixture.Ini.AllValues[pair.Key], Is.EqualTo(pair.Value), "Recovery changed " + pair.Key);
      await fixture.AssertReload(1f);
      using var reader = new IniService(fixture.Config);
      await reader.Initialization.AsTask();
      Assert.That(reader.GetValue<float>("Audio", "MusicVolume"), Is.EqualTo(0.4f));
      Assert.That(reader.GetValue<bool>("Graphics", "DisableBloom"), Is.False);
      Assert.That(reader.GetValue<float>("Input", "CameraPanSensitivity"), Is.EqualTo(1.5f));
      settings.Cancel();
      Assert.That(scale.Value, Is.EqualTo(1f), "Cancel must not restore the scale that required emergency recovery.");
    }
    private sealed class IniFixture : IDisposable
    {
      private readonly InputRebinderService rebinder = new();
      public IniConfig Config { get; }
      public IniService Ini { get; }
      public SettingsSystem Settings { get; }
      public IniFixture(string content = null)
      {
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "automation-scratch", "ui-scale-settings", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        Config = ScriptableObject.CreateInstance<IniConfig>();
        typeof(IniConfig).GetField("iniDirectory", PrivateInstance).SetValue(Config, directory);
        typeof(IniConfig).GetField("iniFileName", PrivateInstance).SetValue(Config, "settings.ini");
        typeof(IniConfig).GetField("usePersistentDataPath", PrivateInstance).SetValue(Config, false);
        if (content != null) File.WriteAllText(Config.GetIniFilePath(), content);
        Ini = new IniService(Config);
        Settings = new SettingsSystem(rebinder, Ini);
      }
      public async Task WaitForWrites()
      {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (Ini.IsSaving.Value && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.That(Ini.IsSaving.Value, Is.False);
      }
      public async Task AssertReload(float expected)
      {
        using var reader = new IniService(Config);
        using var newRebinder = new InputRebinderService();
        using var reloaded = new SettingsSystem(newRebinder, reader);
        await reloaded.Initialization.AsTask();
        var scale = Setting(reloaded.GraphicsSettings);
        Assert.That(scale.Value, Is.EqualTo(expected).Within(0.0001f));
        Assert.That(scale.IsDirty, Is.False);
      }
      public void Dispose()
      {
        Settings.Dispose(); rebinder.Dispose(); Ini.Dispose(); Object.DestroyImmediate(Config);
      }
    }
  }
}
