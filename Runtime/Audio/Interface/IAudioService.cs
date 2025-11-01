using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MToolKit.Runtime.Settings.Audio;

namespace MToolKit.Runtime.Audio.Interface
{
  public interface IAudioService
  {
    void PlayOneShot(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default);
    void PlayOneShot(IReadOnlyList<AudioClip> clips, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default);
    UniTask PlayOneShotAsync(IReadOnlyList<AudioClip> clips, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default);
    UniTask PlayOneShotAsync(AudioClip clip, Vector3? position = null, float volume = 1f, float pitch = 1f, EAudioTypes audioType = EAudioTypes.Game, CancellationToken ct = default);
    void StopAllSounds();
  }
}