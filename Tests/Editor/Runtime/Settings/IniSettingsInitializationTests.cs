using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Input;
using MToolKit.Runtime.Settings;
using MToolKit.Runtime.Settings.Ini;
using MToolKit.Runtime.Settings.Interfaces;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace MToolKit.Tests.Editor.Runtime.Settings
{
  public class IniSettingsInitializationTests
  {
    [Test]
    public async Task LoadAsync_ConcurrentCallersAwaitTheSameUnderlyingLoad()
    {
      IniConfig config = ScriptableObject.CreateInstance<IniConfig>();
      BlockingIniService service = new(config);

      try
      {
        Task first = service.LoadAsync().AsTask();
        await service.LoadStarted.Task;

        Task second = service.LoadAsync().AsTask();

        Assert.That(service.UnderlyingLoadCount, Is.EqualTo(1));
        Assert.That(first.IsCompleted, Is.False);
        Assert.That(second.IsCompleted, Is.False,
          "A concurrent caller returned before the shared INI load completed.");

        service.CompleteLoad();
        await Task.WhenAll(first, second);

        Assert.That(service.UnderlyingLoadCount, Is.EqualTo(1));
        Assert.That(service.Initialization.Status, Is.EqualTo(UniTaskStatus.Succeeded));
      }
      finally
      {
        service.Dispose();
        UnityEngine.Object.DestroyImmediate(config);
      }
    }

    [Test]
    public async Task SettingsInitialization_DoesNotReadIniUntilLoadCompletes()
    {
      BlockingFakeIniService ini = new(0.25f);
      using SettingsSystem settings = new(new InputRebinderService(), ini);

      await ini.LoadStarted.Task;

      Assert.That(settings.Initialization.Status, Is.Not.EqualTo(UniTaskStatus.Succeeded));
      Assert.That(ini.ReadCount, Is.Zero,
        "Settings consumed the INI dictionary while its load was still incomplete.");
      Assert.That(settings.AudioSettings.MusicVolume.Value, Is.EqualTo(1f));

      ini.CompleteLoad();
      await settings.Initialization.AsTask();

      Assert.That(ini.ReadCount, Is.GreaterThan(0));
      Assert.That(settings.AudioSettings.MusicVolume.Value, Is.EqualTo(0.25f));
    }

    [Test]
    public async Task SettingsInitialization_PersistedMusicVolumeWinsWhenLoadAlreadyInProgress()
    {
      BlockingFakeIniService ini = new(0.15f);
      Task installerStyleLoad = ini.LoadAsync().AsTask();
      await ini.LoadStarted.Task;

      using SettingsSystem settings = new(new InputRebinderService(), ini);

      Assert.That(ini.LoadCallCount, Is.EqualTo(2));
      Assert.That(settings.AudioSettings.MusicVolume.Value, Is.EqualTo(1f));

      ini.CompleteLoad();
      await Task.WhenAll(installerStyleLoad, settings.Initialization.AsTask());

      Assert.That(settings.AudioSettings.MusicVolume.Value, Is.EqualTo(0.15f),
        "The persisted value must win regardless of which startup caller begins INI loading.");
    }

    private sealed class BlockingIniService : IniService
    {
      private readonly UniTaskCompletionSource completion = new();

      public BlockingIniService(IniConfig config) : base(config) { }

      public TaskCompletionSource<bool> LoadStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

      public int UnderlyingLoadCount { get; private set; }

      public void CompleteLoad() => completion.TrySetResult();

      protected override async UniTask LoadFromDiskAsync(CancellationToken ct)
      {
        UnderlyingLoadCount++;
        LoadStarted.TrySetResult(true);
        await completion.Task.AttachExternalCancellation(ct);
      }
    }

    private sealed class BlockingFakeIniService : IIniService
    {
      private readonly float persistedMusicVolume;
      private readonly UniTaskCompletionSource completion = new();
      private UniTask initialization;
      private bool loadStarted;
      private bool loaded;

      public BlockingFakeIniService(float persistedMusicVolume)
      {
        this.persistedMusicVolume = persistedMusicVolume;
      }

      public TaskCompletionSource<bool> LoadStarted { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

      public int LoadCallCount { get; private set; }
      public int ReadCount { get; private set; }
      public ReactiveProperty<bool> IsSaving { get; } = new(false);
      public ReactiveProperty<bool> IsLoading { get; } = new(false);
      public IReadOnlyDictionary<string, string> AllValues { get; } =
        new Dictionary<string, string>();
      public UniTask Initialization => initialization;

      public UniTask LoadAsync(CancellationToken ct = default)
      {
        LoadCallCount++;
        if (!loadStarted)
        {
          loadStarted = true;
          initialization = CompleteLoadAsync(ct);
        }

        return initialization;
      }

      public void CompleteLoad() => completion.TrySetResult();

      private async UniTask CompleteLoadAsync(CancellationToken ct)
      {
        IsLoading.Value = true;
        LoadStarted.TrySetResult(true);
        await completion.Task.AttachExternalCancellation(ct);
        loaded = true;
        IsLoading.Value = false;
      }

      public bool KeyExists(string section, string key)
      {
        ReadCount++;
        return loaded && section == "Audio" && key == "MusicVolume";
      }

      public T GetValue<T>(string section, string key, T defaultValue = default)
      {
        ReadCount++;
        if (loaded && section == "Audio" && key == "MusicVolume")
          return (T)(object)persistedMusicVolume;
        return defaultValue;
      }

      public string GetValue(string section, string key, string defaultValue = null) => defaultValue;
      public UniTask SaveAsync(CancellationToken ct = default) => UniTask.CompletedTask;
      public void SetValue(string section, string key, object value) { }
      public void DeleteKey(string section, string key) { }
      public IEnumerable<string> GetKeys(string section) => Array.Empty<string>();
      public IEnumerable<string> GetSections() => Array.Empty<string>();
      public void PopulateDefaultsFromSettingsSystem(ISettingsSystem settingsSystem) { }
    }
  }
}
