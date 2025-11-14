# VisualGraphs Subsystem - Production Roadmap

## Current Status

**Overall Completeness: 75% (Alpha Quality)**

**Core Architecture: 100%** ✅ - Production-ready, event-driven, POCO-based graph execution  
**MToolKit Integration: 15%** ⚠️ - Missing plugin patterns, save system, messaging integration  
**Quest Features: 40%** ⚠️ - Basic stage tracking works, missing progress tracking, conditions, rewards  
**Test Coverage: 0%** ❌ - No tests written (target: 100%)

---

## Phase 1: Critical Integration (Foundation)

**Goal:** Integrate with MToolKit's core patterns so the system works with existing infrastructure

### 1.0 Core Architecture Fixes (IMMEDIATE - Before Integration)

**These must be done first to avoid rework later:**

#### 1.0.1 Asset Reference System (Meta GUID-based)

**Current Problem:**

```csharp
// XNodeGraphExporter.cs line 221
object NormalizeUnityObject(UnityEngine.Object obj) {
    // TODO: Add addressable key extraction if needed
    return obj != null ? obj.name : null;  // ❌ NOT DETERMINISTIC
}
```

**Issues:**
- Names can change, breaking graphs
- Duplicate names cause conflicts
- Not deterministic across environments
- Can't validate if asset is deleted

**Solution:**
- [ ] Extract Unity asset meta GUID from .meta file
- [ ] Store `AssetReference { guid, path, name, addressableKey }` in RuntimeNodeDefinition
- [ ] Validate all asset references during export
- [ ] Error if asset missing or GUID extraction fails
- [ ] Support addressable key extraction if asset is addressable
- [ ] Create `IAssetReferenceResolver` for runtime loading

**See Phase 4 in roadmap for full implementation details.**

---

#### 1.0.2 Explicit Graph Subscriptions

**Current Problem:**

```csharp
// Subscriptions inferred from nodes during export
if (node is QuestOnEventNode questEventNode) {
    def.Subscriptions.Add(new RuntimeSubscriptionDefinition {
        EventType = questEventNode.EventType,
        EventDomain = questEventNode.EventDomain
    });
}
```

**Issues:**
- Accidental subscriptions if node added but not connected
- No graph-level visibility of what events trigger the graph
- Subscriptions scattered across multiple nodes
- Hard to audit what events a graph listens to

**Solution:**
- [ ] Add `Subscriptions` block to graph asset (authoring)
  ```csharp
  [Serializable]
  public class GraphSubscription {
      public string eventType;
      public string eventDomain;
      public bool required;  // Error if no matching entry node
  }
  
  public class QuestGraphAsset : NodeGraph {
      [BoxGroup("Subscriptions")]
      [InfoBox("Explicit subscriptions - graph only runs for these events")]
      public List<GraphSubscription> subscriptions = new();
  }
  ```
- [ ] Export subscriptions from graph-level list, NOT from nodes
- [ ] Validate that entry nodes exist for required subscriptions
- [ ] Entry nodes become "entry points" not "subscription declarers"
- [ ] Editor tool to auto-populate subscriptions from existing entry nodes (migration)

**Files to Modify:**
- `Authoring/Graphs/QuestGraphAsset.cs` - Add subscriptions list
- `Authoring/Graphs/DialogueGraphAsset.cs` - Add subscriptions list
- `Export/XNodeGraphExporter.cs` - Export from graph.subscriptions, not nodes
- `Export/XNodeGraphExporter.cs` - Validate entry nodes exist for subscriptions

---

#### 1.0.3 Per-Graph Execution Limits

**Current Problem:**

```csharp
// GraphRunner.cs line 14
private const int MaxExecutionSteps = 1024;  // ❌ HARDCODED
```

**Issues:**
- All graphs have same step limit
- Simple graphs waste budget
- Complex graphs might need more
- Can't tune per-graph
- Not visible in authoring

**Solution:**
- [ ] Add `maxExecutionSteps` to graph definitions
  ```csharp
  public class QuestDefinition : ScriptableObject {
      [BoxGroup("Performance")]
      [Tooltip("Max nodes executed per event (prevents infinite loops)")]
      [Range(64, 4096)]
      public int maxExecutionSteps = 1024;
  }
  
  public class RuntimeGraphDefinition {
      public int MaxExecutionSteps = 1024;
  }
  ```
- [ ] GraphRunner reads from definition, not constant
  ```csharp
  var maxSteps = _definition.MaxExecutionSteps;
  while (queue.TryDequeue(out var nodeId)) {
      if (++steps > maxSteps) { ... }
  }
  ```
- [ ] Add to VisualGraphConfig for global default
  ```csharp
  public class VisualGraphConfig : ScriptableObject {
      [BoxGroup("Performance")]
      public int defaultMaxExecutionSteps = 1024;
      public int maxAllowedSteps = 4096;
  }
  ```
- [ ] Validate at export: definition.maxSteps <= config.maxAllowedSteps

**Files to Modify:**
- `Definitions/QuestDefinition.cs` - Add maxExecutionSteps field
- `Definitions/DialogueDefinition.cs` - Add maxExecutionSteps field
- `Runtime/DTOs/RuntimeGraphDefinition.cs` - Add MaxExecutionSteps property
- `Export/XNodeGraphExporter.cs` - Copy maxExecutionSteps to runtime def
- `Runtime/GraphRunner.cs` - Use definition.MaxExecutionSteps instead of const

---

#### 1.0.4 Addressables Loading Implementation

**Current Problem:**

```csharp
// QuestDefinition.cs
public string addressableKey;  // ❌ DEFINED BUT UNUSED

// VisualGraphBootstrapMB.cs - Loads from direct reference only
var runtimeDef = exporter.Export(questDef.graphAsset);  // Direct ref
```

**Issues:**
- Addressable keys exist but aren't used
- All graphs loaded at startup (no dynamic loading)
- Can't load graphs on-demand
- No hot update support

**Solution:**
- [ ] Implement lazy graph loading
  ```csharp
  public class VisualGraphBootstrapMB : MonoBehaviour {
      public bool loadAllGraphsOnStartup = true;
      
      private void Awake() {
          if (loadAllGraphsOnStartup) {
              LoadAllGraphs();
          }
      }
      
      public async UniTask LoadGraphAsync(string graphId) {
          var def = registry.GetDefinition(graphId);
          if (!string.IsNullOrEmpty(def.addressableKey)) {
              // Load via addressables
              var graphAsset = await Addressables.LoadAssetAsync<NodeGraph>(
                  def.addressableKey).ToUniTask();
              InitializeGraph(def, graphAsset);
          } else {
              // Use direct reference
              InitializeGraph(def, def.graphAsset);
          }
      }
  }
  ```
- [ ] Add `IGraphLoader` service
  ```csharp
  public interface IGraphLoader {
      UniTask<IGraphRunner> LoadGraphAsync(string graphId, CancellationToken ct);
      void UnloadGraph(string graphId);
      bool IsLoaded(string graphId);
  }
  ```
- [ ] Support addressable asset references in nodes (see 1.0.1)
- [ ] Add unload/cleanup when graph no longer needed

**Files to Create:**
- `Runtime/VisualGraphs/Loading/IGraphLoader.cs`
- `Runtime/VisualGraphs/Loading/GraphLoader.cs`

**Files to Modify:**
- `Bootstrap/VisualGraphBootstrapMB.cs` - Implement async loading
- `Installer/VisualGraphInstaller.cs` - Register IGraphLoader

---

### 1.1 Plugin Architecture Integration

**Current:** `VisualGraphBootstrapMB` is a plain MonoBehaviour  
**Target:** Full plugin lifecycle with proper initialization

- [ ] Create `VisualGraphPlugin : DomainPlugin<GraphEventRouter, IGraphEventRouter>`
  - Implement plugin lifecycle (Setup → RuntimeInit → Tick → Shutdown)
  - Move bootstrap logic from `VisualGraphBootstrapMB` to plugin
  - Register with `PluginRegistry`
  - Add dependency validation via `IDependencyDeclaration`
  
