using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Audio;
using MToolKit.Runtime.Audio.Config;
using MToolKit.Runtime.Settings.Audio;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Input;
using MToolKit.Runtime.Settings.Interfaces;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace MToolKit.Tests.Editor.Runtime
{
  public class AudioServiceInitializationTests
  {
    [Test]
    public async Task InitializeAsync_BindsMixerBeforePrefabLoadCompletes()
    {
      var settings = new TestSettingsSystem();
      var config = ScriptableObject.CreateInstance<AudioConfig>();
      var root = new GameObject("Audio root");
      var service = new TestAudioService(root.transform, config, settings, blockPrefabLoad: true);

      try
      {
        UniTask initialization = service.InitializeAsync(settings);
        await UniTask.Yield();

        Assert.That(service.PrefabLoadStarted, Is.True);
        Assert.That(initialization.Status, Is.EqualTo(UniTaskStatus.Pending));
        Assert.That(service.MixerApplicationCount, Is.GreaterThanOrEqualTo(4),
          "The final settings must be bound and applied while the independent prefab load is still blocked.");

        service.CompletePrefabLoad();
        await initialization;
      }
      finally
      {
        service.Dispose();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(config);
      }
    }

    [Test]
    public async Task ConcurrentInitializeAsync_SharesLoadPoolAndSubscriptions()
    {
      var settings = new TestSettingsSystem();
      var config = ScriptableObject.CreateInstance<AudioConfig>();
      var root = new GameObject("Audio root");
      var service = new TestAudioService(root.transform, config, settings, blockPrefabLoad: true);

      try
      {
        UniTask first = service.InitializeAsync(settings);
        UniTask second = service.InitializeAsync(settings);
        await UniTask.Yield();

        Assert.That(service.PrefabLoadCount, Is.EqualTo(1), "Concurrent callers must await one prefab load.");

        service.CompletePrefabLoad();
        await UniTask.WhenAll(first, second);

        Assert.That(service.PoolInitializationCount, Is.EqualTo(1));
        int applicationsBeforeChange = service.MixerApplicationCount;
        settings.AudioSettings.MusicVolume.Value = 0.25f;
        Assert.That(service.MixerApplicationCount, Is.EqualTo(applicationsBeforeChange + 1),
          "Only one settings subscription should be installed.");
      }
      finally
      {
        service.Dispose();
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(config);
      }
    }

    private sealed class TestAudioService : AudioService
    {
      private readonly bool blockPrefabLoad;
      private readonly UniTaskCompletionSource prefabLoad = new();

      public TestAudioService(Transform root, AudioConfig config, ISettingsSystem settings, bool blockPrefabLoad)
        : base(root, config, settings, null)
      {
        this.blockPrefabLoad = blockPrefabLoad;
      }

      public bool PrefabLoadStarted { get; private set; }
      public int PrefabLoadCount { get; private set; }
      public int PoolInitializationCount { get; private set; }
      public int MixerApplicationCount { get; private set; }

      public void CompletePrefabLoad() => prefabLoad.TrySetResult();

      protected override async UniTask LoadAudioSourcePrefabAsync(CancellationToken ct = default)
      {
        PrefabLoadStarted = true;
        PrefabLoadCount++;
        if (blockPrefabLoad)
          await prefabLoad.Task.AttachExternalCancellation(ct);
      }

      protected override void InitializePool() => PoolInitializationCount++;

      protected override void SetMixerVolume(string parameterName, float volume) => MixerApplicationCount++;
    }

    private sealed class TestSettingsSystem : ISettingsSystem
    {
      public TestSettingsSystem() => AudioSettings = new AudioSettingsModule(this);
      public AudioSettingsModule AudioSettings { get; }
      public GraphicsSettingsModule GraphicsSettings => null;
      public GameSettingsModule GameSettings => null;
      public InputSettingsModule InputSettings => null;
      public ReactiveProperty<bool> IsDirty { get; } = new();
      public UniTask Initialization => UniTask.CompletedTask;
      public void Apply(bool autoFinish = true, bool gotoMenu = true) { }
      public void FinishApply(bool gotoMenu = true) { }
      public void DefaultSettings() { }
      public void Cancel() { }
      public void SetDirty(bool isDirty) => IsDirty.Value = isDirty;
    }
  }
}
