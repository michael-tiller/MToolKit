using System;
using System.Collections.Generic;
using MToolKit.Runtime.Settings.Audio;
using UnityEngine;

namespace MToolKit.Runtime.Audio.Config
{
  /// <summary>Reusable authored settings for a single component audio event.</summary>
  [Serializable]
  public sealed class AudioCueConfig
  {
    public bool enabled = true;
    public List<AudioClip> clips = new();

    [Range(0f, 1f)]
    public float volume = 1f;

    [Range(0.5f, 2f)]
    public float pitch = 1f;

    [Min(0f)]
    public float cooldown = 0.05f;

    public EAudioTypes audioType = EAudioTypes.Interface;
  }
}