using MToolKit.Runtime.VisualGraphs.Authoring;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;
using XNode;
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

namespace MToolKit.Runtime.VisualGraphs.Quest.Nodes
{
  /// <summary>
  ///   Sets progress for a specific quest objective to an exact value.
  ///   Example: Set "Find Sword" objective to 1 (complete)
  ///   Uses GUID-based references (safe, no typos!).
  /// </summary>
  [CreateNodeMenu("Quest/Objective/Set Progress")]
  [NodeTint("#6B8FA8")]
  [NodeWidth(400)]
  public sealed class QuestObjectiveSetNode : VisualGraphNodeBase
  {
    [Input(connectionType = ConnectionType.Multiple)]
    public NodeConnection Input;

    [Output(connectionType = ConnectionType.Multiple)]
    public NodeConnection Output;

    [BoxGroup("Objective")]
    [InfoBox("The quest objective to set. Can be loaded via Addressables.")]
    [Required]
    public ObjectiveAssetReference Objective;

    [BoxGroup("Objective")]
    [MinValue(0)]
    [Tooltip("Progress value to set")]
    public int Value = 0;

    [BoxGroup("Options")]
    [Tooltip("If true, emits QuestObjectiveProgressMessage to MessagePipe")]
    public bool EmitProgressEvent = true;

    public override object GetValue(NodePort port)
    {
      return null;
    }
  }
}

