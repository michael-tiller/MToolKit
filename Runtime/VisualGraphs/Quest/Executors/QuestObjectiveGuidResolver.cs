using System;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Runtime.VisualGraphs.Quest.Executors
{
  /// <summary>
  ///   Maps the objective GUID a graph node points at onto the GUID the *running* quest actually
  ///   uses for its progress.
  ///
  ///   Per-instance quest clones (e.g. one per dungeon layer) re-GUID their objectives as
  ///   <c>{templateGuid}::{instanceSuffix}</c> and own runtime-instantiated
  ///   <see cref="QuestObjective"/> copies. Those copies are not Addressable assets, so the
  ///   graph's <c>ObjectiveAssetReference</c> can only ever resolve to the shared TEMPLATE asset
  ///   and yields the bare template GUID.
  ///
  ///   Without this remap the clone's graph writes progress under <c>objective_{templateGuid}</c>
  ///   while <see cref="QuestDefinition.GetObjectiveProgress"/> reads
  ///   <c>objective_{templateGuid}::{suffix}</c> — so a cloned quest increments a key nobody reads
  ///   and can never complete. The failure is silent: the objective graph reports progress
  ///   correctly against the template while the quest the player holds stays at zero forever.
  /// </summary>
  public static class QuestObjectiveGuidResolver
  {
    /// <summary>Graph-state key holding the definition of the quest this graph belongs to.</summary>
    private const string QuestDefinitionKey = "__quest_definition";

    /// <summary>
    ///   Returns the objective GUID to key progress under for the quest currently executing.
    ///   Returns <paramref name="objectiveGuid"/> unchanged when the running quest owns that exact
    ///   GUID (the normal, non-cloned case) or when no quest definition is in state.
    /// </summary>
    /// <param name="state">Graph state for the running quest instance.</param>
    /// <param name="objectiveGuid">GUID resolved from the node's objective asset reference.</param>
    /// <returns>The running quest's own GUID for that objective.</returns>
    public static string Resolve(IGraphState state, string objectiveGuid)
    {
      if (state == null || string.IsNullOrEmpty(objectiveGuid)) return objectiveGuid;
      if (!state.TryGet<QuestDefinition>(QuestDefinitionKey, out var questDef)) return objectiveGuid;
      if (questDef?.Objectives == null || questDef.Objectives.Count == 0) return objectiveGuid;

      // Exact match wins — an uncloned quest must behave exactly as before.
      for (int i = 0; i < questDef.Objectives.Count; i++)
      {
        var objective = questDef.Objectives[i];
        if (objective != null && objective.Guid == objectiveGuid) return objectiveGuid;
      }

      // Otherwise look for this quest's suffixed copy of that objective.
      var prefix = objectiveGuid + "::";
      for (int i = 0; i < questDef.Objectives.Count; i++)
      {
        var guid = questDef.Objectives[i]?.Guid;
        if (!string.IsNullOrEmpty(guid) && guid.StartsWith(prefix, StringComparison.Ordinal))
          return guid;
      }

      return objectiveGuid;
    }
  }
}
