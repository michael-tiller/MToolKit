# VisualGraphs Subsystem for MToolKit

## Overview

Event-driven visual graph subsystem for Unity using xNode for authoring, with runtime execution being POCO-based, DI-aware (VContainer), and reactive via MessagePipe. Designed to enable systemic, emergent gameplay through reactive event-driven interactions.

**Current Status:** Production-ready for Quest and Dialogue systems. Core architecture complete. General visual scripting (Phase 9+) planned for future.

## When to Use Graphs vs Code

VisualGraphs is designed for **orchestration and policy**, not low-level mechanics. Use a three-layer architecture:

### 1. Mechanical Layer (Code Only)

**Keep in C#:**
- Movement, physics, hit detection
- Raw animation playback
- Low-level AI (pathfinding, steering)
- Rendering, shaders, graphics
- Network serialization
- Performance-critical loops

**These systems emit messages:**
- `EnemyHitMessage`, `EnemyDiedMessage`
- `PlayerEnteredZoneMessage`, `PlayerLeftZoneMessage`
- `ItemPickedUpMessage`, `ItemDroppedMessage`
- `DamageDealtMessage`, `HealthChangedMessage`

### 2. Domain Services (Code, Authoritative State)

**Keep in C# services:**
- `QuestManager`, `InventoryService`, `CombatService`, `DialogueService`
- Own the actual data and invariants
- Handle business logic and validation
- Maintain authoritative state

**These services:**
- Emit domain events: `QuestStartedMessage`, `ItemPickedUpMessage`, `DialogueLineShownMessage`
- Handle commands from graphs or other systems
- Provide APIs that graph executors can call

### 3. Orchestration Layer (Graphs)

**Put in graphs:**
- Tutorial steps and progression
- Quest progress tracking and completion logic
- Dialogue triggers and branching
- UI prompts and notifications
- World toggles (spawners, doors, weather)
- Cross-system coordination ("when X happens, do Y and Z")

**Graphs subscribe to events and decide what other systems to poke:**
- React to `ItemPickedUpMessage` → start tutorial step
- React to `EnemyDefeatedMessage` → increment quest objective
- React to `QuestCompletedMessage` → unlock dialogue
- Optionally emit new messages back into the bus

### Example: Tutorial from Item Pickup

