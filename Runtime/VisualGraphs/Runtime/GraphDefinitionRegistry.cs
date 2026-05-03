#nullable enable
using System;
using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Dialogue.Definitions;
using MToolKit.Runtime.VisualGraphs.Event.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Runtime
{
  /// <summary>
  ///   Runtime registry of graph definitions that self-register when loaded.
  ///   Eliminates need for manual registry asset - definitions register themselves.
  /// </summary>
  public static class GraphDefinitionRegistry
  {
    private static readonly Dictionary<string, QuestDefinition> questDefinitions = new();
    private static readonly Dictionary<string, DialogueDefinition> dialogueDefinitions = new();
    private static readonly Dictionary<string, EventDefinition> eventDefinitions = new();
    private static bool isInitialized;

    /// <summary>
    ///   Initialize the registry by discovering all definitions.
    ///   Called automatically on startup, but can be called manually for testing.
    ///   Note: Only finds definitions in Resources. Addressables-loaded definitions must register manually.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
      if (isInitialized)
        return;

      // Discover all quest definitions from Resources
      // Note: Addressables-loaded definitions should call RegisterQuestDefinition() manually
      var questDefs = Resources.FindObjectsOfTypeAll<QuestDefinition>();
      foreach (var def in questDefs)
      {
        if (def != null && !string.IsNullOrEmpty(def.Guid))
        {
          questDefinitions[def.Guid] = def;
        }
      }

      // Discover all dialogue definitions from Resources
      // Note: Addressables-loaded definitions should call RegisterDialogueDefinition() manually
      var dialogueDefs = Resources.FindObjectsOfTypeAll<DialogueDefinition>();
      foreach (var def in dialogueDefs)
      {
        if (def != null && !string.IsNullOrEmpty(def.DialogueId))
        {
          dialogueDefinitions[def.DialogueId] = def;
        }
      }

      // Discover all event definitions from Resources
      // Note: Addressables-loaded definitions should call RegisterEventDefinition() manually
      EventDefinition[]? eventDefs = Resources.FindObjectsOfTypeAll<EventDefinition>();
      foreach (var def in eventDefs)
        if (def != null && !string.IsNullOrEmpty(def.EventId))
          eventDefinitions[def.EventId] = def;

      isInitialized = true;
//      UnityEngine.Debug.Log($"GraphDefinitionRegistry initialized: {questDefinitions.Count} quests, {dialogueDefinitions.Count} dialogues, {eventDefinitions.Count} events");
    }

    /// <summary>
    ///   Manually register a quest definition (useful for Addressables or dynamic loading).
    /// </summary>
    public static void RegisterQuestDefinition(QuestDefinition definition)
    {
      if (definition == null)
        throw new ArgumentNullException(nameof(definition));

      if (string.IsNullOrEmpty(definition.Guid))
      {
        UnityEngine.Debug.LogWarning($"QuestDefinition '{definition.name}' has no GUID - cannot register");
        return;
      }

      questDefinitions[definition.Guid] = definition;
    }

    /// <summary>
    ///   Manually register a dialogue definition (useful for Addressables or dynamic loading).
    /// </summary>
    public static void RegisterDialogueDefinition(DialogueDefinition definition)
    {
      if (definition == null)
        throw new ArgumentNullException(nameof(definition));

      if (string.IsNullOrEmpty(definition.DialogueId))
      {
        UnityEngine.Debug.LogWarning($"DialogueDefinition '{definition.name}' has no DialogueId - cannot register");
        return;
      }

      dialogueDefinitions[definition.DialogueId] = definition;
    }

    /// <summary>
    ///   Manually register an event definition (useful for Addressables or dynamic loading).
    /// </summary>
    public static void RegisterEventDefinition(EventDefinition definition)
    {
      if (definition == null)
        throw new ArgumentNullException(nameof(definition));

      if (string.IsNullOrEmpty(definition.EventId))
      {
        UnityEngine.Debug.LogWarning($"EventDefinition '{definition.name}' has no EventId - cannot register");
        return;
      }

      eventDefinitions[definition.EventId] = definition;
    }

    /// <summary>
    ///   Get a quest definition by GUID.
    /// </summary>
    public static QuestDefinition? GetQuestDefinition(string guid)
    {
      if (!isInitialized)
        Initialize();

      return questDefinitions.TryGetValue(guid, out var def) ? def : null;
    }

    /// <summary>
    ///   Get a dialogue definition by ID.
    /// </summary>
    public static DialogueDefinition? GetDialogueDefinition(string dialogueId)
    {
      if (!isInitialized)
        Initialize();

      return dialogueDefinitions.TryGetValue(dialogueId, out var def) ? def : null;
    }

    /// <summary>
    ///   Get all registered quest definitions.
    /// </summary>
    public static IEnumerable<QuestDefinition> GetAllQuestDefinitions()
    {
      if (!isInitialized)
        Initialize();

      return questDefinitions.Values;
    }

    /// <summary>
    ///   Get all registered dialogue definitions.
    /// </summary>
    public static IEnumerable<DialogueDefinition> GetAllDialogueDefinitions()
    {
      if (!isInitialized)
        Initialize();

      return dialogueDefinitions.Values;
    }

    /// <summary>
    ///   Get an event definition by ID.
    /// </summary>
    public static EventDefinition? GetEventDefinition(string eventId)
    {
      if (!isInitialized)
        Initialize();

      return eventDefinitions.TryGetValue(eventId, out EventDefinition? def) ? def : null;
    }

    /// <summary>
    ///   Get all registered event definitions.
    /// </summary>
    public static IEnumerable<EventDefinition> GetAllEventDefinitions()
    {
      if (!isInitialized)
        Initialize();

      return eventDefinitions.Values;
    }

    /// <summary>
    ///   Clear all registrations (useful for testing).
    /// </summary>
    public static void Clear()
    {
      questDefinitions.Clear();
      dialogueDefinitions.Clear();
      eventDefinitions.Clear();
      isInitialized = false;
    }
  }
}

