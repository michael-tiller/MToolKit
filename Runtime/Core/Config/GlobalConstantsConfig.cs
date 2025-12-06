using MToolKit.Runtime.AssetLoader;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Core.Config
{
  [CreateAssetMenu(menuName = "MToolKit/Core/GlobalConstantsConfigAsset", fileName = "New GlobalConstantsConfig")]
  [InlineEditor]
  public class GlobalConstantsConfigAsset : ScriptableObject
  {
    [TabGroup("Bootstrapper Settings")]
    [Min(0.01f)]
    [field: SerializeField]
    public float BootstrapperTimeout { get; private set; } = 5f;

    [TabGroup("Bootstrapper Settings")]
    [field: SerializeField]
    public bool AutoLoad { get; private set; }

    [TabGroup("Scene Management")]
    [field: SerializeField]
    [Required]
    public AssetReferenceScene MenuSceneReference { get; private set; }

    [TabGroup("Scene Management")]
    [field: SerializeField]
    [Required]
    public AssetReferenceScene GameplaySceneReference { get; private set; }

    [TabGroup("Performance Settings")]
    [field: SerializeField]
    [Min(1)]
    public int MaxRetryAttempts { get; private set; } = 3;

    [TabGroup("Performance Settings")]
    [field: SerializeField]
    [Min(0.01f)]
    public float RetryDelaySeconds { get; private set; } = 1f;

    [TabGroup("Performance Settings")]
    [field: SerializeField]
    [Range(1, 1000)]
    public float PerformanceThresholdMs { get; private set; } = 16.67f; // 60 FPS

    [TabGroup("Environment Settings")]
    [field: SerializeField]
    public bool EnablePerformanceProfiling { get; private set; }
  }
}