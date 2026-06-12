using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs.Contexts
{
  /// <summary>
  ///   Quest convenience over <see cref="IGraphContext" /> (9.0.2b). There is deliberately NO IQuestContext —
  ///   quests are <see cref="EGraphContextScope.Graph" /> contexts owned by their quest guid; these are thin
  ///   typed wrappers over <see cref="IGraphContext.Variables" /> for the keys <c>QuestManager</c> itself owns
  ///   (<c>__quest_guid</c>, <c>__quest_definition</c>, the <c>__objective_{guid}_progress</c> mirror).
  ///   The executor-owned <c>objective_{guid}</c> key is NOT wrapped here — objective executors write it and
  ///   <c>QuestDefinition.GetObjectiveProgress</c> reads it; it is not QuestManager's to move.
  /// </summary>
  public static class QuestContextExtensions
  {
    private const string QuestGuidKey = "__quest_guid";
    private const string QuestDefinitionKey = "__quest_definition";

    private static string ObjectiveProgressMirrorKey(string objectiveGuid) => $"__objective_{objectiveGuid}_progress";

    /// <summary>Stamp the quest identity keys executors read from the quest's graph state.</summary>
    public static void SetQuestIdentity(this IGraphContext context, string questGuid, QuestDefinition quest)
    {
      context.Variables.Set(QuestGuidKey, questGuid);
      context.Variables.Set(QuestDefinitionKey, quest);
    }

    /// <summary>The quest guid stamped at start/restore, or null.</summary>
    public static string GetQuestGuid(this IGraphContext context) =>
      context.Variables.Get<string>(QuestGuidKey);

    /// <summary>The quest definition stamped at start/restore, or null.</summary>
    public static QuestDefinition GetQuestDefinition(this IGraphContext context) =>
      context.Variables.Get<QuestDefinition>(QuestDefinitionKey);

    /// <summary>Write the objective-progress mirror (the message-handler path; NOT the executor's <c>objective_{guid}</c>).</summary>
    public static void SetObjectiveProgressMirror(this IGraphContext context, string objectiveGuid, int current) =>
      context.Variables.Set(ObjectiveProgressMirrorKey(objectiveGuid), current);

    /// <summary>Read the objective-progress mirror; <paramref name="fallback" /> when never written.</summary>
    public static int GetObjectiveProgressMirror(this IGraphContext context, string objectiveGuid, int fallback = 0) =>
      context.Variables.Get(ObjectiveProgressMirrorKey(objectiveGuid), fallback);
  }
}
