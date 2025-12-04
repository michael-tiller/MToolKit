using System;
using UnityEngine;

namespace MToolKit.Runtime.Music
{
  /// <summary>
  ///   Thin DI adapter that wraps MusicManager.Instance for dependency injection.
  ///   This allows almost all runtime code to stay DI-pure while only bootstrapping/UI glue
  ///   touches the singleton directly.
  /// </summary>
  public class MusicManagerAdapter : IMusicManager
  {
    private readonly Lazy<GlobalMusicManager> _managerInstance;

    public MusicManagerAdapter()
    {
      _managerInstance = new Lazy<GlobalMusicManager>(() => GlobalMusicManager.Instance);
    }

    private GlobalMusicManager Manager => _managerInstance.Value
      ?? throw new InvalidOperationException("MusicManager is not initialized");

    public void PlayMusic(AudioClip audioClip, float duration = 2f)
    {
      Manager.PlayMusic(audioClip, duration);
    }

    public void Pause()
    {
      Manager.Pause();
    }

    public void Resume()
    {
      Manager.Resume();
    }

    public void Stop()
    {
      Manager.Stop();
    }

    public bool IsPlaying => Manager.IsPlaying;

    public bool IsPaused => Manager.IsPaused;
  }
}