- [ ] Create `IGraphEventRouter` interface
  - Extract interface from `GraphEventRouter` concrete class
  - Expose `RegisterRunner`, `RouteAsync`, `GetRunners`, `Clear`
  - Update installer to register as interface

- [ ] Add `VisualGraphConfig` ScriptableObject
  - `bool enableVerboseLogging`
  - `int maxExecutionStepsPerGraph = 1024`
  - `bool validateGraphsOnStartup = true`
  - `bool autoInitializeFromRegistry = true`
  - `VisualGraphRegistry defaultRegistry`
  - `CreateAssetMenu` at `MToolKit/Visual Graphs/Config`

**Files to Create:**
- `Runtime/VisualGraphs/VisualGraphPlugin.cs`
- `Runtime/VisualGraphs/Interfaces/IGraphEventRouter.cs`
- `Runtime/VisualGraphs/Config/VisualGraphConfig.cs`

**Files to Modify:**
- `Installer/VisualGraphInstaller.cs` - Register plugin, config
- `Bootstrap/VisualGraphBootstrapMB.cs` - Simplify to bridge only

---

### 1.2 Save System Integration

**Current:** `GraphStateSaveProvider` exists but not connected  
**Target:** Full save/load with ES3 integration

- [ ] Implement `ISaveDomainController` on `GraphStateSaveController`
  - Create new `GraphStateSaveController : ISaveDomainController`
  - Add `ESaveDomain.Graphs` to enum (or use `ESaveDomain.World`)
  - Implement `SaveAsync()` - capture all graph states
  - Implement `LoadAsync()` - restore all graph states
  - Use `IES3Service` for actual serialization
  
- [ ] Register with `SaveSystemCoordinator`
  - Auto-register in plugin `PerformRuntimeInitialization`
  - Ensure save coordinator exists before registration
  - Handle unregister on shutdown

- [ ] Delete `GraphStateSaveProvider` (replaced by controller)

- [ ] Test save/load cycle
  - Start quest, set stage
  - Save game
  - Reload scene
  - Verify quest stage persists

**Files to Create:**
- `Runtime/VisualGraphs/Persistence/GraphStateSaveController.cs`

**Files to Delete:**
- `Runtime/VisualGraphs/Persistence/GraphStateSaveProvider.cs`

**Files to Modify:**
- `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Register controller
- `Runtime/Persistence/Enums/ESaveDomain.cs` - Add Graphs enum (if needed)

---

### 1.3 MessagePipe / R3 Event Bus Integration ⚠️ **CRITICAL**

**Current:** Stub implementations with TODO comments  
**Target:** Full bidirectional plugin-to-plugin communication

**This is THE critical integration** - without this, graphs are isolated and can't communicate with other MToolKit plugins.

#### Plugin Communication Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      MessagePipe Event Bus                   │
│         (Cross-plugin communication backbone)                │
└─────────────────────────────────────────────────────────────┘
         ▲              ▲              ▲              ▲
         │              │              │              │
    [Publish]      [Publish]      [Subscribe]    [Subscribe]
         │              │              │              │
         │              │              │              │
    ┌────┴───┐    ┌────┴───┐    ┌────┴───┐    ┌────┴───┐
    │ Quest  │    │Dialogue│    │ Player │    │   UI   │
    │ Graph  │    │ Graph  │    │ Plugin │    │ Plugin │
    └────────┘    └────────┘    └────────┘    └────────┘
         │              │              │              │
    [Subscribe]    [Subscribe]    [Publish]      [Publish]
         │              │              │              │
         ▼              ▼              ▼              ▼
┌─────────────────────────────────────────────────────────────┐
│                      MessagePipe Event Bus                   │
└─────────────────────────────────────────────────────────────┘
```

#### Example Communication Flows

**Quest → Player → UI:**
```
QuestGraph emits Quest.TaskComplete
    ↓ (MessagePipe)
PlayerPlugin subscribes → Updates player stats
    ↓ (MessagePipe)
UIPlugin subscribes → Updates quest tracker UI
```

**Player → Quest:**
```
PlayerPlugin emits Player.EnteredZone
    ↓ (MessagePipe)
QuestGraph subscribes → Advances quest stage
    ↓ (MessagePipe)
QuestGraph emits Quest.StageAdvanced
    ↓ (MessagePipe)
UIPlugin subscribes → Shows notification
```

**Combat → Quest → Inventory:**
```
CombatPlugin emits Enemy.Defeated
    ↓ (MessagePipe)
QuestGraph subscribes → Increments kill count (3/5)
    ↓ (when 5/5 reached)
QuestGraph emits Quest.TaskComplete
    ↓ (MessagePipe)
QuestGraph emits Quest.Complete
    ↓ (MessagePipe)
InventoryPlugin subscribes → Grants quest reward items
PlayerPlugin subscribes → Grants experience points
UIPlugin subscribes → Shows quest complete popup
```

#### Implementation Tasks

- [ ] **Create graph-specific message types**
  - `GraphEventMessage` - Base for all graph events
  - `QuestStateChangedMessage` - Quest stage/task changes
  - `QuestCompletedMessage` - Quest completion with rewards
  - `DialogueStateChangedMessage` - Dialogue state
  - `DialogueChoiceSelectedMessage` - Player choice with metadata
  - `GraphExecutionEventMessage` - Debug/diagnostic events
  - Register all brokers in installer

- [ ] **Connect `EventBusBridgeMB` to R3** (Graphs RECEIVE from other plugins)
  - Remove TODO, implement actual subscription
  - Subscribe to `IPublisher<IEventMessage>` from MessagePipe
  - Filter events by graph subscriptions
  - Route matching events to `GraphEventRouter`
  - Handle disposal properly on destroy

- [ ] **Replace `SimpleEventEmitter`** (Graphs SEND to other plugins)
  - Create `MessagePipeEventEmitter : IEventEmitter`
  - Inject `IPublisher<GraphEventMessage>`
  - Emit graph events to MessagePipe
  - Other plugins subscribe to `ISubscriber<GraphEventMessage>`
  - Support filtering by graph ID / domain

- [ ] **Add R3 reactive observables for graph state**
  - `ReactiveProperty<Dictionary<string, QuestState>> ActiveQuests`
  - `ReactiveProperty<string> CurrentDialogueId`
  - `Subject<QuestTaskProgress>` for task updates (0/5 → 1/5)
  - Expose via `IGraphStateObserver` interface
  - Other plugins can bind UI to these observables

- [ ] **Create plugin communication examples**
  - Example: PlayerPlugin + QuestGraph integration
  - Example: CombatPlugin triggers quest objectives
  - Example: InventoryPlugin checks quest requirements
  - Example: DialogueGraph affects RelationshipPlugin
  - Document message contracts between plugins

#### Message Type Examples

```csharp
// Quest messages
public sealed class QuestStateChangedMessage {
    public string QuestId { get; set; }
    public string StageKey { get; set; }
    public int StageValue { get; set; }
}

public sealed class QuestTaskProgressMessage {
    public string QuestId { get; set; }
    public string TaskId { get; set; }
    public int Current { get; set; }
    public int Required { get; set; }
}

public sealed class QuestCompletedMessage {
    public string QuestId { get; set; }
    public List<QuestReward> Rewards { get; set; }
    public DateTime CompletionTime { get; set; }
}

// Dialogue messages
public sealed class DialogueStartedMessage {
    public string DialogueId { get; set; }
    public string NpcId { get; set; }
}

public sealed class DialogueChoiceSelectedMessage {
    public string DialogueId { get; set; }
    public string ChoiceId { get; set; }
    public int ChoiceIndex { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}

// Events other plugins emit that graphs listen for
public sealed class PlayerEnteredZoneMessage {
    public string ZoneId { get; set; }
    public Vector3 Position { get; set; }
}

public sealed class EnemyDefeatedMessage {
    public string EnemyId { get; set; }
    public string EnemyType { get; set; }
    public int ExperienceGranted { get; set; }
}

public sealed class ItemAcquiredMessage {
    public string ItemId { get; set; }
    public int Quantity { get; set; }
    public string Source { get; set; } // "Loot", "Quest", "Craft", etc.
}
```