**Mechanical/Inventory Code (C#):**
```csharp
// In your inventory system
public void PickupItem(Item item)
{
    inventory.Add(item);
    // Emit event - graphs react to this
    GameMessageBroker.Publish(new ItemPickedUpMessage 
    { 
        ItemId = item.Id, 
        Quantity = 1 
    });
}
```

**Orchestration Graph:**
```
[Entry: ItemPickedUpMessage]
  → MessageFieldCheck(ItemId == "FirstGun")
    → [Matches] → Tutorial/StartStep("FirstGunTutorial")
    → [No Match] → (end)
```

### Example: Quest Completion from Enemy Defeat

**Combat Code (C#):**
```csharp
// In your combat system
public void OnEnemyDeath(Enemy enemy, Player killer)
{
    // Resolve damage, XP, loot
    GrantXP(killer, enemy.XPReward);
    DropLoot(enemy);
    
    // Emit event - graphs react to this
    GameMessageBroker.Publish(new EnemyDefeatedMessage 
    { 
        EnemyId = enemy.Id, 
        Location = enemy.Position,
        KillerId = killer.Id 
    });
}
```

**Quest Graph:**
```
[Entry: EnemyDefeatedMessage]
  → MessageFieldCheck(EnemyId == "Boss01")
    → [Matches] → QuestObjectiveIncrement("KillBoss01")
    → [No Match] → (end)
```

### Example: Cross-System Communication

**Dialogue Graph:**
- Emits `DialogueChoiceSelectedMessage` when player makes a choice

**Quest Graph:**
- Listens for `DialogueChoiceSelectedMessage`
- Increments objective or completes quest based on choice

**Quest Graph:**
- Emits `QuestCompletedMessage` when quest finishes

**Dialogue Graph:**
- Listens for `QuestCompletedMessage`
- Unlocks follow-up conversations

Everything is messages in and out; graphs glue them together.

### Message Taxonomy Best Practices

**Define explicit message domains:**
- `Core` - System-level events (scene loaded, game started)
- `Combat` - Combat-related events
- `Inventory` - Item pickup/drop/use
- `Quest` - Quest lifecycle events
- `Dialogue` - Dialogue events
- `UI` - UI state changes
- `Tutorial` - Tutorial progression

**Keep messages event-like and idempotent:**
- ✅ "X happened" or "X state changed from A→B"
- ❌ "Please do X" (use service calls for commands)

**Use domain filters in graph subscriptions:**
- Subscribe to `EnemyDefeatedMessage` in domain `Combat`
- Prevents accidental cross-domain coupling

### Decision Matrix

**Use Graphs When:**
- ✅ High-level behavior that changes frequently
- ✅ Content-driven logic (quests, dialogue, tutorials)
- ✅ Cross-system coordination
- ✅ Best reviewed visually ("when player does A and B, unlock C")
- ✅ Policy decisions ("if player has item X and completed quest Y, show dialogue Z")

**Use Code When:**
- ✅ Numeric tuning formulas
- ✅ Tight loops or performance-critical logic
- ✅ Deep domain invariants
- ✅ Low-level mechanics (physics, rendering, networking)
- ✅ Complex algorithms (pathfinding, AI decision trees)

**Remember:** Graphs are policy documents. They orchestrate systems, they don't replace them.

### When to Refactor Graphs

**Split a Graph When:**
- Graph exceeds complexity budget (e.g., >50 nodes, >10 depth, >5 entry points)
- Graph handles multiple unrelated concerns (e.g., quest logic + UI + world state)
- Graph is hard to read or reason about
- Multiple people need to edit different parts simultaneously

**Extract to Function Graph When:**
- Same logic pattern appears in multiple graphs (DRY violation)
- You have a reusable subgraph that could be called from multiple places
- Logic is self-contained and doesn't need graph-specific state
- You want to test a piece of logic in isolation

**Move Logic to C# Service When:**
- Graph contains business rules or invariants (see Risk #6)
- Logic is duplicated across multiple graphs
- Logic needs to be unit tested
- Logic is performance-critical
- Logic is complex enough that C# would be more readable
- You find yourself writing the same validation/calculation pattern repeatedly

**Red Flags (Move to C# Immediately):**
- Graph directly modifies domain objects (inventory, quest status, etc.)
- Graph contains complex algorithms or calculations
- Graph enforces business rules that must always hold
- Graph logic is hard to test or verify

## Architecture

### Authoring Layer (Editor-Only)

- **xNode Graphs**: `QuestGraphAsset`, `DialogueGraphAsset`
- **Base Classes**: `VisualGraphNodeBase` (with stable GUID), `EntryNodeBase`
- **Event Subscriptions**: Graphs explicitly declare MessagePipe subscriptions at the graph level
- **Example Nodes**: 
  - Quest: `QuestOnEventNode`, `QuestSetStageNode`, `QuestObjectiveIncrementNode`
  - Dialogue: `DialogueStartNode`, `DialogueLineNode`, `DialogueChoiceNode`
  - Message Data Flow: `MessageFieldCheckNode`, `MessageFieldGetNode`, `MessageTypeCheckNode`

### Export Layer

- **XNodeGraphExporter**: Validates authoring graphs and exports to deterministic DTOs
- Uses Odin serialization to extract parameters matching inspector-visible data
- Validates node types, GUIDs, entry nodes, and AssetReferences
- Normalizes UnityEngine.Object references to Addressable keys/GUIDs

### Runtime Layer (POCO, No MonoBehaviours)

- **Interfaces**: `IGraphRunner`, `IGraphNodeExecutor`, `IGraphState`, `IRuntimeGraphDefinition`, `IGameMessage`, `IEventEmitter`
- **DTOs**: `RuntimeGraphDefinition`, `RuntimeNodeDefinition`, `RuntimeConnectionDefinition`, `RuntimeSubscriptionDefinition`
- **Execution**: 
  - `GraphRunner`: Idempotent event handling with sequence IDs
  - `GraphEventRouter`: O(1) indexed event routing by (type, domain)
  - `NodeExecutorRegistry`: Type-safe executor lookup
  - `GraphNodeExecutionContext`: Executor-controlled continuation

### State Management

- **InMemoryGraphState**: Per-graph state container
- **GraphStateSnapshot**: Serializable state for save/restore
- **Application Order**: Global vars → Definition vars → Saved state (wins)

### Unity Bridge

- **VisualGraphPlugin**: Plugin that manages graph system lifecycle, initialization, and DI registration
- **EventBusBridge**: Bridges MessagePipe events to graph router using reflection-based dynamic subscriptions
- **VisualGraphConfig**: ScriptableObject configuration for the plugin

### Variable System

- **GraphVariableSet**: Key/type/value variable collections
- **GlobalGraphVariables**: Project-wide variables per graph ID
- Applied in order: global → definition → save state

### Definitions

- **QuestDefinition**: Links quest ID to graph + config
- **DialogueDefinition**: Links dialogue ID to graph + config
- **VisualGraphRegistry**: Central registry of all definitions

## Key Features

✅ **Deterministic**: Stable GUIDs ensure node identity persists across authoring changes  
✅ **Event-Driven**: No Update loop, pure reactive execution via MessagePipe  
✅ **DI-Aware**: Full VContainer integration, testable  
✅ **Idempotent**: Sequence IDs prevent duplicate event processing  
✅ **Indexed Routing**: O(1) event lookup by type and domain  
✅ **Executor-Controlled**: Executors decide continuation (enables conditional flow)  
✅ **Type-Safe Subscriptions**: Graph-level subscriptions use type references, not strings  
✅ **Message Data Flow**: Extract and check message fields using reflection-based nodes  
✅ **Quest Lifecycle**: Complete quest management with progress tracking  
✅ **Message-Based Rewards**: Game-agnostic reward pattern via MessagePipe  
✅ **Error Handling**: Graceful degradation with diagnostic events  
✅ **Observable**: Emits events for Quest/Dialogue state changes  

## Event-Based Entry System

Graphs subscribe to MessagePipe events explicitly at the graph level. When a matching event is published, the graph's entry nodes are triggered.

### How It Works

1. **Graph Subscriptions**: Each graph asset declares which message types it subscribes to:
   ```csharp
   public class QuestGraphAsset : NodeGraph {
       [BoxGroup("Event Subscriptions")]
       public List<MessageSubscription> Subscriptions = new();
   }
   ```

2. **EventBusBridge**: Dynamically subscribes to MessagePipe for all message types that loaded graphs care about

3. **Routing**: When a message is published, `GraphEventRouter` finds matching graphs and triggers their entry nodes

4. **Execution**: Entry nodes execute, and flow continues through connected nodes

### Benefits

- **Clean Separation**: Graphs declare their dependencies explicitly
- **Type-Safe**: Uses `MessageTypeReference` with Odin validation
- **Flexible**: Multiple graphs can subscribe to the same event type
- **Game-Agnostic**: Works with any `IGameMessage` implementation

### Known Limitations

- Sequential execution (graphs execute one at a time)
- No execution order guarantee between graphs
- Reflection overhead for dynamic subscriptions
- Runtime type safety depends on export-time validation

See `PRODUCTION_ROADMAP.md` for hardening plans.

## Usage

### 1. Create a Graph

Right-click in Unity:
- `Create > MToolKit > Visual Graphs > Quest Graph`
- `Create > MToolKit > Visual Graphs > Dialogue Graph`

### 2. Configure Event Subscriptions

In the graph asset inspector:
- Add subscriptions to message types the graph should respond to
- Use the "Auto-Populate from Entry Nodes" button to generate subscriptions from existing entry nodes
- Validate subscriptions using the "Validate Graph" button

### 3. Author Nodes

Add nodes via right-click in xNode editor:
- Quest: `Quest/On Event`, `Quest/Set Stage`, `Quest/Objective Increment`
- Dialogue: `Dialogue/Start`, `Dialogue/Line`, `Dialogue/Choice`
- Message: `Message/Field Check`, `Message/Field Get`, `Message/Type Check`

### 4. Create Definition

Right-click in Unity:
- `Create > MToolKit > Visual Graphs > Quest Definition`
- Link your graph asset, set quest ID, configure variables

### 5. Add to Registry

Right-click in Unity:
- `Create > MToolKit > Visual Graphs > Registry`
- Add your quest/dialogue definitions

### 6. Bootstrap

Add `VisualGraphPlugin` to a GameObject in your scene. Create a `VisualGraphConfig` asset and assign the registry to it. The plugin will handle initialization automatically.

### 7. Setup

The `VisualGraphPlugin` handles all VContainer registration automatically through its `Register()` method. Just ensure:
- The plugin prefab is added to your scene
- The `VisualGraphConfig` is created and assigned
- The plugin is registered in `GlobalPluginConfigAsset`
- An `EventBusBridge` component is on the same GameObject (auto-created if missing)

### 8. Send Events

Events matching graph subscriptions will trigger execution automatically via MessagePipe:

```csharp
using MToolKit.Runtime.MessageBus;

// Publish any IGameMessage - graphs subscribed to this type will execute
GameMessageBroker.Publish(new MyCustomMessage());
```

## Quest System

### Quest Lifecycle

The framework provides a complete quest lifecycle through `IQuestManager`:

- **Start Quest**: `await questManager.StartQuestAsync(questGuid)`
- **Complete Quest**: `questManager.CompleteQuest(questGuid)` (called automatically when all objectives complete)
- **Claim Quest**: `await questManager.ClaimQuest(questGuid)` (player claims rewards)
- **Abandon Quest**: `await questManager.AbandonQuest(questGuid)`

### Quest Progress Tracking

Quest graphs can track objective progress using quest nodes:
- `QuestObjectiveIncrementNode` - Increment an objective counter
- `QuestObjectiveSetNode` - Set an objective to a specific value
- `QuestObjectiveCheckNode` - Check if an objective meets a condition
- `QuestAllObjectivesCompleteNode` - Check if all objectives are complete

Progress is automatically tracked and `QuestObjectiveProgressMessage` events are emitted when objectives update.

### Quest Rewards Pattern

The framework provides **message-based reward handling** that is game-agnostic. Games implement their own reward logic by subscribing to `QuestClaimedMessage`.

#### Framework Side (Already Implemented)

When a player claims a quest, `QuestManager` emits a `QuestClaimedMessage` via MessagePipe:

```csharp
// Framework automatically emits this when ClaimQuest() is called
var message = new QuestClaimedMessage(questGuid, questDefinition, totalDuration);
questClaimedPublisher.Publish(message);
```

#### Game Side (Your Implementation)

Your game's reward system subscribes to `QuestClaimedMessage` and handles rewards based on your own `QuestDefinition` data structure:

```csharp
using MToolKit.Runtime.VisualGraphs.Quest.Messages;
using MToolKit.Runtime.MessageBus;

public class RewardSystem
{
    public void Initialize()
    {
        // Subscribe to quest claimed events
        var subscriber = GameMessageBroker.GetSubscriber<QuestClaimedMessage>();
        subscriber.Subscribe(OnQuestClaimed);
    }
    
    private async UniTask OnQuestClaimed(
        QuestClaimedMessage message, 
        CancellationToken ct)
    {
        var questDef = message.Quest; // QuestDefinition contains your reward data
        var questGuid = message.QuestGuid;
        
        // Your game's reward logic here
        // Example: Read reward data from QuestDefinition
        if (questDef.TryGetRewardData(out var rewardData))
        {
            GrantExperience(rewardData.Experience);
            GrantGold(rewardData.Gold);
            GrantItems(rewardData.Items);
        }
        
        // Or use your own reward system
        // YourRewardService.GrantRewards(questDef);
    }
}
```

#### QuestDefinition Structure

Store reward data in your `QuestDefinition` ScriptableObject:

```csharp
[CreateAssetMenu(menuName = "MToolKit/Quest Definition")]
public class QuestDefinition : GuidScriptableObject
{
    // ... quest fields ...
    
    // Your reward data structure
    [SerializeField] private QuestRewardData rewardData;
    
    public bool TryGetRewardData(out QuestRewardData data)
    {
        data = rewardData;
        return rewardData != null;
    }
}

[Serializable]
public class QuestRewardData
{
    public int Experience;
    public int Gold;
    public List<ItemReward> Items;
}
```

#### Key Points

- ✅ **Framework provides**: `QuestClaimedMessage` emission
- ✅ **Game provides**: Reward data structure in `QuestDefinition`
- ✅ **Game provides**: Reward handling logic via MessagePipe subscription
- ✅ **Game-agnostic**: Framework doesn't assume XP, gold, items, etc.

This pattern allows each game to define its own reward types (XP, currency, items, achievements, etc.) while the framework handles the quest lifecycle and message emission.

## Message Data Flow

The system provides nodes for extracting and checking data from incoming messages using reflection:

### MessageFieldCheckNode

Branch based on message field values:

```
[Entry] → MessageFieldCheckNode("enemyType", "DragonBoss") 
  → [Matches] → QuestObjectiveIncrementNode("defeat_dragon")
  → [No Match] → (end)
```

### MessageFieldGetNode

Extract field values to graph state:

```
[Entry] → MessageFieldGetNode("intensity", "rain_intensity") 
  → SetVariableNode("weather_intensity", "rain_intensity")
```

### MessageTypeCheckNode

Branch based on message type:

```
[Entry] → MessageTypeCheckNode(EnemyDefeatedMessage)
  → [Matches] → (handle enemy defeat)
  → [No Match] → MessageTypeCheckNode(ItemAcquiredMessage)
    → [Matches] → (handle item acquisition)
```

These nodes enable conditional logic based on message payloads, serving as a stopgap until full data flow is implemented in Phase 9. See `MESSAGE_DATA_FLOW.md` for detailed documentation.

## Event Routing

Events are routed by **(type, domain)** for O(1) lookup:

- `Quest.Started` in domain `Quest` → routes to quest graphs subscribing to that event
- `Dialogue.LineShown` in domain `Dialogue` → routes to dialogue graphs
- Empty domain = wildcard (matches all)

## Custom Node Types

### 1. Create Node Class

```csharp
using MToolKit.Runtime.VisualGraphs.Authoring;

[Node.CreateNodeMenu("MyCategory/MyNode")]
public class MyCustomNode : VisualGraphNodeBase
{
    [Input] public NodeConnection input;
    [Output] public NodeConnection output;
    
    public string myParameter = "value";
    public int myNumber = 42;
    public AssetReferenceGameObject Prefab; // Auto-validated & serialized
}
```

### 2. Create Executor

```csharp
using MToolKit.Runtime.VisualGraphs;

public class MyCustomNodeExecutor : IGraphNodeExecutor
{
    public string NodeType => "MyCustomNode";
    
    public UniTask ExecuteAsync(
        IRuntimeGraphDefinition graph,
        RuntimeNodeDefinition node,
        IGraphState state,
        IGameMessage message,
        GraphNodeExecutionContext context,
        CancellationToken ct = default)
    {
        var param = node.Parameters["myParameter"] as string;
        var number = Convert.ToInt32(node.Parameters["myNumber"]);
        
        // Your logic here
        
        // Continue to connected nodes
        foreach (var conn in graph.GetConnectionsFrom(node.NodeId))
            context.EnqueueNext(conn.ToNodeId);
            
        return UniTask.CompletedTask;
    }
}
```

### 3. Register in Plugin

The `VisualGraphPlugin` automatically discovers and registers all `IGraphNodeExecutor` implementations via VContainer. Just ensure your executor is in an assembly that's loaded:

```csharp
// In VisualGraphPlugin.Register():
builder.Register<MyCustomNodeExecutor>(Lifetime.Singleton)
    .As<IGraphNodeExecutor>();
```

## State Management

### Set Variables

```csharp
state.Set("questStage", 2);
state.Set("hasItem", true);
state.Set("playerName", "Hero");
```

### Get Variables

```csharp
if (state.TryGet<int>("questStage", out var stage))
{
    // Use stage
}
```

### Save/Restore

State is automatically saved/restored through the save system integration (Phase 1.2 - complete!). The framework provides `GraphStateSnapshot` for serialization.

## File Structure

```
VisualGraphs/
├── Runtime/
│   ├── Interfaces/          # Core interfaces
│   ├── DTOs/                # Serializable definitions
│   ├── State/               # State management
│   ├── Execution/           # Execution infrastructure
│   ├── Messages/            # Event message implementations
│   ├── GraphRunner.cs       # Main graph runner
│   └── GraphEventRouter.cs  # Event routing
├── Authoring/
│   ├── VisualGraphNodeBase.cs
│   ├── EntryNodeBase.cs
│   ├── Graphs/              # Graph asset types
│   └── Nodes/               # Example authoring nodes
│       ├── Quest/
│       ├── Dialogue/
│       └── Message/
├── Export/
│   └── XNodeGraphExporter.cs # Authoring → Runtime export
├── Variables/
│   ├── GraphVariableSet.cs
│   └── GlobalGraphVariables.cs
├── Definitions/
│   ├── QuestDefinition.cs
│   ├── DialogueDefinition.cs
│   └── VisualGraphRegistry.cs
├── Quest/
│   ├── QuestManager.cs
│   ├── IQuestManager.cs
│   ├── Messages/
│   └── Executors/
├── Dialogue/
│   └── Executors/
├── Bootstrap/
│   └── EventBusBridge.cs
├── Config/
│   └── VisualGraphConfig.cs
├── VisualGraphPlugin.cs
├── Persistence/
│   └── GraphStateSaveProvider.cs (legacy - to be replaced)
└── Executors/               # Example executors
    ├── Quest/
    ├── Dialogue/
    └── Message/
```

## Integration Checklist

- [x] Runtime infrastructure (POCO, DI-aware)
- [x] Authoring base classes (GUID-bearing nodes)
- [x] xNode graph assets (Quest, Dialogue)
- [x] Export layer with validation
- [x] Event routing with indexed lookup
- [x] State management with save/restore support
- [x] Variable system (global + per-definition)
- [x] Definition assets
- [x] Unity bridge MonoBehaviours
- [x] VContainer installer
- [x] Example executors
- [x] MessagePipe event bus integration ✅ (via GameMessageBroker - complete)
- [x] Event-based entry system ✅ (graph-level subscriptions)
- [x] Message data flow nodes ✅ (MessageFieldCheck, MessageFieldGet, MessageTypeCheck)
- [x] Quest system ✅ (full lifecycle with QuestManager)
- [x] Quest rewards pattern ✅ (message-based, game-agnostic)
- [x] Save system integration (Phase 1.2 - complete!)
- [ ] Dialogue UI service (Phase 3.1 - IDialogueUIService interface)
- [ ] Test coverage (Phase 5 - target: 100%)

## Current Limitations

### Known Issues

- **Dialogue UI**: Dialogue nodes have TODOs for `IDialogueUIService` (Phase 3.1)
- **Test Coverage**: No automated tests yet (Phase 5)
- **General Visual Scripting**: Core programming constructs (variables, math, logic) planned for Phase 9+

### Event-Based Entry System Limitations

See `PRODUCTION_ROADMAP.md` "Event-Based Entry System Hardening" section for detailed risks and mitigation plans.

## Notes

- **xNode is authoring-only**: Runtime has no dependency on xNode
- **Plugin architecture**: `VisualGraphPlugin` manages full lifecycle with proper initialization
- **Saved state wins**: Application order ensures saved data overrides authoring
- **Executors control continuation**: Call `context.EnqueueNext()` to continue
- **Stable GUIDs**: Node identity persists across editor changes
- **Type safety**: Executor registry validates node types during export
- **MessagePipe is the event bus**: All event routing goes through MessagePipe via `GameMessageBroker`
- **Reflection-based subscriptions**: `EventBusBridge` uses reflection to dynamically subscribe to message types

## Roadmap

### Version 1.0 Vision (Summary)

**Goal:** A first-class visual scripting engine used for almost all high-level game logic, with C# handling mechanics and services.

**At Version 1.0, VisualGraphs will provide:**

- **Dedicated Editor**: Full-featured visual graph editor with type-colored pins, validation, search, minimap, and debugging tools
- **Complete Node Library**: Events, logic, math, state, Unity integration, and domain nodes (quest, dialogue, inventory, combat, AI, etc.)
- **Systemic Gameplay**: Support for SS14/RimWorld-style reactive systems through cross-plugin orchestration graphs
- **Production Ready**: Full test coverage, benchmarks, stress tests, and mature tooling you can trust in production
- **Default Use Cases**: Quests, dialogue, tutorials, dynamic world scripting, UI flows, and one-off content logic

**Architecture:** Graphs orchestrate high-level behavior ("when X happens, do Y and emit Z") over a unified message bus, while C# services handle mechanics, data models, and performance-critical logic.

**See `PRODUCTION_ROADMAP.md` for:**
- Detailed Version 1.0 vision with full specifications
- Completed phases (moved to `CHANGELOG.md`)
- Current work and priorities
- Future phases (general visual scripting, Unity integration, performance, etc.)
- Long-term vision (Blueprint-like system)

## License

Part of MToolKit framework. See main repository for license.
