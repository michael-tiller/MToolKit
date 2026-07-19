using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Audio.Interface;
using MToolKit.Runtime.Components;
using MToolKit.Runtime.Settings.Audio;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.Runtime.Components
{
  public class AudioPanelComponentTests
  {
    [Test]
    public void EnablingPanel_PlaysConfiguredInterfaceAudioEachTime()
    {
      var go = new GameObject("Panel");
      var clip = AudioClip.Create("open", 1, 1, 44100, false);
      go.SetActive(false);
      try
      {
        var component = go.AddComponent<AudioPanelComponent>();
        var service = new RecordingAudioService();
        SetField(component, "audioService", service);
        SetField(component, "openAudioClips", new List<AudioClip> { clip });
        SetField(component, "openVolume", 0.7f);
        SetField(component, "openPitch", 1.1f);

        var onEnable = component.GetType().GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)!;
        onEnable.Invoke(component, null);
        onEnable.Invoke(component, null);

        Assert.That(service.PlayCount, Is.EqualTo(2));
        Assert.That(service.Volume, Is.EqualTo(0.7f));
        Assert.That(service.Pitch, Is.EqualTo(1.1f));
        Assert.That(service.AudioType, Is.EqualTo(EAudioTypes.Interface));
      }
      finally { Object.DestroyImmediate(clip); Object.DestroyImmediate(go); }
    }

    [Test]
    public void PlayOpenSound_AllowsShowWithoutActivationToTriggerAudio()
    {
      var go = new GameObject("Panel");
      var clip = AudioClip.Create("open", 1, 1, 44100, false);
      try
      {
        var component = go.AddComponent<AudioPanelComponent>();
        var service = new RecordingAudioService();
        SetField(component, "audioService", service);
        SetField(component, "openAudioClips", new List<AudioClip> { clip });

        component.PlayOpenSound();

        Assert.That(service.PlayCount, Is.EqualTo(1));
      }
      finally { Object.DestroyImmediate(clip); Object.DestroyImmediate(go); }
    }

    [Test]
    public void OnEnable_WhenAutomaticPlaybackIsDisabled_RemainsSilent()
    {
      var go = new GameObject("CanvasGroup Panel");
      var clip = AudioClip.Create("open", 1, 1, 44100, false);
      try
      {
        var component = go.AddComponent<AudioPanelComponent>();
        var service = new RecordingAudioService();
        SetField(component, "audioService", service);
        SetField(component, "openAudioClips", new List<AudioClip> { clip });
        SetField(component, "playOnEnable", false);

        component.GetType().GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null);

        Assert.That(service.PlayCount, Is.Zero);
      }
      finally { Object.DestroyImmediate(clip); Object.DestroyImmediate(go); }
    }

    [Test]
    public void PlayCloseSound_PlaysConfiguredInterfaceAudio()
    {
      var go = new GameObject("Panel");
      var clip = AudioClip.Create("close", 1, 1, 44100, false);
      try
      {
        var component = go.AddComponent<AudioPanelComponent>();
        var service = new RecordingAudioService();
        SetField(component, "audioService", service);
        SetField(component, "closeAudioClips", new List<AudioClip> { clip });
        SetField(component, "closeVolume", 0.8f);
        SetField(component, "closePitch", 0.9f);
        component.PlayCloseSound();
        Assert.That(service.PlayCount, Is.EqualTo(1));
        Assert.That(service.Volume, Is.EqualTo(0.8f));
        Assert.That(service.Pitch, Is.EqualTo(0.9f));
        Assert.That(service.AudioType, Is.EqualTo(EAudioTypes.Interface));
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
