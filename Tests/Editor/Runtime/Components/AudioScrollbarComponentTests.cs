using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Audio.Interface;
using MToolKit.Runtime.Components;
using MToolKit.Runtime.Settings.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Tests.Editor.Runtime.Components
{
  public class AudioScrollbarComponentTests
  {
    [Test]
    public void AddingComponent_AddsRequiredScrollbar()
    {
      var go = new GameObject("Audio Scrollbar");
      try
      {
        go.AddComponent<AudioScrollbarComponent>();
        Assert.That(go.GetComponent<Scrollbar>(), Is.Not.Null);
      }
      finally { Object.DestroyImmediate(go); }
    }

    [TestCase(Scrollbar.Direction.LeftToRight)]
    [TestCase(Scrollbar.Direction.BottomToTop)]
    public void ValueChanged_PlaysConfiguredInterfaceAudio(Scrollbar.Direction direction)
    {
      var go = new GameObject("Audio Scrollbar");
      var clip = AudioClip.Create("move", 1, 1, 44100, false);
      try
      {
        go.AddComponent<Scrollbar>().direction = direction;
        var component = go.AddComponent<AudioScrollbarComponent>();
        var service = new RecordingAudioService();
        SetField(component, "audioService", service);
        SetField(component, "moveAudioClips", new List<AudioClip> { clip });
        SetField(component, "moveVolume", 0.65f);
        SetField(component, "movePitch", 1.15f);
        SetField(component, "moveCooldown", 0f);
        component.OnPointerDown(null);

        component.GetType().GetMethod("OnValueChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, new object[] { 0.5f });

        Assert.That(service.PlayCount, Is.EqualTo(1));
        Assert.That(service.Volume, Is.EqualTo(0.65f));
        Assert.That(service.Pitch, Is.EqualTo(1.15f));
        Assert.That(service.AudioType, Is.EqualTo(EAudioTypes.Interface));
      }
      finally
      {
        Object.DestroyImmediate(clip);
        Object.DestroyImmediate(go);
      }
    }

    [Test]
    public void ValueChanged_WithoutPointerInteraction_DoesNotPlay()
    {
      var go = new GameObject("Audio Scrollbar");
      var clip = AudioClip.Create("move", 1, 1, 44100, false);
      try
      {
        go.AddComponent<Scrollbar>();
        var component = go.AddComponent<AudioScrollbarComponent>();
        var service = new RecordingAudioService();
        SetField(component, "audioService", service);
        SetField(component, "moveAudioClips", new List<AudioClip> { clip });

        component.GetType().GetMethod("OnValueChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, new object[] { 0.5f });

        Assert.That(service.PlayCount, Is.Zero);
      }
      finally { Object.DestroyImmediate(clip); Object.DestroyImmediate(go); }
    }

    [Test]
    public void ValueChanged_WhenHiddenByCanvasGroup_DoesNotPlay()
    {
      var go = new GameObject("Audio Scrollbar");
      var clip = AudioClip.Create("move", 1, 1, 44100, false);
      try
      {
        var canvasGroup = go.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        go.AddComponent<Scrollbar>();
        var component = go.AddComponent<AudioScrollbarComponent>();
        var service = new RecordingAudioService();
        SetField(component, "audioService", service);
        SetField(component, "moveAudioClips", new List<AudioClip> { clip });
        component.OnPointerDown(null);

        component.GetType().GetMethod("OnValueChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, new object[] { 0.5f });

        Assert.That(service.PlayCount, Is.Zero);
      }
      finally { Object.DestroyImmediate(clip); Object.DestroyImmediate(go); }
    }

    private static void SetField(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private sealed class RecordingAudioService : IAudioService
    {
      public int PlayCount { get; private set; }
      public float Volume { get; private set; }
      public float Pitch { get; private set; }
      public EAudioTypes AudioType { get; private set; }
      public void PlayOneShot(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default) { }
      public void PlayOneShot(IReadOnlyList<AudioClip> clips, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default) { PlayCount++; Volume = volume; Pitch = pitch; AudioType = audioType; }
      public UniTask PlayOneShotAsync(IReadOnlyList<AudioClip> clips, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default) => UniTask.CompletedTask;
      public UniTask PlayOneShotAsync(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default) => UniTask.CompletedTask;
      public void StopAllSounds() { }
    }
  }
}
