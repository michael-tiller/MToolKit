# VisualGraphs Subsystem - Changelog

This document tracks completed phases and major milestones. See `PRODUCTION_ROADMAP.md` for current work and future plans.

---

## ⚠️ BREAKING: GraphEventRouter delivery is now ADDITIVE, not exact-ELSE-wildcard

**`GraphEventRouter.RouteAsync`** previously suppressed an empty-domain ("any") subscriber whenever an
exact-domain subscriber existed for the routed domain. This suppression is now removed: a routed message
delivers to BOTH the exact-domain bucket AND the empty-domain wildcard bucket, deduplicated by runner
reference identity, dispatched in overall registration order.

**Why:** enables per-entity/per-event-name subscription filtering (`EventNameFilter` via
`IDomainMessage.Domain`) without silently starving graphs that subscribe with no filter — a filtered
graph and an unfiltered graph on the same message type must BOTH fire on a matching event.

**Impact:** any code relying on an unfiltered subscriber being silenced by a more specific one will now
see both fire. Shipped content was audited (no non-empty `DomainFilter` in any `.asset` at time of
change) — this is a forward-looking behavior fix, not a regression against shipped graphs.

---

## Phase 1.0: Core Architecture Fixes ✅ **COMPLETE**

**Foundation is solid - ready for integration!**

### 1.0.1 Asset Reference System ✅ **COMPLETE**

**Implementation:** Modern `AssetReference` approach (superior to meta GUID extraction)

**What Was Built:**

1. **`SerializableAssetReference`** - Runtime DTO storing GUID, AssetType, RuntimeKey
2. **`IGraphAssetLoader` + `GraphAssetLoader`** - Addressables-based asset loading service
3. **Export validation** - Validates AssetReferences at export time via `XNodeGraphExporter`
4. **Runtime warnings** - Detects missing/invalid assets during parameter extraction

**Key Files Created:**
- `Runtime/DTOs/SerializableAssetReference.cs`
- `Runtime/AssetLoading/IGraphAssetLoader.cs`
- `Runtime/AssetLoading/GraphAssetLoader.cs`

**Validation Features:**
- ✅ Detects unassigned AssetReferences
- ✅ Validates GUID exists via `RuntimeKeyIsValid()`
- ✅ Checks assets are marked as Addressable
- ✅ Throws `InvalidGraphException` on critical errors
- ✅ Provides detailed error messages with node/field/GUID info

**Usage in Nodes:**
```csharp
public class MyNode : VisualGraphNodeBase {
    public AssetReferenceGameObject Prefab;  // Auto-validated & serialized
    public AssetReferenceAudioClip Sound;
}
```

**Why This Is Better Than Meta GUID Extraction:**
- Type-safe (use `AssetReferenceGameObject`, `AssetReferenceAudioClip`, etc.)
- No need to parse `.meta` files - GUIDs already in `AssetReference`
- Native Addressables integration
- Production-ready Unity pattern

---

### 1.0.2 Explicit Graph Subscriptions ✅ **COMPLETE** (Exceeded Spec!)

**What Was Built:**

```csharp
// Type-based subscriptions (better than original string-based spec!)
public class QuestGraphAsset : NodeGraph {
    [BoxGroup("Event Subscriptions")]
    [ValidateInput(nameof(ValidateSubscriptions))]
    public List<MessageSubscription> Subscriptions = new();
    
    [Button("Auto-Populate from Entry Nodes")]
    private void AutoPopulateSubscriptions() { }
    
    [Button("Validate Graph")]
    private void ValidateGraph() { }
}

[Serializable]
public class MessageSubscription {
    public MessageTypeReference MessageType; // ✅ Type (not string!)
    public bool Required;
    public string DomainFilter; // Optional
}
```

**Features Delivered:**
- ✅ **Explicit graph-level subscriptions** - No inference from nodes
- ✅ **Type-safe** - Uses `MessageTypeReference` with Odin dropdown (better than string-based spec)
- ✅ **Real-time validation** - `[ValidateInput]` shows errors in inspector immediately
- ✅ **One-click migration** - "Auto-Populate from Entry Nodes" button
- ✅ **Manual validation** - "Validate Graph" button with dialog
- ✅ **Entry nodes as entry points** - Deleted `IEventSubscribedNode`, nodes don't declare subscriptions
- ✅ **Export from graph.Subscriptions** - Not inferred from nodes

**Validation Checks:**
- ✅ Required subscriptions must have matching entry nodes (compile-time via Odin)
- ✅ Entry nodes without subscriptions show warnings (will never execute)
- ✅ Invalid message types show errors
- ✅ Domain filter matching validated

**What We Built vs. Original Spec:**

Original spec wanted string-based:
```csharp
public string eventType;  // ❌ Fragile, no validation
```

We built type-based:
```csharp
public MessageTypeReference MessageType; // ✅ Type-safe, compiler validated
```

**Additional Benefits:**
- ✅ IntelliSense support (see all message types)
- ✅ Find References works (see all graphs using a message)
- ✅ Refactor support (rename updates everywhere)
- ✅ Direct MessagePipe integration (no wrapper)

**Files Modified:**
- ✅ `Authoring/Graphs/QuestGraphAsset.cs` - Added type-based subscriptions with Odin validation
- ✅ `Export/XNodeGraphExporter.cs` - Exports from graph.Subscriptions, not nodes
- ✅ `Runtime/DTOs/RuntimeSubscriptionDefinition.cs` - Uses MessageTypeReference
- ✅ Deleted `Authoring/IEventSubscribedNode.cs` - No longer needed

---

### 1.0.3 Per-Graph Execution Limits ✅ **COMPLETE**

**What Was Built:**

```csharp
// Authoring (per-graph configuration)
public class QuestGraphAsset : NodeGraph {
    [BoxGroup("Performance")]
    [Range(64, 4096)]
    [InfoBox("Default: 1024. Increase for complex graphs.")]
    public int MaxExecutionSteps = 1024;
}

// Runtime
public class RuntimeGraphDefinition {
    public int MaxExecutionSteps = 1024;
}

// GraphRunner uses it
var maxSteps = Definition.MaxExecutionSteps;
if (++steps > maxSteps) { break; }
```