**Files to Create:**
- `Runtime/VisualGraphs/Messages/GraphEventMessage.cs`
- `Runtime/VisualGraphs/Messages/QuestStateChangedMessage.cs`
- `Runtime/VisualGraphs/Messages/QuestTaskProgressMessage.cs`
- `Runtime/VisualGraphs/Messages/QuestCompletedMessage.cs`
- `Runtime/VisualGraphs/Messages/DialogueStateChangedMessage.cs`
- `Runtime/VisualGraphs/Messages/DialogueStartedMessage.cs`
- `Runtime/VisualGraphs/Messages/DialogueChoiceSelectedMessage.cs`
- `Runtime/VisualGraphs/Integration/MessagePipeEventEmitter.cs`
- `Runtime/VisualGraphs/Interfaces/IGraphStateObserver.cs`
- `Runtime/VisualGraphs/Examples/PluginCommunicationExamples.cs`

**Files to Modify:**
- `Bootstrap/EventBusBridgeMB.cs` - Implement subscription
- `Installer/VisualGraphInstaller.cs` - Register real emitter, message brokers
- `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Register message brokers in Setup()

---

## Phase 2: Quest System Enhancements

**Goal:** Add staged task progress tracking, conditions, and rewards

### 2.1 Quest Progress Tracking System

**Current:** Quests only track stage integers  
**Target:** Track individual task progress with "X/Y complete" display

- [ ] Create task progress data structures
  ```csharp
  [Serializable]
  public sealed class QuestTaskProgress {
      public string taskId;
      public int current;
      public int required;
      public bool IsComplete => current >= required;
      public float Percentage => (float)current / required;
  }
  
  public interface IQuestProgressState {
      void SetTaskProgress(string questId, string taskId, int current, int required);
      QuestTaskProgress GetTaskProgress(string questId, string taskId);
      IReadOnlyList<QuestTaskProgress> GetAllTasksForQuest(string questId);
      bool AreAllTasksComplete(string questId);
  }
  ```

- [ ] Extend `IGraphState` to support nested progress data
  - Add `SetTaskProgress<T>` method
  - Add `GetTaskProgress<T>` method
  - Store as `Dictionary<string, QuestTaskProgress>` per quest

- [ ] Create new quest nodes for task tracking
  - `QuestTaskIncrementNode` - Increment task progress
  - `QuestTaskSetNode` - Set task progress directly
  - `QuestTaskCheckNode` - Conditional branch based on task completion
  - `QuestAllTasksCompleteNode` - Check if all tasks done

- [ ] Create executors for task nodes
  - `QuestTaskIncrementNodeExecutor`
  - `QuestTaskSetNodeExecutor`
  - `QuestTaskCheckNodeExecutor`
  - `QuestAllTasksCompleteNodeExecutor`

- [ ] Add task definitions to `QuestDefinition`
  ```csharp
  [Serializable]
  public class QuestTaskDefinition {
      public string taskId;
      public string displayName;
      public int requiredCount;
      public bool optional;
  }
  
  public List<QuestTaskDefinition> tasks;
  ```

- [ ] Emit progress events
  - `Quest.TaskProgressUpdated` with current/required values
  - `Quest.TaskCompleted` when task reaches required
  - `Quest.AllTasksCompleted` when all non-optional tasks done

**Files to Create:**
- `Runtime/VisualGraphs/Quest/QuestTaskProgress.cs`
- `Runtime/VisualGraphs/Quest/IQuestProgressState.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestTaskIncrementNode.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestTaskSetNode.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestTaskCheckNode.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestAllTasksCompleteNode.cs`
- `Runtime/VisualGraphs/Executors/QuestTaskIncrementNodeExecutor.cs`
- `Runtime/VisualGraphs/Executors/QuestTaskSetNodeExecutor.cs`
- `Runtime/VisualGraphs/Executors/QuestTaskCheckNodeExecutor.cs`
- `Runtime/VisualGraphs/Executors/QuestAllTasksCompleteNodeExecutor.cs`

**Files to Modify:**
- `Definitions/QuestDefinition.cs` - Add task list
- `Runtime/State/InMemoryGraphState.cs` - Support nested progress data

---

### 2.2 Quest Conditions & Requirements

- [ ] Create condition evaluation system
  - `IQuestCondition` interface
  - `QuestConditionEvaluator` service
  - Support for: level requirements, item checks, other quest completion

- [ ] Create condition nodes
  - `QuestCheckConditionNode` - Evaluate condition, branch accordingly
  - `QuestWaitForConditionNode` - Pause until condition met

- [ ] Add conditions to quest definitions
  ```csharp
  public List<QuestConditionDefinition> startConditions;
  public List<QuestConditionDefinition> completionConditions;
  ```

**Files to Create:**
- `Runtime/VisualGraphs/Quest/IQuestCondition.cs`
- `Runtime/VisualGraphs/Quest/QuestConditionEvaluator.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestCheckConditionNode.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestWaitForConditionNode.cs`

---

### 2.3 Quest Rewards System

- [ ] Create reward data structures
  ```csharp
  [Serializable]
  public class QuestReward {
      public QuestRewardType type;
      public string itemId;
      public int quantity;
      public int experiencePoints;
      public int currencyAmount;
  }
  ```

- [ ] Create reward grant node
  - `QuestGrantRewardNode` - Awards rewards to player

- [ ] Add rewards to quest definitions
  ```csharp
  public List<QuestReward> rewards;
  public List<QuestReward> optionalRewards;
  ```

- [ ] Emit reward events
  - `Quest.RewardGranted` with reward details

**Files to Create:**
- `Runtime/VisualGraphs/Quest/QuestReward.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestGrantRewardNode.cs`
- `Runtime/VisualGraphs/Executors/QuestGrantRewardNodeExecutor.cs`

---

## Phase 3: Dialogue System Completion

**Goal:** Implement real UI integration and proper choice branching

### 3.1 Dialogue UI Service Interface

**Current:** TODOs in dialogue executors  
**Target:** Full dialogue UI integration

- [ ] Create `IDialogueUIService` interface
  ```csharp
  public interface IDialogueUIService {
      UniTask ShowLineAsync(string speakerId, string text, CancellationToken ct);
      UniTask<int> ShowChoicesAsync(List<string> choices, CancellationToken ct);
      void HideDialogue();
      ReactiveProperty<bool> IsDialogueActive { get; }
  }
  ```

- [ ] Implement `DialogueLineNodeExecutor` properly
  - Remove `UniTask.Delay` stub
  - Resolve `IDialogueUIService` from context
  - Call `ShowLineAsync` with proper await

- [ ] Implement `DialogueChoiceNodeExecutor` properly
  - Remove hardcoded `selectedIndex = 0`
  - Resolve `IDialogueUIService`
  - Call `ShowChoicesAsync` and await player choice
  - Branch to selected output port only (not all)

- [ ] Fix choice port filtering
  - Track which output port corresponds to which choice index
  - Use `PortName` from `RuntimeConnectionDefinition`
  - Only enqueue the selected branch

**Files to Create:**
- `Runtime/VisualGraphs/Interfaces/IDialogueUIService.cs`

**Files to Modify:**
- `Executors/DialogueLineNodeExecutor.cs` - Remove TODO, implement properly
- `Executors/DialogueChoiceNodeExecutor.cs` - Remove TODO, implement properly, fix branching

---

### 3.2 Dialogue Advanced Features

- [ ] Create dynamic port support for choices
  - `DialogueChoiceNode` should have variable number of output ports
  - One output per choice option
  - Export port names to runtime connections

- [ ] Add speaker portraits/avatars
  - `speakerAvatarKey` parameter in `DialogueLineNode`
  - Addressable reference support

- [ ] Add dialogue animations/effects
  - `DialogueAnimationNode` - Trigger character animations
  - `DialogueWaitNode` - Pause for duration
  - `DialogueCameraNode` - Adjust camera during dialogue

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Dialogue/DialogueAnimationNode.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Dialogue/DialogueWaitNode.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Dialogue/DialogueCameraNode.cs`

