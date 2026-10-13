using MToolKit.Runtime.VisualGraphs.Event.Graphs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Rules
{
  /// <summary>Always-available event graph rule, ordered around normal domain graph dispatch.</summary>
  [CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Game Rule", fileName = "GameRule", order = 398)]
  public sealed class GameRuleDefinition : ScriptableObject
  {
    /// <summary>Stable rule identity; must be unique within VisualGraphConfig.GameRules.</summary>
    [Required]
    [Tooltip("Unique stable rule name. Runtime graph identity is rule:<name>; persistent variables use the shared World scope.")]
    public string RuleName;

    /// <summary>The event graph authored through the existing visual graph tools.</summary>
    [Required]
    public EventGraphAsset Graph;

    /// <summary>Ascending priority within the selected timing; ties retain config registration order.</summary>
    public int ExecutionOrder;

    /// <summary>Author switch; a runtime failure additionally disables the rule for its current session.</summary>
    public bool Enabled = true;

    /// <summary>Choose execution before or after normal subscribed domain graphs.</summary>
    public ERuleTiming Timing = ERuleTiming.BeforeDomainGraphs;
  }
}