**Features Delivered:**
- ✅ Per-graph execution limits (Odin `[Range(64, 4096)]` slider)
- ✅ Visible in authoring (Performance box group with InfoBox)
- ✅ Exported to runtime definition
- ✅ GraphRunner reads from definition
- ✅ Applies to both QuestGraphAsset and DialogueGraphAsset
- ✅ InfoBox explains purpose and default

**Benefits:**
- ✅ Simple graphs can use fewer steps (e.g., 128)
- ✅ Complex graphs can use more (e.g., 2048)
- ✅ Per-graph tuning for performance
- ✅ Prevents infinite loops with configurable safety

**Files Modified:**
- ✅ `Authoring/Graphs/QuestGraphAsset.cs` - Added MaxExecutionSteps field
- ✅ `Authoring/Graphs/DialogueGraphAsset.cs` - Added MaxExecutionSteps field
- ✅ `Runtime/DTOs/RuntimeGraphDefinition.cs` - Added MaxExecutionSteps property
- ✅ `Export/XNodeGraphExporter.cs` - Copies MaxExecutionSteps to runtime
- ✅ `Runtime/GraphRunner.cs` - Uses definition.MaxExecutionSteps

**Note:** Skipped global `VisualGraphConfig` for now - not needed since each graph has sensible defaults. Can add later if needed.

---

### 1.0.4 Addressables Loading Implementation ✅ **COMPLETE**

**What Was Built:**

```csharp
// IGraphLoader service
public interface IGraphLoader {
    UniTask<IGraphRunner> LoadGraphAsync(string graphId, CancellationToken ct);
    void UnloadGraph(string graphId);
    bool IsLoaded(string graphId);
}

// Bootstrap with lazy loading support
public class VisualGraphBootstrap : MonoBehaviour {
    public bool LoadAllOnStartup = true; // Toggle eager/lazy loading
    
    public async UniTask<IGraphRunner> LoadGraphAsync(string graphId) {
        return await graphLoader.LoadGraphAsync(graphId, cts.Token);
    }
    
    public void UnloadGraph(string graphId) {
        graphLoader.UnloadGraph(graphId);
    }
}

// GraphLoader checks AddressableKey
if (!string.IsNullOrEmpty(questDef.AddressableKey)) {
    // Load via Addressables
    var handle = Addressables.LoadAssetAsync<QuestGraphAsset>(questDef.AddressableKey);
    graphAsset = await handle.ToUniTask(ct);
    loadedHandles[questDef.QuestId] = handle; // Track for cleanup
} else {
    // Use direct reference
    graphAsset = questDef.GraphAsset;
}
```

**Features Delivered:**
- ✅ **IGraphLoader service** - Async graph loading with cancellation support
- ✅ **Addressables support** - Uses `QuestDefinition.AddressableKey` if present
- ✅ **Fallback to direct refs** - Works with or without Addressables
- ✅ **Lazy loading mode** - `LoadAllOnStartup` toggle in inspector
- ✅ **Proper cleanup** - Unloads Addressables handles on graph unload
- ✅ **Both graph types** - Works for Quest and Dialogue graphs
- ✅ **Registered in DI** - `IGraphLoader` available via VContainer

**Benefits:**
- ✅ Dynamic loading - Load graphs only when needed
- ✅ Memory efficient - Unload unused graphs
- ✅ Hot updates - Graphs can be updated via Addressables
- ✅ Flexible - Mix direct refs and Addressables

**Files Created:**
- ✅ `Runtime/Loading/IGraphLoader.cs` - Service interface
- ✅ `Runtime/Loading/GraphLoader.cs` - Full implementation (220 lines)

**Files Modified:**
- ✅ `Bootstrap/VisualGraphBootstrap.cs` - Uses `IGraphLoader`, added LoadAllOnStartup toggle
- ✅ `Installer/VisualGraphInstaller.cs` - Registers `IGraphLoader`
- ✅ `Definitions/DialogueDefinition.cs` - Already had AddressableKey

**Note:** Asset references in nodes (1.0.1) already handles loading assets referenced BY graphs. This handles loading the graph assets themselves!

---

## Phase 1.1: Plugin Architecture Integration ✅ **COMPLETE**

**Current:** Full plugin lifecycle with proper initialization  
**Target:** ✅ COMPLETED - Production-ready plugin with config system

- [x] Create `VisualGraphPlugin : DomainPlugin<GraphEventRouter, IGraphEventRouter>`
  - ✅ Implement plugin lifecycle (Setup → RuntimeInit → Tick → Shutdown)
  - ✅ Move bootstrap logic from `VisualGraphBootstrapMB` to plugin
  - ✅ Register with `PluginRegistry`
  - ✅ Add dependency validation via `IDependencyDeclaration`
  
- [x] Create `IGraphEventRouter` interface
  - ✅ Extract interface from `GraphEventRouter` concrete class
  - ✅ Expose `RegisterRunner`, `RouteAsync`, `GetRunners`, `Clear`, `GetSubscribedMessageTypes`
  - ✅ Update installer to register as interface

- [x] Add `VisualGraphConfig` ScriptableObject
  - ✅ `bool EnableVerboseLogging`
  - ✅ `int MaxExecutionStepsPerGraph = 1024`
  - ✅ `bool ValidateGraphsOnStartup = true`
  - ✅ `bool AutoInitializeFromRegistry = true`
  - ✅ `VisualGraphRegistry DefaultRegistry`
  - ✅ `bool LoadAllOnStartup = true`
  - ✅ `CreateAssetMenu` at `MToolKit/Visual Graphs/Config`

**Files Created:**
- ✅ `Runtime/VisualGraphs/VisualGraphPlugin.cs`
- ✅ `Runtime/VisualGraphs/Runtime/Interfaces/IGraphEventRouter.cs`
- ✅ `Runtime/VisualGraphs/Config/VisualGraphConfig.cs`