---

## Phase 4: Asset Reference System Overhaul

**Goal:** Replace `UnityEngine.Object.name` with meta GUID for safe references

### 4.1 Meta GUID Asset Reference System

**Current Problem:**

```csharp
// XNodeGraphExporter.cs line 221
object NormalizeUnityObject(UnityEngine.Object obj) {
    // TODO: Add addressable key extraction if needed
    // For now, use name as a simple identifier
    return obj != null ? obj.name : null;  // ❌ FRAGILE
}
```

**Issues:**
- Object names can change
- Duplicate names possible
- No validation if asset is deleted
- Can't differentiate between assets with same name

**Target Solution:**

- [ ] Create `AssetReference` system using Unity meta GUIDs
  ```csharp
  [Serializable]
  public sealed class AssetReference {
      public string guid;          // From .meta file
      public string path;          // For editor display
      public string name;          // For fallback
      public AssetReferenceType type;
      
      public bool IsValid => !string.IsNullOrEmpty(guid);
      public bool Exists => AssetDatabase.GUIDToAssetPath(guid) != null;
  }
  
  public enum AssetReferenceType {
      Direct,
      Addressable,
      Resource
  }
  ```

- [ ] Update `NormalizeUnityObject` to extract meta GUID
  ```csharp
  AssetReference NormalizeUnityObject(UnityEngine.Object obj) {
      if (obj == null) return null;
      
      var path = AssetDatabase.GetAssetPath(obj);
      if (string.IsNullOrEmpty(path)) {
          throw new InvalidGraphException($"Asset {obj.name} has no path - is it a scene object?");
      }
      
      var guid = AssetDatabase.AssetPathToGUID(path);
      if (string.IsNullOrEmpty(guid)) {
          throw new InvalidGraphException($"Could not get GUID for asset {obj.name} at {path}");
      }
      
      // Check if it's addressable
      var addressableKey = GetAddressableKey(obj);
      
      return new AssetReference {
          guid = guid,
          path = path,
          name = obj.name,
          type = string.IsNullOrEmpty(addressableKey) ? AssetReferenceType.Direct : AssetReferenceType.Addressable
      };
  }
  ```

- [ ] Add validation during export
  - Verify all referenced assets exist
  - Warn if GUID extraction fails
  - Collect all asset references for reporting

- [ ] Add runtime asset resolver
  ```csharp
  public interface IAssetReferenceResolver {
      T Resolve<T>(AssetReference reference) where T : UnityEngine.Object;
      UniTask<T> ResolveAsync<T>(AssetReference reference, CancellationToken ct) where T : UnityEngine.Object;
      void Unload(AssetReference reference);
  }
  ```

- [ ] Support addressables in resolver
  - Check if asset is addressable by GUID
  - Load via Addressables if available
  - Fall back to direct reference if not

- [ ] Add migration tool for old graphs
  - Scan all existing graphs
  - Convert `string` references to `AssetReference`
  - Report any missing assets

**Files to Create:**
- `Runtime/VisualGraphs/AssetReferences/AssetReference.cs`
- `Runtime/VisualGraphs/AssetReferences/IAssetReferenceResolver.cs`
- `Runtime/VisualGraphs/AssetReferences/AssetReferenceResolver.cs`
- `Editor/VisualGraphs/Tools/GraphAssetReferenceMigrationTool.cs`

**Files to Modify:**
- `Export/XNodeGraphExporter.cs` - Use AssetReference system
- `Runtime/DTOs/RuntimeNodeDefinition.cs` - Store AssetReference instead of string
- `Installer/VisualGraphInstaller.cs` - Register resolver

---

### 4.2 Asset Reference Validation

- [ ] Add pre-export validation
  - Check all UnityEngine.Object fields in nodes
  - Verify assets exist and have valid GUIDs
  - Report missing or invalid references before export
  - Prevent export if critical references are broken

- [ ] Add runtime validation
  - On graph initialization, validate all asset references
  - Log warnings for missing assets
  - Emit `Graph.AssetMissing` events
  - Allow execution to continue with graceful degradation

- [ ] Create asset reference inspector
  - Custom editor window to view all asset refs in a graph
  - Show GUID, path, status (valid/missing/moved)
  - Allow bulk re-linking of moved assets

**Files to Create:**
- `Editor/VisualGraphs/Validation/AssetReferenceValidator.cs`
- `Editor/VisualGraphs/Windows/AssetReferenceInspectorWindow.cs`

---

## Phase 5: Testing & Quality Assurance

**Goal:** Achieve 100% test coverage per MToolKit standards

### 5.1 Unit Tests

**Target: 100% coverage for core systems**

- [ ] Export layer tests
  - `XNodeGraphExporter` validation tests
  - Parameter extraction tests
  - Asset reference normalization tests
  - Error handling tests (missing nodes, invalid GUIDs)

- [ ] Runtime execution tests
  - `GraphRunner` idempotency tests
  - `GraphEventRouter` routing tests (exact match, wildcard)
  - `NodeExecutorRegistry` tests
  - `InMemoryGraphState` tests
  - Executor continuation tests

- [ ] State management tests
  - Save/restore cycle tests
  - Variable application order tests
  - GraphStateSnapshot serialization tests

- [ ] Quest system tests
  - Task progress tracking tests
  - Condition evaluation tests
  - Stage advancement tests

- [ ] Asset reference tests
  - GUID extraction tests
  - Resolution tests (direct, addressable)
  - Missing asset handling tests

**Test Framework:** Unity Test Framework (NUnit)  
**Mocking:** NSubstitute  
**Location:** `Tests/Runtime/VisualGraphs/`

**Files to Create:**
- `Tests/Runtime/VisualGraphs/Export/XNodeGraphExporterTests.cs`
- `Tests/Runtime/VisualGraphs/Runtime/GraphRunnerTests.cs`
- `Tests/Runtime/VisualGraphs/Runtime/GraphEventRouterTests.cs`
- `Tests/Runtime/VisualGraphs/Runtime/NodeExecutorRegistryTests.cs`
- `Tests/Runtime/VisualGraphs/State/InMemoryGraphStateTests.cs`
- `Tests/Runtime/VisualGraphs/State/GraphStateSnapshotTests.cs`
- `Tests/Runtime/VisualGraphs/Quest/QuestProgressTrackingTests.cs`
- `Tests/Runtime/VisualGraphs/AssetReferences/AssetReferenceTests.cs`

---

### 5.2 Integration Tests

- [ ] Full graph execution tests
  - Create test graphs programmatically
  - Execute and verify state changes
  - Test event routing end-to-end

- [ ] Save system integration tests
  - Start quest, modify state, save, reload, verify
  - Test with multiple graphs active

- [ ] MessagePipe integration tests
  - Send events via MessagePipe
  - Verify graphs receive and handle
  - Verify graphs emit events correctly

- [ ] Plugin lifecycle tests
  - Test Setup → Init → Tick → Shutdown
  - Verify proper dependency resolution

**Location:** `Tests/Integration/VisualGraphs/`

**Files to Create:**
- `Tests/Integration/VisualGraphs/GraphExecutionIntegrationTests.cs`
- `Tests/Integration/VisualGraphs/SaveSystemIntegrationTests.cs`
- `Tests/Integration/VisualGraphs/MessagePipeIntegrationTests.cs`
- `Tests/Integration/VisualGraphs/PluginLifecycleTests.cs`

---

### 5.3 Property-Based Tests

- [ ] Graph structure invariants
  - All nodes have valid GUIDs
  - All connections reference existing nodes
  - All entry nodes are reachable
  - No infinite loops possible

- [ ] State management invariants
  - Save → Load is idempotent
  - Variable application order is consistent
  - State doesn't leak between graphs

- [ ] Event routing invariants
  - Same event twice = same result (idempotency)
  - Event order doesn't matter (for parallel graphs)

**Framework:** FsCheck or custom property test framework

---

## Phase 6: Editor Tools & UX

