using System;
using UnityEditor;
using UnityEngine;
using XNode;

namespace MToolKit.Runtime.VisualGraphs.Authoring
{
  /// <summary>
  ///   Base class for all visual graph nodes. Provides stable GUID for runtime node identification.
  /// </summary>
  [Serializable]
  public abstract class VisualGraphNodeBase : Node
  {
    /// <summary>
    ///   Stable GUID used as runtime NodeId. This ensures node identity persists across authoring changes.
    /// </summary>
    [SerializeField]
    [HideInInspector]
    private string guid = string.Empty;

    /// <summary>
    ///   Stable GUID for this node.
    /// </summary>
    public string Guid
    {
      get => guid;
      private set => guid = value;
    }

    protected virtual void OnValidate()
    {
      EnsureGuid();
    }

    protected override void Init()
    {
      base.Init();
      EnsureGuid();
    }

    private void EnsureGuid()
    {
      if (string.IsNullOrEmpty(guid))
      {
        guid = System.Guid.NewGuid().ToString();
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
      }
    }

    /// <summary>
    ///   Force regenerate GUID for this node. Used when fixing duplicate GUIDs.
    /// </summary>
#if UNITY_EDITOR
    public void RegenerateGuid()
    {
      guid = System.Guid.NewGuid().ToString();
      EditorUtility.SetDirty(this);
    }
#endif
  }
}