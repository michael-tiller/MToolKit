using System;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.Audio.Config
{
  [CreateAssetMenu(menuName = "MToolKit/Audio/AudioConfig", fileName = "New AudioConfig")]
  [InlineEditor]
  public class AudioConfig : ScriptableObject
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AudioConfig>().ForFeature("Audio.Config"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    [BoxGroup("Audio Source Prefab")]
    [SerializeField]
    [Tooltip("Direct reference to AudioSource prefab (non-addressable fallback)")]
    private AudioSource audioSourcePrefab;

    [BoxGroup("Audio Source Prefab")]
    [SerializeField]
    [Tooltip("Addressable reference to AudioSource prefab (drag from Addressables window)")]
    private AssetReference audioSourcePrefabReference;

    [BoxGroup("Audio Mixer")]
    [SerializeField]
    [Required]
    [ValidateInput("@masterMixerGroup != null", "Master Mixer Group is required")]
    private AudioMixerGroup masterMixerGroup;

    [BoxGroup("Audio Mixer")]
    [SerializeField]
    [Required]
    [ValidateInput("@musicMixerGroup != null", "Music Mixer Group is required")]
    private AudioMixerGroup musicMixerGroup;

    [BoxGroup("Audio Mixer")]
    [SerializeField]
    [Required]
    [ValidateInput("@gameMixerGroup != null", "Game Mixer Group is required")]
    private AudioMixerGroup gameMixerGroup;

    [BoxGroup("Audio Mixer")]
    [SerializeField]
    [Required]
    [ValidateInput("@interfaceMixerGroup != null", "Interface Mixer Group is required")]
    private AudioMixerGroup interfaceMixerGroup;

    [BoxGroup("Audio Source Pool")]
    [SerializeField]
    [MinValue(1)]
    [MaxValue(50)]
    private int initialPoolSize = 5;

    [BoxGroup("Audio Source Pool")]
    [SerializeField]
    [MinValue(1)]
    [MaxValue(100)]
    private int maxPoolSize = 20;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [MinValue(0f)]
    [MaxValue(1f)]
    private float minTimeBetweenSameSound = 0.1f;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    private bool enableSpatialAudio = true;

    [BoxGroup("Audio Settings")]
    [SerializeField]
    [MinValue(0.1f)]
    [MaxValue(10f)]
    private float spatialAudioRolloffFactor = 1f;

    // Public properties for runtime access
    public AudioMixerGroup MasterMixerGroup => masterMixerGroup;
    public AudioMixerGroup MusicMixerGroup => musicMixerGroup;
    public AudioMixerGroup GameMixerGroup => gameMixerGroup;
    public AudioMixerGroup InterfaceMixerGroup => interfaceMixerGroup;

    public int InitialPoolSize => initialPoolSize;
    public int MaxPoolSize => maxPoolSize;
    public AudioSource AudioSourcePrefab => audioSourcePrefab;
    public AssetReference AudioSourcePrefabReference => audioSourcePrefabReference;

    public float MinTimeBetweenSameSound => minTimeBetweenSameSound;
    public bool EnableSpatialAudio => enableSpatialAudio;
    public float SpatialAudioRolloffFactor => spatialAudioRolloffFactor;

    private void OnValidate()
    {
      // Auto-fix common issues during editor validation
      if (initialPoolSize <= 0)
        initialPoolSize = 5;

      if (maxPoolSize < initialPoolSize)
        maxPoolSize = initialPoolSize;
    }

    [Button("Validate Configuration")]
    [PropertyOrder(100)]
    private void ValidateConfiguration()
    {
      bool isValid = true;

      // Validate mixer groups
      if (masterMixerGroup == null)
      {
        log.Error("Master Mixer Group is not assigned in AudioConfig");
        isValid = false;
      }
      if (musicMixerGroup == null)
      {
        log.Error("Music Mixer Group is not assigned in AudioConfig");
        isValid = false;
      }
      if (gameMixerGroup == null)
      {
        log.Error("Game Mixer Group is not assigned in AudioConfig");
        isValid = false;
      }
      if (interfaceMixerGroup == null)
      {
        log.Error("Interface Mixer Group is not assigned in AudioConfig");
        isValid = false;
      }

      // Validate prefab
      if (audioSourcePrefab == null)
      {
        log.Error("AudioSource Prefab is not assigned in AudioConfig");
        isValid = false;
      }

      // Validate pool sizes
      if (initialPoolSize <= 0)
      {
        log.Error("Initial Pool Size must be greater than 0");
        isValid = false;
      }

      if (maxPoolSize < initialPoolSize)
      {
        log.Error("Max Pool Size must be greater than or equal to Initial Pool Size");
        isValid = false;
      }

      if (isValid)
        log.Information("AudioConfig validation passed successfully");
      else
        log.Error("AudioConfig validation failed - please fix the issues above");
    }

    [Button("Auto-Fix Pool Sizes")]
    [PropertyOrder(101)]
    private void AutoFixPoolSizes()
    {
      if (initialPoolSize <= 0)
      {
        initialPoolSize = 5;
        log.Information("Fixed Initial Pool Size to 5");
      }

      if (maxPoolSize < initialPoolSize)
      {
        maxPoolSize = initialPoolSize;
        log.Information("Fixed Max Pool Size to {0}", initialPoolSize);
      }
    }
  }
}