**Goal:** Improve authoring experience and debugging

### 6.1 Graph Validation Tools

- [ ] Create `GraphValidator` static utility
  - Validate graph structure
  - Check for orphaned nodes
  - Detect potential infinite loops
  - Verify all executors exist

- [ ] Add validation to graph inspector
  - Show validation status in QuestGraphAsset inspector
  - Display errors/warnings inline
  - One-click fix for common issues

- [ ] Create graph analysis window
  - Show graph statistics (node count, depth, complexity)
  - List all entry points
  - Show all subscribed events
  - Visualize node dependencies

**Files to Create:**
- `Editor/VisualGraphs/Validation/GraphValidator.cs`
- `Editor/VisualGraphs/Windows/GraphAnalysisWindow.cs`
- `Editor/VisualGraphs/Inspectors/QuestGraphAssetEditor.cs`

---

### 6.2 Runtime Debugging Tools

- [ ] Create graph execution debugger
  - Show active graphs in hierarchy
  - Display current execution state
  - Show which nodes executed recently
  - Visualize event flow

- [ ] Add execution history
  - Record last N node executions
  - Show execution time per node
  - Display state changes over time

- [ ] Create state inspector window
  - Show all active graph states
  - Allow editing state values at runtime
  - Trigger events manually for testing

**Files to Create:**
- `Editor/VisualGraphs/Debugger/GraphExecutionDebugger.cs`
- `Editor/VisualGraphs/Windows/GraphStateInspectorWindow.cs`

---

### 6.3 Graph Creation Wizards

- [ ] Quest creation wizard
  - Template-based quest creation
  - Auto-generate common structures (linear, branching, collection)
  - Pre-populate with example nodes

- [ ] Dialogue creation wizard
  - Import from spreadsheet/JSON
  - Auto-generate dialogue tree
  - Support for Yarn/Ink format import

**Files to Create:**
- `Editor/VisualGraphs/Wizards/QuestCreationWizard.cs`
- `Editor/VisualGraphs/Wizards/DialogueCreationWizard.cs`

---

## Phase 7: Documentation & Polish

### 7.1 API Documentation

- [ ] Add XML comments to all public APIs
  - All interfaces (100%)
  - All public classes (100%)
  - All public methods (100%)

- [ ] Generate API documentation
  - Use DocFX or similar
  - Host on GitHub Pages

---

### 7.2 Tutorial Content

- [ ] Create example graphs
  - Simple linear quest
  - Branching quest with choices
  - Multi-task quest with progress
  - Dialogue with choices
  - Conditional dialogue

- [ ] Write tutorials
  - "Your First Quest" tutorial
  - "Creating Branching Dialogues" tutorial
  - "Advanced Quest Conditions" tutorial
  - "Custom Node Types" tutorial

- [ ] Create video tutorials (optional)

---

### 7.3 Performance Optimization

- [ ] Profile graph execution
  - Measure average execution time
  - Identify bottlenecks
  - Optimize hot paths

- [ ] Add execution metrics
  - Track graphs per second
  - Track average node execution time
  - Emit performance warnings

- [ ] Consider async/parallel execution
  - Execute independent graphs in parallel
  - Use Job System for heavy computations

---

## Phase 8: Graph Versioning & Stability

### 8.1 Graph Versioning

- [ ] Add version field to `RuntimeGraphDefinition`
- [ ] Support multiple graph versions in save data
- [ ] Create migration system for old saves
- [ ] Warn on version mismatch
- [ ] Semantic versioning for graph schemas

---

### 8.2 Multiplayer Support (Pre-requisite for SS14-level complexity)

- [ ] Network-synced graph state
- [ ] Per-player graph instances
- [ ] Shared world graph state
- [ ] Authority/ownership model
- [ ] State replication and interpolation
- [ ] Conflict resolution for concurrent modifications

---

## Phase 9: General-Purpose Visual Scripting Foundation

**Goal:** Expand beyond quests/dialogue into general game logic scripting

### 9.1 Core Programming Constructs

**Current:** Linear/branching execution only  
**Target:** Full programming capabilities

- [ ] Create variable system nodes
  - `GetVariableNode<T>` - Read any variable type
  - `SetVariableNode<T>` - Write any variable type
  - `IncrementNode`, `DecrementNode`, `ToggleNode`
  - Support for: int, float, bool, string, Vector3, Quaternion, Color, etc.

- [ ] Create flow control nodes
  - `BranchNode` - If/then/else logic
  - `SwitchNode` - Switch statement with multiple cases
  - `ForLoopNode` - Iterate N times
  - `WhileLoopNode` - Loop while condition true
  - `ForEachNode` - Iterate over collections
  - `BreakNode` - Exit loop early
  - `ReturnNode` - Exit graph early

- [ ] Create comparison nodes
  - `EqualsNode<T>`, `NotEqualsNode<T>`
  - `GreaterThanNode<T>`, `LessThanNode<T>`
  - `IsNullNode`, `IsValidNode`
  - `ContainsNode` - Check collection membership

- [ ] Create math nodes
  - Arithmetic: `AddNode`, `SubtractNode`, `MultiplyNode`, `DivideNode`, `ModuloNode`
  - Vector math: `Vector3AddNode`, `Vector3DotNode`, `Vector3CrossNode`, `NormalizeNode`
  - Trigonometry: `SinNode`, `CosNode`, `TanNode`, `Atan2Node`
  - Utility: `ClampNode`, `LerpNode`, `RandomRangeNode`, `RoundNode`, `FloorNode`, `CeilNode`

- [ ] Create logic nodes
  - `AndNode`, `OrNode`, `NotNode`, `XorNode`
  - `NandNode`, `NorNode`
  - Chaining support for multiple conditions

- [ ] Create collection nodes
  - `ArrayCreateNode`, `ArrayAddNode`, `ArrayRemoveNode`, `ArrayGetNode`, `ArraySetNode`
  - `ArrayLengthNode`, `ArrayClearNode`, `ArrayContainsNode`
  - `ListNode`, `DictionaryNode`, `HashSetNode`

**Files to Create:** ~50+ node types in `Runtime/VisualGraphs/Authoring/Nodes/Core/`

---

### 9.2 Type System & Generics

- [ ] Implement runtime type system
  ```csharp
  public interface ITypedPort {
      Type PortType { get; }
      bool CanConnectTo(ITypedPort other);
  }
  
  public sealed class TypedNodePort {
      public string portName;
      public Type dataType;
      public PortDirection direction;
      public bool allowMultipleConnections;
  }
  ```

- [ ] Add type validation during export
  - Verify port type compatibility
  - Detect type mismatches
  - Auto-conversion where safe (int → float)
  - Error on incompatible connections

- [ ] Support generic nodes
  - `CompareNode<T>` works with any comparable type
  - `GetComponentNode<T>` for any component
  - Type inference from connections

- [ ] Create type conversion nodes
  - `IntToFloatNode`, `FloatToIntNode`
  - `StringToIntNode`, `IntToStringNode`
  - `Vector3ToStringNode`, etc.

**Files to Create:**
- `Runtime/VisualGraphs/Types/ITypedPort.cs`
- `Runtime/VisualGraphs/Types/TypedNodePort.cs`
- `Runtime/VisualGraphs/Types/TypeValidator.cs`
- `Runtime/VisualGraphs/Types/TypeConversionRegistry.cs`

---

### 9.3 Data Flow & Pure Functions

**Current:** Execution flow only (white wires)  
**Target:** Data flow (colored wires by type)

- [ ] Implement data flow execution model
  - Execution pins (white) control flow
  - Data pins (colored) pass values
  - Pure nodes execute on-demand when output requested
  - Cache pure node results within single execution

- [ ] Create pure function nodes
  - Mark nodes as `[Pure]` - no side effects
  - Auto-execute when output is needed
  - Never execute twice in same frame
  - Visualize differently in editor (rounded corners)

- [ ] Support for default values
  - Unconnected input pins use default value
  - Show default value in node inspector
  - Override defaults per node instance

