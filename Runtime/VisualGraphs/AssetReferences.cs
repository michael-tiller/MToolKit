using System;
using MToolKit.Runtime.AssetLoader;
using MToolKit.Runtime.VisualGraphs.Dialogue.Definitions;
using MToolKit.Runtime.VisualGraphs.Event.Definitions;
using MToolKit.Runtime.VisualGraphs.Quest.Definitions;

namespace MToolKit.Runtime.VisualGraphs {

  /// <summary>
  /// Type-safe AssetReference for QuestDefinition assets.
  /// Prefer over string name comparison — names change, asset GUIDs don't.
  /// Load at setup time and compare by reference in event handlers.
  /// </summary>
  [Serializable]
  public class AssetReferenceQuestDefinition : AssetReferenceBase<QuestDefinition> {
    public AssetReferenceQuestDefinition(string guid) : base(guid) { }
  }

  /// <summary>
  /// Type-safe AssetReference for DialogueDefinition assets.
  /// </summary>
  [Serializable]
  public class AssetReferenceDialogueDefinition : AssetReferenceBase<DialogueDefinition> {
    public AssetReferenceDialogueDefinition(string guid) : base(guid) { }
  }

  /// <summary>
  /// Type-safe AssetReference for EventDefinition assets.
  /// </summary>
  [Serializable]
  public class AssetReferenceEventDefinition : AssetReferenceBase<EventDefinition> {
    public AssetReferenceEventDefinition(string guid) : base(guid) { }
  }
}
