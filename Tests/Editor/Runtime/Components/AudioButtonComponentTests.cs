using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Audio.Config;
using MToolKit.Runtime.Audio.Interface;
using MToolKit.Runtime.Components;
using MToolKit.Runtime.Settings.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MToolKit.Tests.Editor.Runtime.Components
{
  public sealed class AudioButtonComponentTests
  {
    [Test]
    public void Events_ConsumeTheirConfiguredCues()
    {
      var go = new GameObject("Button");
      var clip = AudioClip.Create("configured", 1, 1, 44100, false);
      var config = ScriptableObject.CreateInstance<AudioButtonConfig>();
      try
      {
        go.AddComponent<Button>();
        var component = go.AddComponent<AudioButtonComponent>();
        var audio = new RecordingAudioService();
        config.hover.clips.Add(clip);
        config.hover.volume = 0.6f;
        config.hover.pitch = 1.2f;
        config.hover.cooldown = 0f;
        SetField(component, "audioService", audio);
        SetField(component, "audioConfig", config);

        component.OnPointerEnter((PointerEventData)null);

        Assert.That(audio.PlayedClips, Is.EqualTo(new[] { clip }));
        Assert.That(audio.Volume, Is.EqualTo(0.6f));
        Assert.That(audio.Pitch, Is.EqualTo(1.2f));
        Assert.That(audio.AudioType, Is.EqualTo(EAudioTypes.Interface));
      }
      finally
      {
        Object.DestroyImmediate(config);
        Object.DestroyImmediate(clip);
        Object.DestroyImmediate(go);
      }
    }

    [Test]
    public void SwappingPackage_ChangesTheClickSound()
    {
      var go = new GameObject("Button");
      var successClip = AudioClip.Create("success", 1, 1, 44100, false);
      var cancelClip = AudioClip.Create("cancel", 1, 1, 44100, false);
      var success = ScriptableObject.CreateInstance<AudioButtonConfig>();
      var cancel = ScriptableObject.CreateInstance<AudioButtonConfig>();
      try
      {
        var button = go.AddComponent<Button>();
        var component = go.AddComponent<AudioButtonComponent>();
        var audio = new RecordingAudioService();
        success.click.clips.Add(successClip);
        cancel.click.clips.Add(cancelClip);
        success.click.cooldown = 0f;
        cancel.click.cooldown = 0f;
        SetField(component, "audioService", audio);
        SetField(component, "audioConfig", success);
        Invoke(component, "Start");
        button.onClick.Invoke();
        SetField(component, "audioConfig", cancel);
        button.onClick.Invoke();

        Assert.That(audio.PlayedClips, Is.EqualTo(new[] { successClip, cancelClip }));
      }
      finally
      {
        Object.DestroyImmediate(success);
        Object.DestroyImmediate(cancel);
        Object.DestroyImmediate(successClip);
        Object.DestroyImmediate(cancelClip);
        Object.DestroyImmediate(go);
      }
    }

    private static void SetField(object target, string name, object value) =>
      target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private static void Invoke(object target, string name) =>
      target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);

    private sealed class RecordingAudioService : IAudioService
    {
      public readonly List<AudioClip> PlayedClips = new();
      public float Volume { get; private set; }
      public float Pitch { get; private set; }
      public EAudioTypes AudioType { get; private set; }
      public void PlayOneShot(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default) { }
      public void PlayOneShot(IReadOnlyList<AudioClip> clips, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default)
      { PlayedClips.Add(clips[0]); Volume = volume; Pitch = pitch; AudioType = audioType; }
      public UniTask PlayOneShotAsync(IReadOnlyList<AudioClip> clips, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default) => UniTask.CompletedTask;
      public UniTask PlayOneShotAsync(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default) => UniTask.CompletedTask;
      public void StopAllSounds() { }
    }
  }
}