**Files to Create:**
- `Runtime/VisualGraphs/Execution/DataFlowExecutor.cs`
- `Runtime/VisualGraphs/Attributes/PureNodeAttribute.cs`

---

## Phase 10: Unity Integration Nodes

**Goal:** Deep integration with Unity engine systems

### 10.1 GameObject & Component Nodes

- [ ] GameObject manipulation
  - `CreateGameObjectNode`, `DestroyGameObjectNode`
  - `FindGameObjectNode`, `FindGameObjectWithTagNode`
  - `SetActiveNode`, `GetActiveNode`
  - `GetNameNode`, `SetNameNode`, `GetTagNode`, `SetTagNode`

- [ ] Transform manipulation
  - `GetPositionNode`, `SetPositionNode`
  - `GetRotationNode`, `SetRotationNode`
  - `GetScaleNode`, `SetScaleNode`
  - `TranslateNode`, `RotateNode`, `LookAtNode`
  - `GetChildNode`, `GetParentNode`, `SetParentNode`

- [ ] Component access
  - `GetComponentNode<T>` - Generic component getter
  - `AddComponentNode<T>`, `RemoveComponentNode<T>`
  - `HasComponentNode<T>`
  - Support for common components: Rigidbody, Collider, Renderer, etc.

- [ ] Prefab instantiation
  - `InstantiatePrefabNode` - Spawn prefab at position/rotation
  - `InstantiateFromPoolNode` - Object pooling support
  - `ReturnToPoolNode`

**Files to Create:** ~30+ nodes in `Runtime/VisualGraphs/Authoring/Nodes/Unity/`

---

### 10.2 Physics & Collision Nodes

- [ ] Physics queries
  - `RaycastNode` - Single raycast
  - `RaycastAllNode` - Get all hits
  - `SphereCastNode`, `BoxCastNode`, `CapsuleCastNode`
  - `OverlapSphereNode`, `OverlapBoxNode`
  - `ClosestPointNode`

- [ ] Rigidbody control
  - `AddForceNode`, `AddTorqueNode`
  - `SetVelocityNode`, `GetVelocityNode`
  - `SetAngularVelocityNode`
  - `SetKinematicNode`, `SetGravityNode`

- [ ] Collision events
  - `OnCollisionEnterNode`, `OnCollisionExitNode`
  - `OnTriggerEnterNode`, `OnTriggerExitNode`
  - Event-driven execution from physics callbacks

---

### 10.3 Animation & Audio Nodes

- [ ] Animator control
  - `SetAnimatorParameterNode`
  - `PlayAnimationNode`, `CrossfadeAnimationNode`
  - `GetAnimatorStateNode`

- [ ] Audio playback
  - `PlaySoundNode` - One-shot audio
  - `PlaySound3DNode` - Spatial audio
  - `PlayMusicNode`, `StopMusicNode`
  - `SetVolumeNode`, `SetPitchNode`

---

### 10.4 UI Nodes

- [ ] UI manipulation
  - `SetTextNode`, `GetTextNode`
  - `SetImageSpriteNode`, `SetColorNode`
  - `SetActiveUINode`, `ShowPanelNode`, `HidePanelNode`
  - `GetSliderValueNode`, `SetSliderValueNode`
  - `GetInputFieldNode`, `SetInputFieldNode`

- [ ] UI events
  - `OnButtonClickNode`
  - `OnSliderChangedNode`
  - `OnInputFieldChangedNode`
  - `OnToggleChangedNode`

---

## Phase 11: Advanced Execution Models

**Goal:** Support complex execution patterns for game systems

### 11.1 Subroutines & Functions

- [ ] Create function graph type
  - `FunctionGraphAsset` - Reusable logic
  - Input parameters and output returns
  - Can be called from any graph
  - Support for local variables

- [ ] Function call nodes
  - `CallFunctionNode` - Execute function graph
  - `CallFunctionAsyncNode` - Async function call
  - Parameter passing via ports
  - Return value handling

- [ ] Function library system
  - `FunctionLibrary` asset - Collection of functions
  - Category organization
  - Search and autocomplete
  - Documentation per function

**This enables Blueprint-style function libraries**

---

### 11.2 Coroutines & Async Execution

- [ ] Create delay nodes
  - `DelayNode` - Wait for seconds
  - `DelayFramesNode` - Wait N frames
  - `WaitForNode` - Wait until condition true
  - `WaitForSecondsNode`, `WaitForEndOfFrameNode`

- [ ] Parallel execution
  - `ParallelNode` - Execute multiple branches simultaneously
  - `RaceNode` - First branch to complete wins
  - `SequenceNode` - Execute branches in order
  - `JoinNode` - Wait for all branches to complete

- [ ] Timeline & animation sequences
  - `TimelineNode` - Execute nodes over time with curves
  - Support for easing functions
  - Keyframe-based execution

---

### 11.3 Event System

**Current:** External events only (MessagePipe)  
**Target:** Internal graph events & delegates

- [ ] Custom event nodes
  - `CreateEventNode` - Define custom event
  - `FireEventNode` - Trigger event
  - `BindEventNode` - Subscribe to event
  - `UnbindEventNode` - Unsubscribe

- [ ] Delegate system
  - Pass execution flow as parameter
  - Callback nodes
  - Event aggregation

- [ ] Global event bus nodes
  - `EmitGlobalEventNode`
  - `ListenForGlobalEventNode`
  - Cross-graph communication

**This enables complex system interactions like SS14**

---

## Phase 12: Complex Game Systems (SS14-Level)

**Goal:** Support interconnected game systems like Space Station 14 using plugin architecture

**Note:** This uses **traditional Unity GameObjects and MToolKit plugins**, NOT Unity ECS/DOTS.

### 12.1 Plugin-Based Game System Nodes

**Architecture:** Each game system is a plugin that communicates via MessagePipe

- [ ] Create plugin query nodes
  - `GetPluginNode<T>` - Get plugin instance by type
  - `HasPluginNode<T>` - Check if plugin exists
  - `CallPluginMethodNode` - Invoke plugin methods from graphs

- [ ] Create plugin lifecycle hooks
  - `OnPluginInitializedNode` - React when plugin loads
  - `OnPluginShutdownNode` - React when plugin unloads
  - Graphs can trigger on system availability

---

### 12.2 Inventory System Nodes

**Assumes InventoryPlugin exists in game**

- [ ] Inventory manipulation nodes
  - `AddItemToInventoryNode` - Add item by ID/reference
  - `RemoveItemFromInventoryNode` - Remove item
  - `GetInventoryContentsNode` - Query what player has
  - `HasItemNode` - Check if player has specific item
  - `GetItemCountNode` - Count of specific item
  - `CanAddItemNode` - Check if inventory has space
  - `DropItemNode`, `EquipItemNode`, `UnequipItemNode`

