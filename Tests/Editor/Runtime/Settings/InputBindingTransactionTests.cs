using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Settings;
using MToolKit.Runtime.Settings.BoundSettings;
using MToolKit.Runtime.Settings.Ini;
using MToolKit.Runtime.Settings.Input;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  public sealed class InputBindingTransactionTests
  {
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void Cancel_RestoresAppliedCustomBinding_InsteadOfAuthoredDefault()
    {
      using var actions = new OwnedActions();
      using var rebinder = new InputRebinderService();
      var module = new InputSettingsModule(rebinder);
      try
      {
        module.RegisterAction(actions.Pause);
        ChangeBinding(rebinder, actions.Pause, "<Keyboard>/f10");
        module.Apply();
        ChangeBinding(rebinder, actions.Pause, "<Keyboard>/f11");

        module.Cancel();

        Assert.That(actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"),
          "Cancel must restore the applied custom key, not remove all overrides.");
      }
      finally { module.OnShutdown(); }
    }

    [Test]
    public void Cancel_RestoresAppliedOverridesAcrossTwoMaps()
    {
      using var actions = new OwnedActions();
      using var rebinder = new InputRebinderService();
      using var settings = CreateSettings(rebinder, null, actions.Asset);
      ChangeBinding(rebinder, actions.Pause, "<Keyboard>/f10");
      ChangeBinding(rebinder, actions.Attack, "<Keyboard>/f7");
      settings.Apply(autoFinish: true, gotoMenu: false);
      ChangeBinding(rebinder, actions.Pause, "<Keyboard>/f11");
      ChangeBinding(rebinder, actions.Attack, "<Keyboard>/f8");

      settings.Cancel();

      Assert.That(actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"));
      Assert.That(actions.Attack.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f7"));
      Assert.That(settings.IsDirty.Value, Is.False);
      Assert.That(BindingSetting(settings.InputSettings).IsDirty, Is.False);
    }

    [Test]
    public void BindingOnlyEditAndRowReset_ParticipateInSettingsDirtyState()
    {
      using var actions = new OwnedActions();
      using var rebinder = new InputRebinderService();
      using var settings = CreateSettings(rebinder, null, actions.Asset);
      Assert.That(settings.IsDirty.Value, Is.False);

      ChangeBinding(rebinder, actions.Pause, "<Keyboard>/f10");
      Assert.That(settings.IsDirty.Value, Is.True, "A binding-only edit must enable Apply/Cancel.");
      settings.Apply(autoFinish: true, gotoMenu: false);
      Assert.That(settings.IsDirty.Value, Is.False);

      settings.InputSettings.RemoveBindingOverride(actions.Pause, 0);
      Assert.That(actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/escape"));
      Assert.That(settings.IsDirty.Value, Is.True, "Resetting an applied custom key is an unsaved edit.");
      settings.Cancel();
      Assert.That(actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"));
      Assert.That(settings.IsDirty.Value, Is.False);
    }

    [Test]
    public async Task AppliedOverrides_ReloadBeforeGameplay_AndCancelDoesNotWriteDraft()
    {
      using var fixture = new IniFixture();
      await fixture.Settings.Initialization.AsTask();
      ChangeBinding(fixture.Rebinder, fixture.Actions.Pause, "<Keyboard>/f10");
      ChangeBinding(fixture.Rebinder, fixture.Actions.Attack, "<Keyboard>/f7");
      fixture.Settings.Apply(autoFinish: true, gotoMenu: false);
      await fixture.WaitForWrites();
      string appliedFile = File.ReadAllText(fixture.Config.GetIniFilePath());
      Assert.That(fixture.Ini.KeyExists("Input", "BindingOverridesJson"), Is.True);
      Assert.That(fixture.Ini.GetValue<string>("Input", "BindingOverridesJson", ""),
        Is.Not.Empty, "The real INI must contain the applied overrides.");

      using (var reloadedActions = fixture.Actions.CloneAuthored())
      using (var newRebinder = new InputRebinderService())
      using (var reader = new IniService(fixture.Config))
      using (var reloaded = CreateSettings(newRebinder, reader, reloadedActions.Asset))
      {
        await reloaded.Initialization.AsTask();
        Assert.That(reloadedActions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"),
          "Startup must load bindings without opening or configuring a settings UI.");
        Assert.That(reloadedActions.Attack.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f7"));
        Assert.That(reloaded.IsDirty.Value, Is.False);
        Assert.That(BindingSetting(reloaded.InputSettings).IsDirty, Is.False);
        ChangeBinding(newRebinder, reloadedActions.Pause, "<Keyboard>/f11");
        reloaded.Cancel();
        Assert.That(reloadedActions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"));
        Assert.That(File.ReadAllText(fixture.Config.GetIniFilePath()), Is.EqualTo(appliedFile));
      }
    }

    [Test]
    public async Task ConfirmedDefaults_PersistEmptyOverrides_AndBecomeCancelBaseline()
    {
      using var fixture = new IniFixture();
      await fixture.Settings.Initialization.AsTask();
      ChangeBinding(fixture.Rebinder, fixture.Actions.Pause, "<Keyboard>/f10");
      fixture.Settings.Apply(autoFinish: true, gotoMenu: false);
      await fixture.WaitForWrites();

      fixture.Settings.DefaultSettings();
      await fixture.WaitForWrites();
      Assert.That(fixture.Actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/escape"));
      Assert.That(fixture.Ini.GetValue("Input", "BindingOverridesJson", "missing"), Is.Empty);
      ChangeBinding(fixture.Rebinder, fixture.Actions.Pause, "<Keyboard>/f11");
      fixture.Settings.Cancel();
      Assert.That(fixture.Actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/escape"));

      using var reloadedActions = fixture.Actions.CloneAuthored();
      using var newRebinder = new InputRebinderService();
      using var reader = new IniService(fixture.Config);
      using var reloaded = CreateSettings(newRebinder, reader, reloadedActions.Asset);
      await reloaded.Initialization.AsTask();
      Assert.That(reloadedActions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/escape"));
      Assert.That(reloadedActions.Attack.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
      Assert.That(reloaded.IsDirty.Value, Is.False);
    }

    [Test]
    public async Task LegacyIniWithoutBindingKey_LoadsAuthoredBindingsAsCleanBaseline()
    {
      using var fixture = new IniFixture("[Input]\nCameraPanSensitivity=1\nCameraZoomSensitivity=1\n");
      await fixture.Settings.Initialization.AsTask();
      Assert.That(fixture.Actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/escape"));
      Assert.That(fixture.Actions.Attack.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/space"));
      Assert.That(BindingSetting(fixture.Settings.InputSettings).Value, Is.Empty);
      Assert.That(fixture.Settings.IsDirty.Value, Is.False);
    }

    [Test]
    public void DisposeWithoutActiveRebind_DoesNotEnableDisabledMapsOrActions()
    {
      using var actions = new OwnedActions();
      var rebinder = new InputRebinderService();
      rebinder.RegisterAction(actions.Pause);
      rebinder.RegisterAction(actions.Attack);
      actions.Attack.Enable();
      Assert.That(actions.Pause.enabled, Is.False);
      Assert.That(actions.Attack.enabled, Is.True);
      Assert.That(actions.SecondAttack.enabled, Is.False);

      rebinder.Dispose();

      Assert.That(actions.Pause.enabled, Is.False, "Disposal without a rebind cannot activate an input mode.");
      Assert.That(actions.Attack.enabled, Is.True);
      Assert.That(actions.SecondAttack.enabled, Is.False, "Partial action enablement must remain unchanged.");
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task OverlappingApply_PersistsLatestAppliedSnapshot_WithoutDraftOrConcurrentMutation(bool cancelBeforeRelease)
    {
      using var actions = new OwnedActions();
      using var rebinder = new InputRebinderService();
      using var ini = new BlockingWriteIniService();
      using var settings = CreateSettings(rebinder, ini, actions.Asset);
      await settings.Initialization.AsTask();
      ChangeBinding(rebinder, actions.Pause, "<Keyboard>/f10");
      settings.Apply(autoFinish: true, gotoMenu: false);
      await WaitUntil(() => ini.IsSaving.Value, "First applied snapshot must start saving.");
      try
      {
        ChangeBinding(rebinder, actions.Pause, "<Keyboard>/f8");
        settings.Apply(autoFinish: true, gotoMenu: false);
        ChangeBinding(rebinder, actions.Pause, "<Keyboard>/f9");
        if (cancelBeforeRelease) settings.Cancel();
        Assert.That(ini.MutationsWhileSaving, Is.Zero,
          "A later Apply must not mutate dictionaries while the first disk write reads them.");
        ini.ReleaseFirstWrite();
        await WaitUntil(() => ini.CompletedWrites >= 2 && !ini.IsSaving.Value,
          "The latest applied snapshot must get a follow-up write.");
        Assert.That(ini.DroppedWrites, Is.Zero);
        Assert.That(ini.MutationsWhileSaving, Is.Zero);
        using var reloaded = actions.CloneAuthored();
        reloaded.Asset.LoadBindingOverridesFromJson(ini.Persisted["Input.BindingOverridesJson"]);
        Assert.That(reloaded.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f8"),
          "Queued writes must retain the applied snapshot while F9 is an uncommitted draft.");
        if (!cancelBeforeRelease) settings.Cancel();
        Assert.That(actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f8"));
        Assert.That(settings.IsDirty.Value, Is.False);
      }
      finally { ini.ReleaseFirstWrite(); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CancelActiveRebind_RestoresExactActionEnablement_AndExistingOverride(bool targetEnabled)
    {
      using var actions = new OwnedActions();
      using var rebinder = new InputRebinderService();
      rebinder.RegisterAction(actions.Pause);
      rebinder.RegisterAction(actions.Attack);
      rebinder.RegisterAction(actions.SecondAttack);
      actions.Attack.ApplyBindingOverride(0, "<Keyboard>/f10");
      if (targetEnabled) actions.Attack.Enable();
      Assert.That(rebinder.StartInteractiveRebinding(actions.Attack, 0), Is.True);
      Assert.That(actions.Attack.enabled, Is.False);
      rebinder.CancelRebinding();
      Assert.That(actions.Pause.enabled, Is.False, "Inactive Colony mode must remain inactive.");
      Assert.That(actions.Attack.enabled, Is.EqualTo(targetEnabled));
      Assert.That(actions.SecondAttack.enabled, Is.False, "Partial action enablement must be preserved.");
      Assert.That(actions.Attack.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"));
    }

    [Test]
    public void BindingConflicts_RejectSameMapSibling_AndAllowDifferentModeMap()
    {
      using var actions = new OwnedActions();
      using var rebinder = new InputRebinderService();
      rebinder.RegisterAction(actions.Pause);
      rebinder.RegisterAction(actions.Attack);
      rebinder.RegisterAction(actions.SecondAttack);
      actions.Pause.ApplyBindingOverride(0, "<Keyboard>/f10");
      Assert.That(rebinder.CheckBindingConflict(actions.Attack, 0, "<Keyboard>/f10"), Is.False);
      actions.SecondAttack.ApplyBindingOverride(0, "<Keyboard>/f10");
      Assert.That(rebinder.CheckBindingConflict(actions.Attack, 0, "<Keyboard>/f10"), Is.True);
    }

    [Test]
    public void RejectedInteractiveConflict_PreservesAppliedOverride_CleanState_AndExactActionEnablement()
    {
      using var actions = new OwnedActions();
      using var rebinder = new InputRebinderService();
      using var settings = CreateSettings(rebinder, null, actions.Asset);
      ChangeBinding(rebinder, actions.Attack, "<Keyboard>/f10");
      settings.Apply(autoFinish: true, gotoMenu: false);
      actions.Attack.Enable();
      bool? completed = null;
      int bindingChanges = 0;
      using var completedSubscription = rebinder.OnRebindingCompleted.Subscribe(result =>
      {
        if (result.action == actions.Attack) completed = result.completed;
      });
      using var changedSubscription = rebinder.OnBindingChanged.Subscribe(_ => bindingChanges++);
      Keyboard previousKeyboard = Keyboard.current;
      Keyboard ownedKeyboard = InputSystem.AddDevice<Keyboard>();
      try
      {
        Assert.That(rebinder.StartInteractiveRebinding(actions.Attack, 0), Is.True);
        var operationField = typeof(InputRebinderService).GetField("currentRebindingOperation", PrivateInstance);
        Assert.That(operationField, Is.Not.Null);
        var operation = (InputActionRebindingExtensions.RebindingOperation)operationField.GetValue(rebinder);
        Assert.That(operation, Is.Not.Null);
        // Drive Unity's real candidate selection and completion callback without injecting editor input.
        operation.AddCandidate(ownedKeyboard.qKey, score: 1f, magnitude: 1f);
        operation.Complete();

        Assert.That(completed, Is.False, "Q conflicts with the authored ARPG/Alternate binding.");
        Assert.That(rebinder.IsRebinding, Is.False);
        Assert.That(actions.Attack.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"));
        Assert.That(actions.SecondAttack.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/q"));
        Assert.That(bindingChanges, Is.Zero, "A rejected candidate must not announce an accepted binding edit.");
        Assert.That(settings.IsDirty.Value, Is.False);
        Assert.That(BindingSetting(settings.InputSettings).IsDirty, Is.False);
        Assert.That(actions.Attack.enabled, Is.True);
        Assert.That(actions.SecondAttack.enabled, Is.False);
        Assert.That(actions.Pause.enabled, Is.False);
      }
      finally
      {
        rebinder.CancelRebinding();
        InputSystem.RemoveDevice(ownedKeyboard);
        if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
      }
    }

    [Test]
    public void CancelledOperationCompletion_CanStartNextRebind()
    {
      using var actions = new OwnedActions();
      using var rebinder = new InputRebinderService();
      bool nextStarted = false;
      using var subscription = rebinder.OnRebindingCompleted.Subscribe(completion =>
      {
        if (completion.action == actions.Attack)
          nextStarted = rebinder.StartInteractiveRebinding(actions.Pause, 0);
      });
      Assert.That(rebinder.StartInteractiveRebinding(actions.Attack, 0), Is.True);
      rebinder.CancelRebinding();
      Assert.That(nextStarted, Is.True, "Completion observers must see previous capture cleaned up.");
      Assert.That(rebinder.IsRebinding, Is.True, "Previous cleanup must not dispose its successor.");
      rebinder.CancelRebinding();
      Assert.That(rebinder.IsRebinding, Is.False);
    }

    [Test]
    public async Task InvalidPersistedJson_PreservesExistingOverrides_FaultsInitialization_AndRefusesSave()
    {
      using var actions = new OwnedActions();
      actions.Pause.ApplyBindingOverride(0, "<Keyboard>/f10");
      using var rebinder = new InputRebinderService();
      using var ini = new BlockingWriteIniService();
      ini.SetValue("Input", "BindingOverridesJson", "invalid-json");
      using var settings = CreateSettings(rebinder, ini, actions.Asset);
      Assert.ThrowsAsync<InvalidOperationException>(async () => await settings.Initialization.AsTask());
      Assert.That(actions.Pause.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f10"));
      settings.Apply(autoFinish: true, gotoMenu: false);
      await Task.Yield();
      Assert.That(ini.IsSaving.Value, Is.False);
      Assert.That(ini.CompletedWrites, Is.Zero);
      Assert.That(ini.AllValues["Input.BindingOverridesJson"], Is.EqualTo("invalid-json"));
    }

    private static async Task WaitUntil(Func<bool> predicate, string failure)
    {
      var deadline = DateTime.UtcNow.AddSeconds(3);
      while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(10);
      Assert.That(predicate(), Is.True, failure);
    }

    private sealed class BlockingWriteIniService : IIniService, IDisposable
    {
      private readonly Dictionary<string, string> values = new();
      private readonly UniTaskCompletionSource firstWriteReleased = new();
      private int startedWrites;
      public ReactiveProperty<bool> IsSaving { get; } = new(false);
      public ReactiveProperty<bool> IsLoading { get; } = new(false);
      public IReadOnlyDictionary<string, string> AllValues => values;
      public IReadOnlyDictionary<string, string> Persisted { get; private set; } = new Dictionary<string, string>();
      public int MutationsWhileSaving { get; private set; }
      public int DroppedWrites { get; private set; }
      public int CompletedWrites { get; private set; }
      public UniTask Initialization => UniTask.CompletedTask;
      public UniTask LoadAsync(CancellationToken ct = default) => UniTask.CompletedTask;
      public async UniTask SaveAsync(CancellationToken ct = default)
      {
        if (IsSaving.Value) { DroppedWrites++; return; }
        IsSaving.Value = true;
        var snapshot = new Dictionary<string, string>(values);
        int writeNumber = ++startedWrites;
        try
        {
          if (writeNumber == 1) await firstWriteReleased.Task.AttachExternalCancellation(ct);
          Persisted = snapshot;
          CompletedWrites++;
        }
        finally { IsSaving.Value = false; }
      }
      public void ReleaseFirstWrite() => firstWriteReleased.TrySetResult();
      public void SetValue(string section, string key, object value)
      {
        if (IsSaving.Value) MutationsWhileSaving++;
        values[$"{section}.{key}"] = value?.ToString() ?? string.Empty;
      }
      public bool KeyExists(string section, string key) => values.ContainsKey($"{section}.{key}");
      public string GetValue(string section, string key, string defaultValue = null)
        => values.TryGetValue($"{section}.{key}", out string value) ? value : defaultValue;
      public T GetValue<T>(string section, string key, T defaultValue = default)
        => values.TryGetValue($"{section}.{key}", out string value)
          ? (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture)
          : defaultValue;
      public void DeleteKey(string section, string key) => values.Remove($"{section}.{key}");
      public IEnumerable<string> GetKeys(string section) => Array.Empty<string>();
      public IEnumerable<string> GetSections() => Array.Empty<string>();
      public void PopulateDefaultsFromSettingsSystem(MToolKit.Runtime.Settings.Interfaces.ISettingsSystem settings) { }
      public void Dispose()
      {
        ReleaseFirstWrite();
        IsSaving.Dispose();
        IsLoading.Dispose();
      }
    }

    private static SettingsSystem CreateSettings(InputRebinderService rebinder, IIniService ini, InputActionAsset asset)
    {
      var ctor = typeof(SettingsSystem).GetConstructor(new[]
        { typeof(InputRebinderService), typeof(IIniService), typeof(InputActionAsset) });
      Assert.That(ctor, Is.Not.Null,
        "SettingsSystem must receive the canonical InputActionAsset before asynchronous INI initialization.");
      return (SettingsSystem)ctor.Invoke(new object[] { rebinder, ini, asset });
    }

    private static ReactiveSetting<string> BindingSetting(InputSettingsModule module)
    {
      var property = typeof(InputSettingsModule).GetProperty("BindingOverridesJson");
      Assert.That(property, Is.Not.Null, "Binding snapshots must participate in the settings lifecycle.");
      Assert.That(property.PropertyType, Is.EqualTo(typeof(ReactiveSetting<string>)));
      return (ReactiveSetting<string>)property.GetValue(module);
    }

    private static void ChangeBinding(InputRebinderService rebinder, InputAction action, string path)
    {
      // This is the notification emitted by successful interactive rebinding. The actual
      // interactive menu path is verified separately; these tests cover its transaction owner.
      action.ApplyBindingOverride(0, path);
      rebinder.OnBindingChanged.OnNext((action, 0, path));
    }

    private sealed class OwnedActions : IDisposable
    {
      public InputActionAsset Asset { get; }
      public InputAction Pause => Asset.FindAction("Colony/Pause", true);
      public InputAction Attack => Asset.FindAction("ARPG/Attack", true);
      public InputAction SecondAttack => Asset.FindAction("ARPG/Alternate", true);

      public OwnedActions()
      {
        Asset = ScriptableObject.CreateInstance<InputActionAsset>();
        Asset.AddActionMap("Colony").AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
        var arpg = Asset.AddActionMap("ARPG");
        arpg.AddAction("Attack", InputActionType.Button, "<Keyboard>/space");
        arpg.AddAction("Alternate", InputActionType.Button, "<Keyboard>/q");
      }

      private OwnedActions(InputActionAsset asset) { Asset = asset; }

      public OwnedActions CloneAuthored()
      {
        var clone = InputActionAsset.FromJson(Asset.ToJson());
        clone.RemoveAllBindingOverrides();
        return new OwnedActions(clone);
      }

      public void Dispose()
      {
        Asset.Disable();
        Object.DestroyImmediate(Asset);
      }
    }

    private sealed class IniFixture : IDisposable
    {
      private readonly string directory;
      private readonly int quality = QualitySettings.GetQualityLevel();
      private readonly int vsync = QualitySettings.vSyncCount;
      private readonly int width = Screen.width;
      private readonly int height = Screen.height;
      private readonly FullScreenMode fullscreen = Screen.fullScreenMode;
      private readonly RefreshRate refreshRate = Screen.currentResolution.refreshRateRatio;
      public OwnedActions Actions { get; } = new();
      public InputRebinderService Rebinder { get; } = new();
      public IniConfig Config { get; }
      public IniService Ini { get; }
      public SettingsSystem Settings { get; }

      public IniFixture(string content = null)
      {
        directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "automation-scratch",
          "input-binding-transactions", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(directory);
        Config = ScriptableObject.CreateInstance<IniConfig>();
        typeof(IniConfig).GetField("iniDirectory", PrivateInstance).SetValue(Config, directory);
        typeof(IniConfig).GetField("iniFileName", PrivateInstance).SetValue(Config, "settings.ini");
        typeof(IniConfig).GetField("usePersistentDataPath", PrivateInstance).SetValue(Config, false);
        if (content != null) File.WriteAllText(Config.GetIniFilePath(), content);
        Ini = new IniService(Config);
        try { Settings = CreateSettings(Rebinder, Ini, Actions.Asset); }
        catch { Dispose(); throw; }
      }

      public async Task WaitForWrites()
      {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (Ini.IsSaving.Value && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.That(Ini.IsSaving.Value, Is.False, "The isolated binding INI write did not complete.");
      }

      public void Dispose()
      {
        Settings?.Dispose();
        Rebinder.Dispose();
        Ini?.Dispose();
        Actions.Dispose();
        if (Config != null) Object.DestroyImmediate(Config);
        if (QualitySettings.GetQualityLevel() != quality) QualitySettings.SetQualityLevel(quality);
        if (QualitySettings.vSyncCount != vsync) QualitySettings.vSyncCount = vsync;
        if (Screen.width != width || Screen.height != height || Screen.fullScreenMode != fullscreen)
          Screen.SetResolution(width, height, fullscreen, refreshRate);
        string file = Path.Combine(directory, "settings.ini");
        if (File.Exists(file)) File.Delete(file);
        if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
          Directory.Delete(directory);
      }
    }
  }
}
