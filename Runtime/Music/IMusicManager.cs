
using UnityEngine;

namespace MToolKit.Runtime.Music
{

  public interface IMusicManager
  {
    void PlayMusic(AudioClip audioClip, float duration = 2f);
    void Pause();
    void Resume();
    void Stop();
    bool IsPlaying { get; }
    bool IsPaused { get; }
  }

}