- [ ] Inventory event nodes
  - `OnItemAddedNode` - React when item added
  - `OnItemRemovedNode` - React when item removed
  - `OnInventoryFullNode` - React when can't add more

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Inventory/*.cs` (~8 nodes)

---

### 12.3 Role/Permission System Nodes

**For games with role-based gameplay (like SS14 jobs)**

- [ ] Role management nodes
  - `GetPlayerRoleNode` - Get current role/job
  - `SetPlayerRoleNode` - Assign role/job
  - `HasRoleNode` - Check if player has specific role
  - `GetRolePermissionsNode` - Get list of permissions

- [ ] Permission check nodes
  - `HasPermissionNode` - Check permission by ID
  - `CheckAccessNode` - Check access to area/object
  - `CanInteractNode` - Check if role can interact with object
  - `GetAllowedActionsNode` - Get list of allowed actions

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Roles/*.cs` (~6 nodes)

---

### 12.4 Interaction System Nodes

**For complex object interactions**

- [ ] Interaction event nodes
  - `OnInteractNode` - Player interacts with object
  - `OnInteractAlternateNode` - Right-click / alternate action
  - `OnInteractHoldNode` - Long press interaction

- [ ] Interaction control nodes
  - `CanInteractNode` - Check if interaction allowed (distance, permissions)
  - `GetInteractionOptionsNode` - Get context menu options
  - `ExecuteInteractionNode` - Trigger specific interaction
  - `SetInteractableNode` - Enable/disable interaction

- [ ] Context menu nodes
  - `AddContextOptionNode` - Add option to context menu
  - `RemoveContextOptionNode` - Remove option
  - `ShowContextMenuNode` - Force show menu

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Interaction/*.cs` (~8 nodes)

---

### 12.5 Crafting/Construction System Nodes

**For crafting and building mechanics**

- [ ] Crafting nodes
  - `CanCraftNode` - Check if recipe is craftable
  - `CraftItemNode` - Execute crafting recipe
  - `GetCraftingRequirementsNode` - Get required materials
  - `GetKnownRecipesNode` - Get recipes player knows
  - `LearnRecipeNode` - Unlock new recipe

- [ ] Construction nodes
  - `StartConstructionNode` - Begin building object
  - `ContinueConstructionNode` - Add materials to construction
  - `GetConstructionProgressNode` - Get % complete
  - `CompleteConstructionNode` - Finish construction
  - `CancelConstructionNode` - Abort and refund

- [ ] Resource nodes
  - `GetResourceCountNode` - Count of specific resource
  - `ConsumeResourcesNode` - Remove resources
  - `CheckResourcesAvailableNode` - Verify materials exist

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Crafting/*.cs` (~11 nodes)

---

### 12.6 Environmental System Nodes

**For environmental interactions (example: atmospherics like SS14)**

- [ ] Environment query nodes
  - `GetEnvironmentDataNode` - Get tile/area data
  - `GetTemperatureNode`, `SetTemperatureNode`
  - `GetPressureNode`, `SetPressureNode`
  - `GetAtmosphereCompositionNode` - Gas mixture

- [ ] Environmental effect nodes
  - `ApplyEnvironmentalEffectNode` - Fire, cold, poison, etc.
  - `TriggerEnvironmentalEventNode` - Explosion, fire spread, etc.
  - `GetEnvironmentalHazardsNode` - List hazards in area

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Environment/*.cs` (~7 nodes)

---

### 12.7 Multiplayer/Networking Nodes

**For multiplayer games with authority models**

- [ ] Network state nodes
  - `IsServerNode` - Check if running as server
  - `IsClientNode` - Check if running as client
  - `GetLocalPlayerNode` - Get local player reference
  - `GetPlayerCountNode` - Number of connected players

- [ ] Network synchronization nodes
  - `SyncVariableNode` - Mark variable for network sync
  - `IsOwnerNode` - Check if local player owns object
  - `HasAuthorityNode` - Check if can modify object
  - `TransferAuthorityNode` - Transfer ownership

- [ ] RPC nodes
  - `CallRPCNode` - Call method on server/client
  - `BroadcastEventNode` - Send event to all players
  - `SendToPlayerNode` - Send to specific player

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Network/*.cs` (~10 nodes)

**Note:** Requires integration with Unity Netcode/Mirror/etc.

---

### 12.8 AI/Behavior Nodes

**For NPC behavior and AI systems**

- [ ] AI state nodes
  - `SetAIStateNode` - Set behavior state (Idle, Patrol, Combat, Flee)
  - `GetAIStateNode` - Query current state
  - `TransitionAIStateNode` - Change state with transition

- [ ] Pathfinding nodes
  - `FindPathNode` - Calculate path to target
  - `MoveAlongPathNode` - Follow calculated path
  - `CanReachNode` - Check if path exists
  - `GetNearestWaypointNode`

- [ ] Perception nodes
  - `CanSeeNode` - Check line of sight
  - `GetVisibleEnemiesNode` - List targets in view
  - `GetNearestEnemyNode` - Find closest threat
  - `HearSoundNode` - React to audio events

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/AI/*.cs` (~11 nodes)

---

**Total Files to Create:** ~60+ nodes for complex game systems  
**Integration:** All systems communicate via MessagePipe, no ECS required

---

## Phase 13: Advanced Editor Features (Blueprint-level)

**Goal:** Match Unreal Blueprint editor UX

### 13.1 Visual Editor Enhancements

- [ ] Improve xNode or replace with custom editor
  - Minimap navigation
  - Node search/palette (Tab key)
  - Quick wire rerouting
  - Alignment and distribution tools
  - Snap to grid
  - Node groups/comments
  - Bookmarks

- [ ] Node aesthetics
  - Color-coded by category
  - Icons per node type
  - Compact vs. expanded view
  - Pin labels and tooltips

- [ ] Wire aesthetics
  - Curved bezier wires
  - Color-coded by data type
  - Animation on execution (debug mode)
  - Wire thickness based on data flow

---

### 13.2 Debugging & Profiling

- [ ] Breakpoint system
  - Set breakpoints on nodes
  - Pause execution at breakpoint
  - Inspect variables at pause
  - Step through execution

- [ ] Watch window
  - Monitor variable values in real-time
  - Pin important values
  - Historical data over time

- [ ] Execution flow visualization
  - Highlight active nodes during execution
  - Show data values on wires
  - Execution heatmap (frequently executed nodes)
  - Performance profiling per node

- [ ] Graph simulation mode
  - Run graph in editor without play mode
  - Mock external dependencies
  - Instant feedback

---

### 13.3 Node Creation Tools

- [ ] Visual node creation wizard
  - Point-and-click node generation
  - Template-based node creation
  - Auto-generate executors from attributes

- [ ] C# to node converter
  - Annotate C# class with attributes
  - Auto-generate node and executor
  - Keep in sync with code changes
  ```csharp
  [GenerateGraphNode("Math/Distance")]
  [Pure]
  public static float CalculateDistance(Vector3 a, Vector3 b) {
      return Vector3.Distance(a, b);
  }
  // Auto-generates: CalculateDistanceNode + executor
  ```

- [ ] Node library browser
  - Search all available nodes
  - Preview node connections
  - Drag and drop into graph
  - Favorites system

---

## Phase 14: Performance & Scalability

**Goal:** Handle thousands of active graphs like SS14 would need

### 14.1 Execution Optimization

- [ ] Just-In-Time compilation
  - Compile graphs to IL at runtime
  - Cache compiled versions
  - 10-100x speedup over interpreted execution

- [ ] Job System integration
  - Execute graphs on worker threads
  - Parallel graph execution
  - Avoid main thread blocking

- [ ] Burst compilation support
  - Compile hot paths with Burst
  - SIMD optimization for math nodes
  - Managed code elimination

- [ ] Graph execution pooling
  - Reuse execution contexts
  - Pool node executors
  - Reduce GC pressure

---

### 14.2 Memory Optimization

- [ ] Compact graph representation
  - Binary serialization
  - Reference counting
  - Struct-based execution context

- [ ] Lazy loading
  - Load graphs on-demand
  - Unload inactive graphs
  - LRU cache for definitions

- [ ] State compression
  - Delta compression for saves
  - Run-length encoding for arrays
  - Bit-packing for booleans

---

### 14.3 Scalability Testing

- [ ] Stress test suite
  - 1,000+ active graphs simultaneously
  - 10,000+ nodes executing per frame
  - 100,000+ state variables
  - Memory usage under load

- [ ] Benchmarking framework
  - Compare against native C# performance
  - Track performance regressions
  - Continuous performance monitoring

---

## Phase 15: Community & Ecosystem

**Goal:** Enable community-driven node development

### 15.1 Node Package System

- [ ] Package manifest format
  - Define node collections
  - Versioning and dependencies
  - License and author info

- [ ] Package manager
  - Import/export node packages
  - Version management
  - Dependency resolution
  - Unity Package Manager integration

- [ ] Package marketplace (future)
  - Community node library
  - Ratings and reviews
  - Documentation and examples

---

### 15.2 Code Generation & Export

- [ ] C# code generation from graphs
  - Export graph as C# class
  - Use for performance-critical code
  - Keep graph as source of truth

- [ ] Graph templates
  - Save graphs as templates
  - Parameterized templates
  - Instantiate with different values

- [ ] Cross-project sharing
  - Export graphs as standalone assets
  - Import into other projects
  - Version compatibility checks

---

## Long-Term Vision

### Ultimate Goal: "Blueprint-like from UE5"

The VisualGraphs system should eventually:

1. ✅ **Replace C# for most game logic** - Designers create gameplay without programming
2. ✅ **Support complex systems** - Handle SS14-level complexity (roles, inventory, crafting, atmospherics)
3. ✅ **Performance competitive with code** - JIT compilation makes graphs as fast as C#
4. ✅ **Professional tooling** - Editor UX matches Unreal Blueprints
5. ✅ **Extensible** - Easy to add custom nodes for project-specific logic
6. ✅ **Debuggable** - Breakpoints, watch windows, visual execution flow
7. ✅ **Type-safe** - Compile-time type checking prevents runtime errors
8. ✅ **Multiplayer-ready** - State replication and authority built-in
9. ✅ **Community-driven** - Node marketplace and package ecosystem

### Reference Games Built with Similar Systems

- **Space Station 14** - Complex multiplayer systems with ECS
- **Unreal Engine Games** - Many AAA games use Blueprints extensively
- **Unity Visual Scripting** - Asset Store games using Bolt/VS
- **GameMaker Studio** - Entire games built with visual scripting

### Why This Matters for MToolKit

MToolKit aims to be a "production game accelerator." A full visual scripting system means:

- ✅ **Faster iteration** - Designers test ideas without waiting for programmers
- ✅ **Lower barrier to entry** - Non-programmers contribute gameplay
- ✅ **Better collaboration** - Visual graphs are easier to review than code
- ✅ **Rapid prototyping** - Test game mechanics in minutes
- ✅ **Living documentation** - Graphs self-document behavior

This aligns perfectly with MToolKit's mission of shipping production-quality games faster.

---

## Estimated Timeline to "Blueprint-like"

**Note:** Original v0.5 system (43 files, production-quality architecture) was built in ~1 afternoon. Timeline adjusted for **full-time development** at proven velocity.

**Phase 1-8 (Quest/Dialogue Production Ready):** 3-5 days (MToolKit integration, save system, MessagePipe, tests)  
**Phase 9-11 (General Visual Scripting):** 1 week (Core nodes: variables, flow control, math, Unity integration, ~50 types)  
**Phase 12 (Complex Game Systems):** 1 week (Plugin nodes: inventory, crafting, AI, networking, ~60 types)  
**Phase 13 (Advanced Editor):** 1-2 weeks (Blueprint-quality UX, debugging, profiling, breakpoints)  
**Phase 14 (Performance):** 3-5 days (JIT compilation, benchmarking, optimization passes)  
**Phase 15 (Ecosystem):** 2-3 days (Package system, documentation, examples)  

**Total Timeline (Full-Time Focus):** ~4-6 weeks to v4.0 "Blueprint-like"

**Conservative Estimate (With Interruptions):** 8-10 weeks  
**Aggressive Estimate (In The Zone™):** 3-4 weeks

---

## Why This Timeline Makes Sense

At current velocity (v0.5 in 1 afternoon):

- **Node creation is fast** - Once pattern established, nodes are copy-paste with parameter changes
- **Architecture is solid** - Core runtime won't need major refactoring, just extension
- **Integration work is straightforward** - MToolKit patterns already established
- **Editor tools are incremental** - Can ship v1.0 first, polish editor in v2.0+

**Bottlenecks:**
- Testing (but you can iterate fast with manual testing first)
- JIT compilation (complex but bounded scope)
- Editor UX polish (can ship with basic editor, enhance later)

**Ideal Schedule:**
- **Week 1:** Phase 1-8 complete, v1.0 shipped (production-ready quest/dialogue)
- **Week 2:** Phase 9-10 done (general scripting + Unity nodes)
- **Week 3:** Phase 11-12 done (functions + complex systems)
- **Week 4-5:** Phase 13 done (editor tools)
- **Week 6:** Phase 14-15 done (performance + polish)
- **Week 7:** Buffer for testing, documentation, showcase videos

**Portfolio Impact:**

A Blueprint-like visual scripting system is a **serious** showcase project. This could:
- Demonstrate architectural skills (event-driven, DI, type systems)
- Show breadth (editor tools, JIT compilation, networking)
- Prove velocity (shipped in 6-8 weeks)
- Stand out vs. typical portfolio projects

---

## Phased Rollout Strategy

### Version 0.5 (Current)
- Quest and Dialogue graphs
- Basic event routing
- xNode authoring

### Version 1.0 (Phase 1-2 Complete)
- Production-ready quest system with task tracking
- Full MToolKit integration
- Save system working
- Test coverage 80%+

### Version 2.0 (Phase 9-10 Complete)
- General-purpose visual scripting
- Core programming constructs (variables, flow control, math)
- Unity integration nodes (GameObject, Transform, Physics)
- Type system with validation

### Version 3.0 (Phase 11-12 Complete)
- Functions and subroutines
- Complex execution models (coroutines, parallel)
- Plugin-based game systems (inventory, crafting, roles, AI)
- "Can build SS14" milestone (using traditional GameObjects + plugins)

### Version 4.0 (Phase 13-15 Complete)
- Blueprint-quality editor
- JIT compilation and performance
- Node marketplace and ecosystem
- "Blueprint-like from UE5" milestone achieved

---

## Summary of Critical Path

**Must-Have for Production (Phase 1-2):**
1. ✅ Plugin architecture integration
2. ✅ Save system integration
3. ✅ MessagePipe/R3 event bus
4. ✅ Quest task progress tracking
5. ✅ Meta GUID asset references
6. ✅ Dialogue UI service implementation
7. ✅ Core test coverage (80%+)

**Nice-to-Have (Phase 3-4):**
- Quest conditions & rewards
- Advanced dialogue features
- Editor tools
- Asset reference validation UI

**Future Enhancements (Phase 5+):**
- Graph versioning
- Visual Scripting integration
- Multiplayer support

---

## Estimated Timeline

**Phase 1 (Critical Integration):** 3-5 days  
**Phase 2 (Quest Enhancements):** 3-4 days  
**Phase 3 (Dialogue Completion):** 2-3 days  
**Phase 4 (Asset References):** 2-3 days  
**Phase 5 (Testing):** 4-6 days  
**Phase 6 (Editor Tools):** 2-3 days  
**Phase 7 (Documentation):** 2-3 days  

**Total to Production Ready:** ~18-27 days (3-4 weeks)

---

## Success Criteria

### ✅ Phase 1 Complete When:
- [ ] Graphs save/load properly with game saves
- [ ] Graphs receive events from MessagePipe
- [ ] Graphs emit events to MessagePipe
- [ ] Plugin appears in PluginRegistry
- [ ] Config asset controls system behavior

### ✅ Phase 2 Complete When:
- [ ] Quests track task progress (X/Y complete)
- [ ] Can display quest progress in UI
- [ ] Task completion triggers events
- [ ] Quest rewards are granted

### ✅ System is Production-Ready When:
- [ ] 100% test coverage for core systems
- [ ] All integration TODOs removed
- [ ] Meta GUID system implemented and validated
- [ ] Documentation complete
- [ ] No known critical bugs
- [ ] Performance targets met (1000+ nodes/sec)

---

## Risk Areas

1. **Asset Reference Migration** - Existing graphs will break
   - Mitigation: Create migration tool first, test extensively
   
2. **Save System Compatibility** - Changes to state format may break saves
   - Mitigation: Version save data, provide migration path
   
3. **Performance** - Large graphs may execute slowly
   - Mitigation: Profile early, optimize hot paths, consider parallel execution

4. **xNode Dependency** - Reliance on external package
   - Mitigation: xNode is authoring-only, runtime has no dependency

---

## File Count Estimate

**New Files to Create:** ~60-80 files  
**Existing Files to Modify:** ~15-20 files  
**Test Files:** ~30-40 files  

**Current:** 43 files  
**After Roadmap:** ~130-160 files (3-4x growth)

