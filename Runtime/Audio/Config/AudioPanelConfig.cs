using UnityEngine;

namespace MToolKit.Runtime.Audio.Config
{
  /// <summary>Interchangeable open/close sound package for an audio panel.</summary>
  [CreateAssetMenu(menuName = "MToolKit/Audio/Panel Audio Config", fileName = "PanelAudioConfig")]
  public sealed class AudioPanelConfig : ScriptableObject
  {
    public AudioCueConfig open = new();
    public AudioCueConfig close = new();
  }
}