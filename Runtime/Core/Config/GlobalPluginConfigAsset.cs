using System.Collections.Generic;
using MToolKit.Runtime.Core.Abstractions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Core.Config
{
  /// <summary>
  ///   Configuration asset for global plugins that persist across all scenes.
  ///   These plugins are instantiated by GlobalInstaller and provide core services.
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Core/GlobalPluginConfigAsset", fileName = "GlobalPluginConfig")]
  [InlineEditor]
  public class GlobalPluginConfigAsset : ScriptableObject
  {
    [Tooltip("Global plugin prefabs that will be instantiated and persist across all scenes")]
    public List<AbstractGamePlugin> GlobalPluginPrefabs;
  }
}