using UnityEngine;

namespace MToolKit.Runtime.Audio.Config
{
  /// <summary>Interchangeable click/select/hover sound package for an audio button.</summary>
  [CreateAssetMenu(menuName = "MToolKit/Audio/Button Audio Config", fileName = "ButtonAudioConfig")]
  public sealed class AudioButtonConfig : ScriptableObject
  {
    public AudioCueConfig click = new();
    public AudioCueConfig select = new();
    public AudioCueConfig hover = new();
  }
}