**Files Modified:**
- ✅ `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Override Register() to handle all DI registration
- ✅ `Runtime/VisualGraphs/Runtime/GraphEventRouter.cs` - Implements IGraphEventRouter
- ✅ `Runtime/VisualGraphs/Definitions/VisualGraphRegistry.cs` - Updated documentation
- ✅ `Runtime/VisualGraphs/README.md` - Updated to reflect plugin architecture

**Files Removed:**
- ✅ `Runtime/VisualGraphs/Bootstrap/VisualGraphBootstrap.cs` - Replaced by VisualGraphPlugin
- ✅ `Runtime/VisualGraphs/Installer/VisualGraphInstaller.cs` - Functionality moved into plugin

**Architecture:**
- ✅ Plugin handles its own DI registration via `Register()` override
- ✅ All services registered internally: GraphEventRouter, NodeExecutorRegistry, GraphLoader, EventEmitter, Executors
- ✅ Config injected via `Construct()` and registered as instance
- ✅ Plugin added to VisualGraphsPlugin.prefab and GlobalPluginConfigAsset

**Public API:**
- ✅ `VisualGraphPlugin.LoadGraphAsync(string graphId)` - Load graphs dynamically
- ✅ `VisualGraphPlugin.UnloadGraph(string graphId)` - Unload graphs
- ✅ `VisualGraphPlugin.IsGraphLoaded(string graphId)` - Check graph status

---

## Phase 1.2: Save System Integration ✅ **COMPLETE**

**Current:** Full save/load with quest state persistence working  
**Target:** ✅ ACHIEVED - Production-ready save/load integration

**What Was Built:**

1. **`GraphStateSaveController : ISaveDomainController`** - Full save/load controller
   - Implements `ISaveDomainController` with `ESaveDomain.Graphs`
   - Saves all graph states via `IGraphRunner.ExportState()`
   - Saves `QuestManager` state (active, completed, claimed quests)
   - Loads quest state first, then graph states
   - Handles late registration (save system loads before plugin initialization)

2. **Quest State Persistence**
   - `QuestManager.GetSaveData()` - Serializes quest state
   - `QuestManager.RestoreSaveDataAsync()` - Restores quest state
   - `QuestManager.FinalizeCompletedQuestRestoration()` - Marks completed quests after graph state restoration
   - Proper ordering: Quest restoration → Graph state restoration → Quest finalization

3. **Graph State Persistence**
   - Filters out `ScriptableObject` references (can't be serialized by ES3)
   - Uses domain-prefixed keys (`graphs_graph_states`, `graphs_quest_manager_state`)
   - Waits for quest runners to be registered before restoring graph state
   - Handles missing runners gracefully

4. **Plugin Integration**
   - Auto-registers `GraphStateSaveController` with `SaveSystemCoordinator`
   - Handles late registration (manually triggers load if save system loaded first)
   - Auto-start quest logic checks for existing quests (prevents duplicates)
   - Proper cleanup on shutdown

**Key Features:**
- ✅ Saves all graph states (quest objective graphs, dialogue graphs, etc.)
- ✅ Saves quest manager state (active, completed-unclaimed, claimed quests)
- ✅ Restores quest state before graph state (ensures runners exist)
- ✅ Waits for runners to be registered before restoring graph state
- ✅ Handles completed quests correctly (restores state, then marks as completed)
- ✅ Filters out non-serializable types (ScriptableObject references)
- ✅ Handles late registration (save system loads before plugin)
- ✅ Auto-start skips quests that are already active/completed/claimed

**Files Created:**
- ✅ `Runtime/VisualGraphs/Persistence/GraphStateSaveController.cs` - Full implementation (322 lines)

**Files Modified:**
- ✅ `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Registers controller, handles late registration, auto-start checks
- ✅ `Runtime/VisualGraphs/Quest/QuestManager.cs` - Added save/load methods, finalization method
- ✅ `Runtime/VisualGraphs/Quest/IQuestManager.cs` - Added save/load interface methods
- ✅ `Runtime/VisualGraphs/Runtime/GraphRunner.cs` - Filters ScriptableObject references in ExportState

**Files Removed:**
- ✅ `Runtime/VisualGraphs/Persistence/GraphStateSaveProvider.cs` - Replaced by GraphStateSaveController

**Architecture:**
- ✅ Uses `ESaveDomain.Graphs` domain for all graph-related save data
- ✅ Domain-prefixed keys prevent conflicts with other save domains
- ✅ Proper async/await throughout (no blocking operations)
- ✅ Comprehensive logging at Information level for debugging

**Testing:**
- ✅ Verified quest state persists across game loads
- ✅ Verified graph state (objective progress) persists correctly
- ✅ Verified completed quests restore with correct completion percentage
- ✅ Verified auto-start doesn't duplicate restored quests

---

## Phase 1.3: MessagePipe Event Bus Integration ✅ **COMPLETE**

**Current:** Full bidirectional plugin-to-plugin communication working  
**Target:** ✅ ACHIEVED

**What Was Built:**

```csharp
// SimpleEventEmitter - Publishes to MessagePipe
public void Emit(IGameMessage message, string domain = null)
{
    // Uses reflection to call GlobalAsyncMessageBroker.Publish<T>() for concrete type
    var publishMethod = typeof(GlobalAsyncMessageBroker)
        .GetMethod(nameof(GlobalAsyncMessageBroker.Publish))
        ?.MakeGenericMethod(message.GetType());
    publishMethod.Invoke(null, new object[] { message });
}

// EventBusBridge - Subscribes from MessagePipe
public void SubscribeToGraphMessages()
{
    // Gets all message types from router.GetSubscribedMessageTypes()
    // For each type, uses reflection to call GlobalAsyncMessageBroker.GetSubscriber<T>()
    // Subscribes with OnMessageReceivedGeneric<T>() handler
    // Routes received messages to GraphEventRouter
}
```

**Features Delivered:**
- ✅ **Architecture** - Complete `IGameMessage` direct integration
- ✅ **Type-based subscriptions** - Uses `MessageTypeReference` for compile-time safety
- ✅ **O(1) routing** - `GraphEventRouter` routes by `(Type, domain)` tuples
- ✅ **Bidirectional** - Graphs can publish AND subscribe via MessagePipe
- ✅ **Dynamic subscription** - EventBusBridge auto-subscribes to types graphs care about
- ✅ **Reflection-based** - Works with any `IGameMessage` type (no code generation)
- ✅ **Uses existing messages** - Works with `SceneLoadedMessage`, `NavigationRequestMessage`, `ErrorRequestMessage`, etc.
- ✅ **Proper cleanup** - Disposable subscriptions, cancellation tokens

**How It Works:**
1. **Graphs load** → `GraphLoader` exports definitions and registers runners with `GraphEventRouter`
2. **Bootstrap calls** → `EventBusBridge.SubscribeToGraphMessages()` after all graphs load
3. **Bridge subscribes** → Gets unique message types from router, subscribes to each via `GlobalAsyncMessageBroker`
4. **Messages arrive** → MessagePipe → EventBusBridge → GraphEventRouter → IGraphRunner → Node executors
5. **Graphs emit** → Node executor → `IEventEmitter` → `GlobalAsyncMessageBroker.Publish<T>()`

