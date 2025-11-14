using MToolKit.Runtime.Utilities;
using MToolKit.Runtime.VisualGraphs.Quest.Graphs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Quest.Definitions
{
  /// <summary>
  ///   Defines a single trackable objective within a quest.
  ///   Example: "Kill 5 Goblins", "Collect 3 Herbs"
  ///   Uses GUID for safe references (no string ID typos!).
  /// </summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Quests/Quest Objective", fileName = "QuestObjective", order = 292)]
  [InlineEditor]
  public sealed class QuestObjective : GuidScriptableObject
  {
    [BoxGroup("Display")]
    [TextArea(2, 5)]
    [Tooltip("Description shown in quest log")]
    public string Description = "";

    [BoxGroup("Display")]
    [PreviewField(50, ObjectFieldAlignment.Left)]
    [Tooltip("Icon displayed in UI")]
    public Sprite Icon;

    [BoxGroup("Progress")]
    [MinValue(1)]
    [Tooltip("Amount of progress required to complete this objective")]
    public int RequiredProgress = 1;

    [BoxGroup("Graph")]
    [Required]
    [Tooltip("Graph that defines how this objective progresses (e.g., OnEnemyDefeated → check if turnip → increment)")]
    public QuestGraphAsset ObjectiveGraph;

    [BoxGroup("Options")]
    [Tooltip("If true, quest can complete without finishing this objective")]
    public bool Optional;

    [BoxGroup("Options")]
    [Tooltip("If true, objective is hidden until revealed by graph logic")]
    public bool Hidden;

    public override string ToString()
    {
      var optional = Optional ? " [Optional]" : "";
      var hidden = Hidden ? " [Hidden]" : "";
      return $"{DisplayName} (0/{RequiredProgress}){optional}{hidden}";
    }
  }
}

