using System.Collections.Generic;
using MToolKit.Runtime.Core.Abstractions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Core.Config
{
  [CreateAssetMenu(menuName = "MToolKit/Core/PluginConfigAsset", fileName = "PluginConfig")]
  [InlineEditor]
  public class PluginConfigAsset : ScriptableObject
  {
    [Tooltip("All plugin prefabs must inherit from AbstractGamePlugin")]
    public List<AbstractGamePlugin> PluginPrefabs;
  }
}