**Setup Requirements:**
- `EventBusBridge` MonoBehaviour must be in scene (e.g., on same GameObject as `VisualGraphBootstrap`)
- `GlobalAsyncMessageBroker.Initialize()` must be called before graph system starts
- Graphs must have explicit subscriptions defined in `QuestGraphAsset.Subscriptions` or `DialogueGraphAsset.Subscriptions`

**Files Modified:**
- ✅ `Installer/VisualGraphInstaller.cs` - SimpleEventEmitter now publishes to MessagePipe
- ✅ `Bootstrap/EventBusBridge.cs` - Now subscribes to MessagePipe dynamically
- ✅ `Bootstrap/VisualGraphBootstrap.cs` - Calls `SubscribeToGraphMessages()` after loading
- ✅ `Runtime/GraphEventRouter.cs` - Added `GetSubscribedMessageTypes()` for bridge

**Note:** Quest-specific message types (e.g., `QuestStartedMessage`, `QuestCompletedMessage`, `QuestClaimedMessage`) were implemented in Phase 2.1 and 2.3. The system works with both quest-specific messages and existing `IGameMessage` types from other MToolKit subsystems.

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

---

## Phase 2.1: Quest Progress Tracking System ✅ **100% COMPLETE!**

**Current:** ✅ Full three-tier hierarchy with GUID-based references + Quest Manager  
**Target:** ✅ Track individual objective progress with "X/Y complete" display + lifecycle orchestration

**What Was Actually Built (BETTER than spec!):**

#### ✅ Data Structures (100% - BETTER THAN SPEC!)

**Built: Three-Tier GUID-Based Hierarchy**
- [x] ✅ `QuestObjective` (GuidScriptableObject) - Reusable objective assets
- [x] ✅ `QuestDefinition` (GuidScriptableObject) - Contains objectives list
- [x] ✅ `QuestCampaign` (GuidScriptableObject) - Contains quests list
- [x] ✅ `QuestObjectiveProgress` - Runtime progress tracking (Current/Required/IsComplete/Percentage)

**Why Better:** GUID-based references (no string typos!), reusable objectives, hierarchical organization

#### ✅ Graph State Support (100%)
- [x] ✅ `IGraphState` already supports nested data via generic `Set<T>` / `Get<T>`
- [x] ✅ Progress stored as: `state.Set("objective_{guid}", QuestObjectiveProgress)`
- [x] ✅ Query helpers in QuestDefinition: `GetObjectiveProgress()`, `GetCompletionPercentage()`, `IsComplete()`

#### ✅ Quest Nodes (100%)
- [x] ✅ `QuestObjectiveIncrementNode` - Increment objective progress by N
- [x] ✅ `QuestObjectiveSetNode` - Set objective progress to exact value
- [x] ✅ `QuestObjectiveCheckNode` - Branch if objective complete/incomplete
- [x] ✅ `QuestAllObjectivesCompleteNode` - Branch if all required objectives done

**Enhanced:** Nodes reference `QuestObjective` assets directly (GUID-safe!)

#### ✅ Node Executors (100%)
- [x] ✅ `QuestObjectiveIncrementNodeExecutor` - With debug logging
- [x] ✅ `QuestObjectiveSetNodeExecutor`
- [x] ✅ `QuestObjectiveCheckNodeExecutor` - Port-based branching
- [x] ✅ `QuestAllObjectivesCompleteNodeExecutor`

All registered with DI in `VisualGraphPlugin`

#### ✅ Quest Definitions (100%)
- [x] ✅ `QuestDefinition.Objectives: List<QuestObjective>` - References objective assets
- [x] ✅ `QuestObjective.RequiredProgress` - Per-objective required count
- [x] ✅ `QuestObjective.Optional` - Quest can complete without
- [x] ✅ `QuestObjective.Hidden` - Revealed dynamically
- [x] ✅ `QuestObjective.ObjectiveGraph` - Each objective owns its graph! (MAJOR ENHANCEMENT)

**Architecture Decision:** Objectives are ACTIVE (have graphs), Quests/Campaigns are PASSIVE (optional graphs)

#### ✅ Progress Events (100% - COMPLETE!)
- [x] ✅ `QuestObjectiveProgressMessage` - Emitted by increment/set executors
- [x] ✅ `QuestStartedMessage` - Emitted when quest starts
- [x] ✅ `QuestCompletedMessage` - Emitted when quest completes
- [x] ✅ `QuestClaimedMessage` - Emitted when quest rewards claimed
- [x] ✅ `QuestAbandonedMessage` - Emitted when quest abandoned
- [x] ✅ Event emission - All wired up with full context (quest GUID, objective definition, progress)

**Files Created:**
- ✅ `Runtime/VisualGraphs/Definitions/QuestObjective.cs` (GuidScriptableObject)
- ✅ `Runtime/VisualGraphs/Definitions/QuestCampaign.cs` (GuidScriptableObject)
- ✅ `Runtime/VisualGraphs/Quest/QuestObjectiveProgress.cs`
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestObjectiveIncrementNode.cs`
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestObjectiveSetNode.cs`
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestObjectiveCheckNode.cs`
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/Quest/QuestAllObjectivesCompleteNode.cs`
- ✅ `Runtime/VisualGraphs/Executors/QuestObjectiveIncrementNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Executors/QuestObjectiveSetNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Executors/QuestObjectiveCheckNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Executors/QuestAllObjectivesCompleteNodeExecutor.cs`

**Files Modified:**
- ✅ `Runtime/VisualGraphs/Definitions/QuestDefinition.cs` - Extended with GUID, objectives, helper methods
- ✅ `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Registered new executors

**Bonus: Message Data Flow System** ✅ (Not in original spec!)
- ✅ `MessageFieldCheckNode` - Branch based on message field values (enables filtering)
- ✅ `MessageFieldGetNode` - Extract field value to state
- ✅ `MessageTypeCheckNode` - Branch based on message type
- ✅ All executors implemented with reflection-based field access

**Files Created (Bonus):**
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/Message/MessageFieldCheckNode.cs`
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/Message/MessageFieldGetNode.cs`
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/Message/MessageTypeCheckNode.cs`
- ✅ `Runtime/VisualGraphs/Executors/MessageFieldCheckNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Executors/MessageFieldGetNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Executors/MessageTypeCheckNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/MESSAGE_DATA_FLOW.md` - Full documentation

