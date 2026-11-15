using System;
using MToolKit.Runtime.AssetLoader;
using UnityEngine;

namespace MToolKit.ContextMenus.Configuration
{
  /// <summary>Uses MTK's typed Addressables contract; loading and release remain the host's responsibility.</summary>
  [Serializable]
  public sealed class ContextMenuPrefabReference : AssetReferenceBase<GameObject>
  {
    public ContextMenuPrefabReference(string guid) : base(guid) { }
  }
}
