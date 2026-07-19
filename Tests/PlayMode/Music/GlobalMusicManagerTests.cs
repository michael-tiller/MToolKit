using System.Collections;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Music;
using MToolKit.Runtime.Settings.Audio;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Input;
using MToolKit.Runtime.Settings.Interfaces;
using NUnit.Framework;
using R3;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.PlayMode.Music
{
  public sealed class GlobalMusicManagerTests
  {
    private GameObject gameObject;
    private GlobalMusicManager manager;
    private AudioSource first;
    private AudioSource second;
    private AudioClip a;
    private AudioClip b;
    private AudioClip c;

    [SetUp]
    public void SetUp()
    {
      GlobalMusicManager.ResetForTesting();
      gameObject = new GameObject(nameof(GlobalMusicManagerTests));
      manager = gameObject.AddComponent<GlobalMusicManager>();
      AudioSource[] sources = gameObject.GetComponents<AudioSource>();
      first = sources[0];
      second = sources[1];
      a = AudioClip.Create("A", 44100, 1, 44100, false);
      b = AudioClip.Create("B", 44100, 1, 44100, false);
      c = AudioClip.Create("C", 44100, 1, 44100, false);
    }

    [TearDown]
    public void TearDown()
    {
      if (gameObject != null) Object.DestroyImmediate(gameObject);
      if (a != null) Object.DestroyImmediate(a);
      if (b != null) Object.DestroyImmediate(b);
      if (c != null) Object.DestroyImmediate(c);
      GlobalMusicManager.ResetForTesting();
    }

    [UnityTest]
    public IEnumerator DefaultMusic_WaitsForSettingsAndMixerReadinessBeforePlaying()
    {
      var settings = new GatedSettingsSystem();
      manager.InitializeForTests(first, second, settings);

      UniTask startup = manager.StartDefaultMusicAsyncForTests(a);
      yield return null;

      Assert.That(first.isPlaying || second.isPlaying, Is.False,
        "Default music started while persisted settings/mixer application was still blocked.");
      Assert.That(first.clip, Is.Null);
      Assert.That(second.clip, Is.Null);

      settings.MarkReady();
      yield return startup.ToCoroutine();

      Assert.That(ActiveSource().clip, Is.SameAs(a));
    }

    [UnityTest]
    public IEnumerator RapidReplacements_LeaveOnlyLatestClipActive()
    {
      var settings = new GatedSettingsSystem();
      settings.MarkReady();
      manager.InitializeForTests(first, second, settings);

      yield return manager.PlayMusicAsyncForTests(a, 0f).ToCoroutine();
      UniTask toB = manager.PlayMusicAsyncForTests(b, 0.25f);
      yield return null;
      UniTask toC = manager.PlayMusicAsyncForTests(c, 0.05f);
      yield return UniTask.WhenAll(toB, toC).ToCoroutine();

      Assert.That(PlayingCount(), Is.EqualTo(1));
      Assert.That(ActiveSource().clip, Is.SameAs(c));
      Assert.That(ActiveSource().volume, Is.EqualTo(1f).Within(0.001f));
      Assert.That(InactiveSource().volume, Is.Zero.Within(0.001f));
    }

    [UnityTest]
    public IEnumerator StopDuringReplacement_StopsAndZeroesBothSources()
    {
      var settings = new GatedSettingsSystem();
      settings.MarkReady();
      manager.InitializeForTests(first, second, settings);

      yield return manager.PlayMusicAsyncForTests(a, 0f).ToCoroutine();
      UniTask replacement = manager.PlayMusicAsyncForTests(b, 0.25f);
      yield return null;

      manager.Stop();
      yield return replacement.ToCoroutine();

      Assert.That(first.isPlaying, Is.False);
      Assert.That(second.isPlaying, Is.False);
      Assert.That(first.volume, Is.Zero.Within(0.001f));
      Assert.That(second.volume, Is.Zero.Within(0.001f));
    }

    private int PlayingCount() => (first.isPlaying ? 1 : 0) + (second.isPlaying ? 1 : 0);

    private AudioSource ActiveSource() => first.isPlaying ? first : second;

    private AudioSource InactiveSource() => first.isPlaying ? second : first;

    private sealed class GatedSettingsSystem : ISettingsSystem
    {
      private readonly UniTaskCompletionSource ready = new();

      public GatedSettingsSystem()
      {
        AudioSettings = new AudioSettingsModule(this);
        GraphicsSettings = new GraphicsSettingsModule(this);
        GameSettings = new GameSettingsModule(this);
      }

      public UniTask Initialization => ready.Task;
      public AudioSettingsModule AudioSettings { get; }
      public GraphicsSettingsModule GraphicsSettings { get; }
      public GameSettingsModule GameSettings { get; }
      public InputSettingsModule InputSettings => null;
      public ReactiveProperty<bool> IsDirty { get; } = new(false);

      public void MarkReady() => ready.TrySetResult();
      public void Apply(bool autoFinish = true, bool gotoMenu = true) { }
      public void FinishApply(bool gotoMenu = true) { }
      public void DefaultSettings() { }
      public void Cancel() { }
      public void SetDirty(bool isDirty) => IsDirty.Value = isDirty;
    }
  }
}