#### ✅ Quest Manager / Orchestration (100% - **COMPLETE!**)

**The orchestrator that ties together all quest components! It's game-agnostic and provides essential coordination services.**

**Responsibilities (Framework Level) - ALL IMPLEMENTED:**
- [x] ✅ **Quest Lifecycle:** `StartQuestAsync()`, `CompleteQuest()`, `ClaimQuest()`, `AbandonQuest()`
- [x] ✅ **Graph Orchestration:** Auto-load/unload objective + quest graphs when quest becomes active
- [x] ✅ **State Queries:** `GetActiveQuests()`, `GetCompletedUnclaimedQuests()`, `GetClaimedQuestGuids()`, `IsQuestActive()`, `IsQuestCompleted()`, `IsQuestClaimed()`
- [x] ✅ **Progress Aggregation:** Query quest completion % from objectives via `GetQuestCompletionPercentage()`
- [x] ✅ **Message Emission:** Emit all lifecycle messages via MessagePipe
- [x] ✅ **Persistence Hook:** `GetSaveData()` / `RestoreSaveDataAsync()` for save/load integration

**What Quest Manager DOESN'T Do (Game Responsibility):**
- ❌ Decide WHICH quests to offer (game logic)
- ❌ Handle quest rewards (game emits messages, game's reward system handles)
- ❌ Check unlock conditions (graphs do this via state nodes)
- ❌ Implement quest UI (game implements, subscribes to manager's messages)

**Key Implementation Details:**
- Three-state lifecycle: Active → CompletedUnclaimed → Claimed (supports "complete but not claimed" state for reward UIs)
- Stores quest context in graph state (`__quest_guid`, `__quest_definition`) for executor access
- Auto-loads/unloads objective graphs via `GraphEventRouter`
- Full MessagePipe integration for lifecycle events
- Persistence-ready with `QuestManagerSaveData` DTO

**Files Created:**
- ✅ `Runtime/VisualGraphs/Quest/IQuestManager.cs` - Interface definition
- ✅ `Runtime/VisualGraphs/Quest/QuestManager.cs` - Full implementation
- ✅ `Runtime/VisualGraphs/Quest/QuestRuntimeState.cs` - Runtime quest state tracking
- ✅ `Runtime/VisualGraphs/Quest/QuestManagerSaveData.cs` - Persistence DTO
- ✅ `Runtime/VisualGraphs/Quest/Messages/QuestStartedMessage.cs`
- ✅ `Runtime/VisualGraphs/Quest/Messages/QuestCompletedMessage.cs`
- ✅ `Runtime/VisualGraphs/Quest/Messages/QuestClaimedMessage.cs`
- ✅ `Runtime/VisualGraphs/Quest/Messages/QuestAbandonedMessage.cs`
- ✅ `Runtime/VisualGraphs/Quest/Messages/QuestObjectiveProgressMessage.cs`
- ✅ `Runtime/VisualGraphs/Quest/QuestDatabase.cs` - Simple campaign registry
- ✅ `Runtime/VisualGraphs/Config/VisualGraphConfig.cs` - Added quest auto-start settings

**Files Modified:**
- ✅ `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Registered `IQuestManager` singleton, added auto-start logic
- ✅ `Runtime/VisualGraphs/Quest/Executors/QuestObjectiveIncrementNodeExecutor.cs` - Added message emission
- ✅ `Runtime/VisualGraphs/Quest/Executors/QuestObjectiveSetNodeExecutor.cs` - Added message emission

**Actual Time:** ~6 hours (afternoon session #2!) 🚀

---

## Summary

**Completed Phases:**
- ✅ Phase 1.0: Core Architecture (1.0.1-1.0.4)
- ✅ Phase 1.1: Plugin Architecture Integration
- ✅ Phase 1.3: MessagePipe Event Bus Integration
- ✅ Phase 2.1: Quest Progress Tracking System
- ✅ Phase 2.2: Quest Conditions & Requirements (Generic State System)
- ✅ Phase 2.3: Quest Rewards System
- ✅ Phase 4: Asset Reference System Overhaul (superseded by Phase 1.0.1)

**Key Achievements:**
- Production-ready event-driven architecture
- Full MToolKit plugin integration
- Bidirectional MessagePipe communication
- Complete quest lifecycle orchestration
- Message data flow system (bonus feature)

**Overall Progress:** 9 of 11 critical milestones complete! ✅

---

## Phase 2.2: Quest Conditions & Requirements ✅ **COMPLETE**

**Status:** ✅ **Generic State System fully implemented!**

**Approach:** Rearchitected as game-agnostic state system. Framework provides generic state nodes. Games implement their own condition logic.

**What Was Built:**

1. **Generic State Nodes** - Three new node types for state management:
   - `GenericStateSetNode` - Set arbitrary state keys with type conversion (bool, int, float, string)
   - `GenericStateCheckNode` - Branch execution based on state values with comparison operators
   - `GenericStateGetNode` - Read state values and store in other keys for comparisons

2. **State Change Events** - Reactive state change notifications:
   - `GraphStateChangedMessage` - Emitted when state values change
   - Enables reactive graphs that respond to state changes across graph boundaries

**Key Features:**
- ✅ Type support: bool, int, float, string, enum values
- ✅ Comparison operators: Equals, NotEquals, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual
- ✅ Case-insensitive string comparison option
- ✅ Debug logging for troubleshooting
- ✅ Default values for missing state keys
- ✅ Automatic state change message emission

**Files Created:**
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/State/GenericStateSetNode.cs`
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/State/GenericStateCheckNode.cs`
- ✅ `Runtime/VisualGraphs/Authoring/Nodes/State/GenericStateGetNode.cs`
- ✅ `Runtime/VisualGraphs/Executors/GenericStateSetNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Executors/GenericStateCheckNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Executors/GenericStateGetNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Runtime/Messages/GraphStateChangedMessage.cs`

**Files Modified:**
- ✅ `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Registered state executors

**Usage Example:**
```csharp
// In a graph:
// 1. GenericStateSetNode: Set "player_has_key" = true
// 2. GenericStateCheckNode: Check if "player_has_key" == true → branch to unlock door
// 3. Other graphs can subscribe to GraphStateChangedMessage to react to state changes
```

**Design Decision:** Framework provides generic state primitives. Games implement their own condition logic using these tools, keeping the framework game-agnostic while enabling powerful reactive patterns.

---

## Phase 2.3: Quest Rewards System ✅ **COMPLETE**

**Status:** ✅ **Message-based reward pattern fully implemented and documented!**

**Current Implementation:**
- ✅ `QuestClaimedMessage` - Emitted by `QuestManager.ClaimQuest()` when player claims rewards
- ✅ Games can subscribe to `QuestClaimedMessage` via MessagePipe to handle rewards
- ✅ Message includes `QuestGuid` and `QuestDefinition` for game to look up reward data

**How Games Handle Rewards:**
```csharp
// Game's RewardSystem subscribes to QuestClaimedMessage
GameMessageBroker.GetSubscriber<QuestClaimedMessage>()
    .Subscribe(async (msg, ct) => {
        var questDef = msg.Quest; // QuestDefinition contains reward data
        // Game's logic: Grant XP, items, currency based on questDef
        GrantRewards(questDef);
    });
```

**Completed:**
- ✅ `QuestClaimedMessage` emission via `QuestManager.ClaimQuest()`
- ✅ MessagePipe integration for game subscription
- ✅ Documentation added to README.md with implementation pattern

**Note:** Example reward system implementation is game-specific and not part of the framework.

**Decision:** Framework provides `QuestClaimedMessage` emission. Games subscribe and handle rewards based on their own `QuestDefinition` data structure. No additional framework code needed!

---

## Phase 3: Dialogue System Completion ✅ **COMPLETE**

**Status:** ✅ **Production-ready dialogue system with message-based architecture**

**What Was Built:**

### Core Dialogue Nodes & Executors

1. **`DialogueStartNode`** - Entry point for dialogue graphs
   - Subscribes to dialogue trigger messages
   - Initiates dialogue execution flow

2. **`DialogueLineNode`** - Displays dialogue text
   - Supports speaker ID, text, and localization keys
   - **Timing support** - `MinDisplaySeconds`, `AutoAdvanceDelaySeconds`, `AutoAdvance`, `Skippable` fields
   - Auto-advance mode with configurable delays
   - Minimum display time enforcement
   - Skippable lines (race condition between min time and player click)
   - Emits `DialogueShowMessage` to UI
   - Waits for user input via `DialogueProgressMessage` subscription

3. **`DialogueChoiceNode`** - Presents player choices
   - **Dynamic port support** - Automatically creates output ports for each choice
   - Supports up to 3 choices per node
   - Emits `DialogueShowChoiceMessage` to UI
   - Branches to selected choice output port only
   - Handles choice selection via `DialogueChoiceSelectedMessage`

### Message-Based Architecture

**Decision:** Implemented message-based communication instead of service interface pattern for better decoupling and flexibility.

**Messages Created:**
- `DialogueShowMessage` - Signals UI to display a dialogue line
- `DialogueShowChoiceMessage` - Signals UI to display choices
- `DialogueChoiceSelectedMessage` - Published by UI when player selects a choice
- `DialogueProgressMessage` - Published by UI to continue dialogue progression
- `DialogueContinueMessage` - Internal message to resume graph execution

**Benefits:**
- ✅ Multiple systems can react to dialogue events (3D positioning, audio, camera, etc.)
- ✅ Decoupled - UI implementation is game-specific
- ✅ Flexible - Easy to add new dialogue features without changing core system
- ✅ Testable - Messages can be mocked/subscribed to for testing

### Dynamic Port System

**Problem Solved:** xNode doesn't scan nested types for ports, so `[Output]` on nested `Choice` class was ignored.

**Solution Implemented:**
- Added `[Output(dynamicPortList = true)] public NodeConnection[] ChoiceOutputs;` directly to `DialogueChoiceNode`
- Implemented `SyncOutputPorts()` method to synchronize port count with choices list
- Uses `[OnValueChanged(nameof(SyncOutputPorts))]` to auto-update ports when choices change
- Exporter correctly maps xNode's dynamic port names (`"ChoiceOutputs {index}"`) to runtime format (`"Choice_{index}"`)

### Execution Flow

**Dialogue Progression:**
1. Entry node (`DialogueStartNode`) receives trigger message
2. Stores next node IDs in graph state (allows pausing)
3. Executes first dialogue node (`DialogueLineNode` or `DialogueChoiceNode`)
4. Node emits message to UI and stores next node IDs in state
5. Graph execution pauses, waiting for user input
6. UI publishes `DialogueProgressMessage` or `DialogueChoiceSelectedMessage`
7. `DialogueService` publishes `DialogueContinueMessage` to resume execution
8. `GraphRunner` reads next node IDs from state and continues
9. Process repeats until dialogue ends (no next nodes)

**Race Condition Fix:**
- Added one-frame delay (`await UniTask.Yield()`) in `DialogueService.OnDialogueChoiceSelectedMessage` before publishing `DialogueContinueMessage`
- Ensures executor has finished storing next node IDs in state before runner reads them

### Export & Serialization

**Choice Serialization:**
- `XNodeGraphExporter.ExtractChoiceNodeParameters()` manually serializes `Choices` list as `List<Dictionary<string, object>>`
- Stores choice text in runtime node parameters
- Executor correctly deserializes choices from parameters

**Port Connection Mapping:**
- `XNodeGraphExporter.ExtractChoiceNodeConnections()` maps xNode dynamic ports to runtime connections
- Handles both `"ChoiceOutputs {index}"` (xNode format) and `"Choice_{index}"` (runtime format)
- Correctly identifies which output port corresponds to which choice index

### Graceful Dialogue End

**Implementation:**
- `GraphRunner` detects when dialogue naturally ends (no next node IDs in state)
- Emits `DialogueProgressMessage(shouldClose: true)` to signal UI to close
- Changed from warning log to informational log (expected behavior)

### Localization Integration

- Dialogue nodes support `LocalizedString` fields
- Text can be localized using Unity's standard localization tooling
- Localization keys stored in node parameters and passed to UI

### Key Features Delivered:

- ✅ **Full dialogue execution** - Start, line display, choice selection, branching
- ✅ **Timing support** - Auto-advance, minimum display time, skippable lines
- ✅ **Dynamic choice ports** - Variable number of output ports based on choices
- ✅ **Message-based UI integration** - Decoupled, flexible communication
- ✅ **Quest integration** - `StartQuestNode` can be used in dialogue graphs to start quests
- ✅ **Proper state management** - Dialogue pauses/resumes correctly
- ✅ **Choice branching** - Only selected branch executes
- ✅ **Graceful dialogue end** - Auto-detects and closes when complete
- ✅ **Localization support** - Integrated with Unity localization
- ✅ **Race condition handling** - Proper async coordination
- ✅ **Comprehensive logging** - Verbose logging for debugging

### Files Created:

- ✅ `Runtime/VisualGraphs/Dialogue/Nodes/DialogueStartNode.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Nodes/DialogueLineNode.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Nodes/DialogueChoiceNode.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Executors/DialogueStartNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Executors/DialogueLineNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Executors/DialogueChoiceNodeExecutor.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Messages/DialogueShowMessage.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Messages/DialogueShowChoiceMessage.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Messages/DialogueChoiceSelectedMessage.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Messages/DialogueProgressMessage.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Messages/DialogueContinueMessage.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Graphs/DialogueGraphAsset.cs`
- ✅ `Runtime/VisualGraphs/Dialogue/Definitions/DialogueDefinition.cs`

### Files Modified:

- ✅ `Runtime/VisualGraphs/Export/XNodeGraphExporter.cs` - Added choice serialization and port mapping
- ✅ `Runtime/VisualGraphs/Runtime/GraphRunner.cs` - Added dialogue-specific execution flow and graceful end handling
- ✅ `Runtime/VisualGraphs/Dialogue/Nodes/DialogueChoiceNode.cs` - Added dynamic port support with `SyncOutputPorts()`

### Architecture Decisions:

**Message-Based vs Service Interface:**
- **Chosen:** Message-based architecture using `IGameMessage` and MessagePipe
- **Rationale:** Better decoupling, multiple subscribers, flexible for 3D dialogue systems
- **Alternative Considered:** `IDialogueUIService` interface (deferred - can be added later as wrapper if needed)

**Dynamic Ports:**
- **Chosen:** xNode's `[Output(dynamicPortList = true)]` with synchronization method
- **Rationale:** Native xNode support, automatic port creation, works with existing export system

**State Management:**
- **Chosen:** Store next node IDs in graph state, pause execution, resume via `DialogueContinueMessage`
- **Rationale:** Allows proper pausing between dialogue lines/choices, supports async UI operations

**Phase 3 Summary:** ✅ **Complete** - Production-ready dialogue system with full execution flow, dynamic choices, message-based UI integration, and proper state management. System is ready for 3D dialogue implementation.

---

## Phase 4: Asset Reference System Overhaul ✅ **SUPERSEDED BY PHASE 1.0.1**

**Status:** ✅ **Already Solved with Superior Approach!**

**Original Goal:** Replace `UnityEngine.Object.name` with meta GUID for safe references

**What Actually Happened:** Phase 1.0.1 implemented Unity's native `AssetReference` system, which is **better** than the originally planned meta GUID extraction!

### Why Phase 1.0.1's Approach Is Better:

✅ **No Meta File Parsing** - Unity's `AssetReference` already contains GUIDs  
✅ **Type-Safe** - `AssetReferenceGameObject`, `AssetReferenceAudioClip`, etc. provide compile-time type checking  
✅ **Native Addressables** - Seamless integration with Unity's Addressables system  
✅ **Validation Built-In** - Export-time validation via `XNodeGraphExporter`  
✅ **Production-Ready** - Unity's recommended pattern, battle-tested  

### Original Problems (All Solved):
- ✅ Object names can change → GUIDs don't change
- ✅ Duplicate names possible → GUIDs are unique
- ✅ No validation if asset is deleted → Export-time validation implemented
- ✅ Can't differentiate between assets with same name → GUIDs distinguish them

### What Was Built (Phase 1.0.1):
- ✅ `SerializableAssetReference` - Runtime DTO
- ✅ `IGraphAssetLoader` + `GraphAssetLoader` - Addressables loader
- ✅ Export validation in `XNodeGraphExporter`
- ✅ Runtime asset loading with proper lifecycle

**See Phase 1.0.1 for complete implementation details.**

### 4.1 Meta GUID Asset Reference System ❌ **NOT NEEDED - OBSOLETE**

**Original Plan (No Longer Relevant):**

~~- [ ] Create `AssetReference` system using Unity meta GUIDs~~  
~~- [ ] Update `NormalizeUnityObject` to extract meta GUID~~ ✅ Not needed - Unity's `AssetReference` handles this  
~~- [ ] Add validation during export~~ ✅ Already implemented in `XNodeGraphExporter`  
~~- [ ] Add runtime asset resolver~~ ✅ Already implemented as `IGraphAssetLoader` + `GraphAssetLoader`  
~~- [ ] Support addressables in resolver~~ ✅ Native Addressables support via `AssetReference`  
~~- [ ] Add migration tool for old graphs~~ ❌ Not needed - using Unity's native system from the start  

**Files Actually Created (Phase 1.0.1):**
- ✅ `Runtime/VisualGraphs/Runtime/DTOs/SerializableAssetReference.cs` (better than planned AssetReference)
- ✅ `Runtime/VisualGraphs/Runtime/AssetLoading/IGraphAssetLoader.cs` (better than IAssetReferenceResolver)
- ✅ `Runtime/VisualGraphs/Runtime/AssetLoading/GraphAssetLoader.cs` (full implementation)

**Files Modified (Phase 1.0.1):**
- ✅ `Export/XNodeGraphExporter.cs` - Uses Unity's `AssetReference` system with validation
- ✅ `Runtime/DTOs/RuntimeNodeDefinition.cs` - Stores `SerializableAssetReference`
- ✅ `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Registered asset loader

### 4.2 Asset Reference Validation ✅ **ALREADY IMPLEMENTED IN PHASE 1.0.1**

✅ **Pre-export validation** - Implemented in `XNodeGraphExporter`
  - ✅ Checks all `AssetReference` fields in nodes
  - ✅ Verifies assets exist via `RuntimeKeyIsValid()`
  - ✅ Reports missing or invalid references during export
  - ✅ Throws `InvalidGraphException` if critical references are broken

✅ **Runtime validation** - Implemented in `GraphAssetLoader`
  - ✅ Validates asset references on load
  - ✅ Logs warnings for missing assets
  - ✅ Graceful degradation for missing assets

~~- [ ] Create asset reference inspector~~ ❌ Not needed - Unity's Inspector handles `AssetReference` natively

**Phase 4 Summary:** ✅ **Complete via Phase 1.0.1** - All asset reference functionality implemented with Unity's native system instead of custom meta GUID extraction.

---

## Phase 6.2: Runtime Debugging Tools ✅ **COMPLETE**

**Comprehensive runtime debugging system for VisualGraphs - all requirements met!**

### What Was Built:

#### 1. Visual Node Highlighting in Graph Editor ✅
- **Real-time node execution highlighting** - Nodes show colored status bars when executing
- **Last executed node tracking** - Yellow highlight for most recently executed node
- **Currently executing indicator** - Green highlight for node currently running
- **Automatic graph ID resolution** - Cached lookup system for quest, dialogue, and objective graphs
- **Performance optimized** - O(1) lookups with periodic cache refresh

**Key Files:**
- `Editor/VisualGraphs/DebuggableNodeEditor.cs` - Custom xNode editor with visual highlighting
- `Editor/VisualGraphs/XNodeDebugState.cs` - Editor-side debug state tracking

**Features:**
- ✅ Visual status bars on nodes during runtime execution
- ✅ Color-coded indicators (green = executing, yellow = last executed)
- ✅ Cached graph ID resolution (quest, dialogue, objective graphs)
- ✅ Automatic repaint on execution events
- ✅ Works with all graph types (quest-level, objective, dialogue)

#### 2. Comprehensive Debugger Window ✅
- **Odin-powered debugger window** - Full-featured runtime debugging interface
- **Active graphs display** - Shows all currently loaded graphs with execution status
- **Execution history** - Records last 1000 node executions with timestamps
- **State change tracking** - Records last 500 state changes with old/new values
- **Graph statistics** - Per-graph execution metrics (total executions, avg/min/max time, error count)
- **Manual event triggering** - Test events by selecting message type and providing JSON payload
- **State inspector** - View and edit graph state values at runtime

**Key Files:**
- `Editor/VisualGraphs/XNodeOdinDebuggerWindow.cs` - Main debugger window
- `Runtime/VisualGraphs/Runtime/Debug/NodeDebugEvents.cs` - Static event system
- `Runtime/VisualGraphs/Runtime/Debug/INodeExecutionDebugEvent.cs` - Debug event interfaces
- `Runtime/VisualGraphs/Runtime/State/DebuggableGraphState.cs` - State wrapper that emits debug events

**Features:**
- ✅ Active graphs list with execution status, domain, and trigger messages
- ✅ Node execution history with node type, execution time, and error messages
- ✅ State change history with old/new values and timestamps
- ✅ Graph statistics (total executions, average/min/max execution time, error counts)
- ✅ Manual event triggering with JSON payload support
- ✅ State inspector with runtime editing capabilities
- ✅ Auto-refresh during play mode
- ✅ Accessible via `Tools/MToolKit/VisualGraphs Debugger` menu

#### 3. Runtime Debug Event System ✅
- **Lightweight event emission** - Static events for editor subscription
- **Node execution tracking** - Emits events when nodes execute with timing and error info
- **State change tracking** - Emits events when graph state changes
- **Graph lifecycle tracking** - Emits events when graphs start/stop execution
- **Zero runtime overhead** - Events only fire if editor is subscribed

**Integration Points:**
- ✅ `GraphRunner` - Emits node execution and graph lifecycle events
- ✅ `DebuggableGraphState` - Wraps `IGraphState` to emit state change events
- ✅ `GraphLoader` - Automatically wraps states with `DebuggableGraphState`

### Phase 6.2 Requirements Coverage:

| Requirement | Status | Implementation |
|------------|--------|----------------|
| Show active graphs in hierarchy | ✅ | Active graphs list in debugger window |
| Display current execution state | ✅ | Active graphs show execution status, domain, trigger messages |
| Show which nodes executed recently | ✅ | Execution history table + visual highlighting |
| Visualize event flow | ✅ | Execution history shows event flow, state changes tracked |
| Record last N node executions | ✅ | Last 1000 executions recorded |
| Show execution time per node | ✅ | Execution history shows timing, stats show avg/min/max |
| Display state changes over time | ✅ | State change history table (last 500 changes) |
| Show all active graph states | ✅ | State inspector shows all graphs and their state |
| Allow editing state values at runtime | ✅ | State inspector allows editing values |
| Trigger events manually for testing | ✅ | Manual event triggering with JSON payload |

### Performance Optimizations:

- ✅ **Cached graph ID lookups** - O(1) dictionary lookups instead of O(n*m) searches
- ✅ **Periodic cache refresh** - Cache rebuilds every 2 seconds in play mode
- ✅ **History size limits** - Execution history (1000) and state changes (500) capped
- ✅ **Lazy event subscription** - Editor only subscribes when window is open
- ✅ **Efficient repaint** - Uses `EditorApplication.delayCall` for batched repaints

### Usage:

1. **Visual Highlighting:**
   - Open any graph in the xNode editor
   - Enter play mode
   - Nodes will automatically highlight as they execute

2. **Debugger Window:**
   - Open via `Tools/MToolKit/VisualGraphs Debugger`
   - View active graphs, execution history, state changes, and statistics
   - Use manual event triggering to test graph behavior
   - Edit state values in the state inspector

### Key Files Created:

**Runtime:**
- `Runtime/VisualGraphs/Runtime/Debug/INodeExecutionDebugEvent.cs`
- `Runtime/VisualGraphs/Runtime/Debug/NodeDebugEvents.cs`
- `Runtime/VisualGraphs/Runtime/State/DebuggableGraphState.cs`

**Editor:**
- `Editor/VisualGraphs/XNodeDebugState.cs`
- `Editor/VisualGraphs/XNodeOdinDebuggerWindow.cs`
- `Editor/VisualGraphs/DebuggableNodeEditor.cs`

### Key Files Modified:

- `Runtime/VisualGraphs/Runtime/GraphRunner.cs` - Added debug event emission
- `Runtime/VisualGraphs/Runtime/Loading/GraphLoader.cs` - Wraps states with `DebuggableGraphState`

**Phase 6.2 Summary:** ✅ **Complete** - All runtime debugging requirements implemented with visual highlighting, comprehensive debugger window, and full event tracking system.

---

