# VisualGraphs Subsystem - Production Roadmap

## Current Status

**Overall Completeness: 95% (Beta Quality)**

**Core Architecture: 100%** ✅ - Production-ready, event-driven, POCO-based graph execution  
**Asset System: 100%** ✅ - Modern AssetReference with validation (1.0.1 complete)  
**Type System: 100%** ✅ - Type-based subscriptions with MessagePipe integration (1.0.2 complete)  
**Performance System: 100%** ✅ - Per-graph execution limits (1.0.3 complete)  
**Loading System: 100%** ✅ - Addressables with lazy loading (1.0.4 complete)  
**Plugin Integration: 100%** ✅ - Full plugin lifecycle with config system (1.1 complete!)  
**MessagePipe Integration: 100%** ✅ - Bidirectional pub/sub working (1.3 complete!)  
**Message Data Flow: 100%** ✅ - Field checks, extraction, type branching (2.1 bonus!)  
**Quest System: 100%** ✅ - Full lifecycle orchestration with Quest Manager (Phase 2.1 complete!)  
**Quest Rewards: 100%** ✅ - Message-based reward pattern implemented and documented (Phase 2.3 complete!)  
**Save System Integration: 100%** ✅ - Full save/load with quest state persistence (Phase 1.2 complete!)  
**Quest Conditions: 100%** ✅ - Generic state system implemented (game-agnostic approach)  
**Test Coverage: 0%** ❌ - No tests written (target: 100%)

---

## Phase 1: Critical Integration (Foundation)

**Goal:** Integrate with MToolKit's core patterns so the system works with existing infrastructure

### 1.0 Core Architecture Fixes ✅ **COMPLETE**

**See `CHANGELOG.md` for details.**

---

### 1.1 Plugin Architecture Integration ✅ **COMPLETE**

**See `CHANGELOG.md` for details.**

---

### 1.2 Save System Integration ✅ **COMPLETE**

**See `CHANGELOG.md` for details.**

---

### 1.3 MessagePipe Event Bus Integration ✅ **COMPLETE**

**See `CHANGELOG.md` for details.**

---

## Phase 2: Quest System Enhancements ✅ **COMPLETE**

**Goal:** Add objective progress tracking, state management, and quest orchestration

**Status:** ✅ **All Phase 2 tasks complete!**

**See `CHANGELOG.md` for complete implementation details:**
- 2.1 Quest Progress Tracking System ✅
- 2.2 Quest Conditions & Requirements (Generic State System) ✅
- 2.3 Quest Rewards System ✅

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

## Phase 4: Asset Reference System Overhaul ✅ **SUPERSEDED BY PHASE 1.0.1**

**See `CHANGELOG.md` for details.**

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

- [] MessagePipe integration tests
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

### 6.2 Runtime Debugging Tools ✅ **COMPLETE**

**See `CHANGELOG.md` for complete implementation details.**

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

**Status:** ~80% complete - Most public APIs have XML comments

**XML Comments Coverage:**
- [x] Core interfaces documented ✅ (IGraphRunner, IGraphEventRouter, IGraphState, IGraphNodeExecutor, IQuestManager, etc.)
- [x] Most public classes documented ✅ (266 XML comment matches across 85 files)
- [x] Public methods documented ✅ (extensive parameter/return documentation)
- [x] Generate API documentation ✅
- [ ] Verify 100% coverage ❌ **MISSING** - Need audit to ensure all public APIs are documented

**What's Done:**
- All major interfaces have comprehensive XML comments
- Executors, nodes, and core runtime classes documented
- Message types documented
- Quest system APIs fully documented

**What's Missing:**
- Complete audit to verify 100% coverage
- Doxygen configuration and generation

---

### 7.2 Tutorial Content

**Status:** ~40% complete - Example quest graphs exist, documentation is comprehensive

**Example Graphs:**
- [x] Simple linear quest ✅ (TC_Quest1 in TemplateGame)
- [ ] Branching quest with choices ❌ **MISSING**
- [x] Multi-task quest with progress ✅ (TC_Quest2 with Task1/Task2 in TemplateGame)
- [ ] Dialogue with choices ❌ **MISSING**
- [ ] Conditional dialogue ❌ **MISSING**

**Documentation (Partial):**
- [x] Comprehensive README.md with examples ✅
- [x] System-specific docs (Quest, Message Flow, Debugger, Architecture) ✅
- [x] Usage patterns and best practices ✅
- [ ] Step-by-step tutorials ❌ **MISSING**

**Tutorials to Write:**
- [ ] "Your First Quest" tutorial ❌ **MISSING**
- [ ] "Creating Branching Dialogues" tutorial ❌ **MISSING**
- [ ] "Advanced Quest Conditions" tutorial ❌ **MISSING**
- [ ] "Custom Node Types" tutorial ❌ **MISSING**

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

## Phase 9: General-Purpose Visual Scripting Foundation

**Goal:** Expand beyond quests/dialogue into general game logic scripting and enable systemic/emergent gameplay (RimWorld, Project Zomboid, Space Station 14-style)

**Critical for:** Reactive event-driven systems where multiple game systems interact to create emergent gameplay 

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

### 9.4 Systemic Gameplay Primitives

**Goal:** Enable reactive, event-driven systems that create emergent gameplay through system interactions

**Why This Matters:**
Games like RimWorld, Project Zomboid, and SS14 create emergent gameplay through systems reacting to each other. This phase provides the primitives needed for graphs to:
- React to events with calculations (not just comparisons)
- Transform data between systems
- Chain conditional logic for complex reactions
- Query and manipulate state across system boundaries

**Current Capability:** ✅ Event-driven architecture, ✅ MessagePipe integration, ✅ Conditional branching, ✅ State storage  
**Missing:** ❌ Math operations, ❌ Complex logic chains, ❌ Data transformation

**Priority Nodes for Systemic Gameplay:**

- [ ] **Math operations (CRITICAL for systemic gameplay)**
  - `AddNode`, `SubtractNode`, `MultiplyNode`, `DivideNode` - Basic arithmetic
  - `ClampNode`, `LerpNode` - Value manipulation
  - `RandomRangeNode` - Procedural variation
  - **Use Case:** Calculate damage based on weapon + stats, adjust crop growth rate, compute distance-based effects

- [ ] **Logic operations (CRITICAL for complex reactions)**
  - `AndNode`, `OrNode`, `NotNode` - Chain conditions
  - `AllTrueNode`, `AnyTrueNode` - Multi-condition checks
  - **Use Case:** "If health < 50% AND stamina < 25% AND not in safe zone → trigger panic behavior"

- [ ] **State query nodes (ENABLES cross-system queries)**
  - `GetStateNode<T>` - Read state from any graph by key
  - `SetStateNode<T>` - Write state to any graph
  - `HasStateNode` - Check if state key exists
  - `QueryStateNode` - Query multiple state values at once
  - **Use Case:** Weather system checks crop growth state, AI checks player inventory state

- [ ] **Data transformation nodes (ENABLES system communication)**
  - `MapValueNode` - Map value from one range to another
  - `CombineValuesNode` - Combine multiple values (e.g., Vector3 from x,y,z)
  - `ExtractValueNode` - Extract components (e.g., x from Vector3)
  - `FormatStringNode` - Build strings from values
  - **Use Case:** Convert temperature to crop growth multiplier, format status messages

- [ ] **Conditional math nodes (ENABLES reactive calculations)**
  - `ConditionalAddNode` - Add if condition true
  - `ConditionalMultiplyNode` - Multiply if condition true
  - `SelectNode` - Choose value based on condition (ternary operator)
  - **Use Case:** "If raining, multiply crop growth by 1.5, else use base rate"

**Systemic Gameplay Patterns Enabled:**

**Pattern 1: Reactive Calculations**
```
WeatherSystem emits RainStarted { intensity: 0.8 }
    ↓
CropGraph subscribes:
  MessageFieldGetNode("intensity", "rain_intensity")
  MultiplyNode(rain_intensity, 1.5) → "growth_multiplier"
  SetStateNode("growth_multiplier", growth_multiplier)
```

**Pattern 2: Complex Conditional Reactions**
```
PlayerMovement emits PlayerMoved { noise: 0.5, position: Vector3 }
    ↓
ZombieGraph subscribes:
  MessageFieldGetNode("noise", "player_noise")
  MessageFieldGetNode("position", "player_pos")
  AndNode(
    GreaterThanNode(player_noise, 0.3),
    DistanceCheckNode(player_pos, zombie_pos, 10.0)
  ) → [True] → Emit ZombieAlerted
```

**Pattern 3: Cross-System State Queries**
```
CropGraph:
  GetStateNode("weather_temperature") → "temp"
  GetStateNode("soil_quality") → "soil"
  MultiplyNode(temp, soil) → "growth_rate"
  SetStateNode("crop_growth_rate", growth_rate)
```

**Pattern 4: Emergent Chain Reactions**
```
CombatSystem emits EnemyDefeated { xp: 50, itemDrop: "Seed" }
    ↓
QuestGraph: Increment objective
    ↓
QuestGraph emits QuestComplete
    ↓
InventoryGraph: AddItem(Seed)
    ↓
InventoryGraph emits ItemAdded { itemId: "Seed" }
    ↓
CropGraph: 
  GetStateNode("has_seeds") → check
  AndNode(check, GetStateNode("has_soil")) → can_plant
  [can_plant] → EnableCropPlanting
```

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Core/Math/*.cs` (~15 nodes)
- `Runtime/VisualGraphs/Authoring/Nodes/Core/Logic/*.cs` (~5 nodes)
- `Runtime/VisualGraphs/Authoring/Nodes/Core/State/*.cs` (~4 nodes)
- `Runtime/VisualGraphs/Authoring/Nodes/Core/Transform/*.cs` (~5 nodes)
- `Runtime/VisualGraphs/Executors/Math/*.cs` (~15 executors)
- `Runtime/VisualGraphs/Executors/Logic/*.cs` (~5 executors)
- `Runtime/VisualGraphs/Executors/State/*.cs` (~4 executors)
- `Runtime/VisualGraphs/Executors/Transform/*.cs` (~5 executors)

**Total:** ~58 new files (nodes + executors)

**Note:** This section focuses on the MINIMUM needed for systemic gameplay. Full visual scripting (loops, functions, etc.) comes in Phase 11, but these primitives unlock RimWorld/Zomboid-style emergent gameplay NOW.

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

## Phase 12: Complex Game Systems (SS14-Level Systems)

**Goal:** Support interconnected game systems like Space Station 14 using plugin architecture

**Note:** This phase focuses on **system complexity** (inventory, roles, atmospherics, etc.), NOT multiplayer. For multiplayer support, see Phase 16 (Stretch Goal).

**Prerequisites:** Phase 9 (especially 9.4 Systemic Gameplay Primitives) must be complete for reactive system interactions

**Note:** This uses **traditional Unity GameObjects and MToolKit plugins**, NOT Unity ECS/DOTS.

**Builds on:** Phase 9.4 provides the math/logic primitives needed for complex system interactions. Phase 12 adds game-specific system nodes (inventory, roles, atmospherics, etc.) that USE those primitives.

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

### 12.7 AI/Behavior Nodes

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

## Phase 15: Community & Ecosystem (Stretch Goal)

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

## Phase 16: Multiplayer/Networking Support (Stretch Goal)

**Goal:** Enable multiplayer games with network-synced graph state

**Status:** Future stretch goal - Networking infrastructure coming but not yet implemented

**Prerequisites:** Phase 9 (Systemic Gameplay Primitives) should be complete for reactive system interactions in multiplayer context

- [ ] Network-synced graph state
  - Sync graph state across network
  - Handle state conflicts
  - Optimize state updates (delta compression)

- [ ] Per-player graph instances
  - Separate graph state per player
  - Player-specific quest/dialogue progress
  - Privacy boundaries

- [ ] Shared world graph state
  - World-level graph state (e.g., weather, time of day)
  - Authority model for world state
  - Conflict resolution for concurrent modifications

- [ ] Authority/ownership model
  - Server authority for critical state
  - Client prediction for responsive gameplay
  - Ownership transfer

- [ ] State replication and interpolation
  - Efficient state synchronization
  - Interpolation for smooth updates
  - Bandwidth optimization

- [ ] Network nodes
  - `IsServerNode` - Check if running as server
  - `IsClientNode` - Check if running as client
  - `GetLocalPlayerNode` - Get local player reference
  - `GetPlayerCountNode` - Number of connected players
  - `SyncVariableNode` - Mark variable for network sync
  - `IsOwnerNode` - Check if local player owns object
  - `HasAuthorityNode` - Check if can modify object
  - `TransferAuthorityNode` - Transfer ownership
  - `CallRPCNode` - Call method on server/client
  - `BroadcastEventNode` - Send event to all players
  - `SendToPlayerNode` - Send to specific player

**Note:** Requires integration with Unity Netcode/Mirror/etc. This is a stretch goal for future multiplayer support.

**Files to Create:**
- `Runtime/VisualGraphs/Authoring/Nodes/Network/*.cs` (~10 nodes)
- `Runtime/VisualGraphs/Executors/Network/*.cs` (~10 executors)
- `Runtime/VisualGraphs/Networking/NetworkGraphStateSync.cs`
- `Runtime/VisualGraphs/Networking/NetworkAuthorityManager.cs`

---

## Long-Term Vision

### Version 1.0: First-Class Visual Scripting Engine

**Final Product:** A first-class visual scripting engine inside MToolKit used for almost all high-level game logic, with C# pushed down into services and "mechanics," not orchestration.

#### 1. Authoring Experience

**Dedicated VisualGraphs Editor:**
- Create Quest, Dialogue, Tutorial, and generic Logic graphs
- Drag from a **large node palette**: events, math, comparisons, timers, state, Unity API, inventory, combat, AI, UI, etc.
- **Type-colored data pins** and white flow pins, with automatic type checking and conversions
- **Authoring tools**: validation, graph analysis, "go to message type," search, minimap, grouping, bookmarks

**Debugger Layer:**
- Play mode shows active nodes pulsing, wires animating when they fire, recent execution history
- Breakpoints on nodes, step/continue, and a watch window for graph state
- Runtime state inspector window for editing variables on the fly and firing test events

#### 2. Runtime Model

**Graph Execution:**
- All graphs export to **DTOs with versions**, no xNode or editor code in runtime
- **GraphRunner + GraphEventRouter** drive execution:
  - O(1) routing by `(messageType, domain)` with sequence IDs and per-graph execution caps
  - Parallel execution for independent graphs where safe

**State Management:**
- Graph state stored per graph instance, with clear separation of global, per-quest, per-player, and per-world variables
- Fully integrated with the save system via a dedicated save domain; versioned snapshots with migration hooks

**Message Bus:**
- Every system talks through MessagePipe with strongly typed `IGameMessage`s
- VisualGraphs subscribes based on explicit graph-level subscriptions and emits events out
- All major subsystems expose a message surface: Inventory, Combat, Dialogue, UI, Tutorial, World/Environment, AI

#### 3. Node Library

**Core, Stable Node Set:**

**Events:**
- On message, timers, triggers, input, area/zone entry, health changes, item pickup, enemy defeat, quest lifecycle, dialogue lifecycle, tutorial steps

**Logic and Math:**
- Branch, switch, loops, arithmetic, comparisons, boolean logic, randoms, value mapping, interpolation

**State:**
- Get/Set/TryGet state, collections, generic key/value storage, graph-local and global

**Unity Integration:**
- GameObject/Transform/Prefab/Animator/Audio/UI nodes sufficient to build non-trivial interactions without dropping to C#

**Domain Nodes:**
- Quest nodes for starting, advancing, checking, claiming, querying
- Dialogue nodes that call `IDialogueUIService` and branch on choices
- Inventory, crafting, roles/permissions, interaction, AI, environment – thin wrappers over services, not reimplementations

**Custom Nodes:**
- Trivial to add: one C# class with attributes generates the node and executor, registered automatically by the plugin

#### 4. Systemic Gameplay

**Reactive Systems (SS14/RimWorld-style):**
- Weather affecting crops and NPC behavior
- Power system affecting doors, lights, and life support
- Faction reputation reacting to quests, kills, theft, dialogue choices

**Cross-Plugin Orchestration:**
- Most systemic gameplay built as orchestration graphs
- Subscribe to Combat/Inventory/Environment/AI messages, manipulate state, emit new events for other plugins

**C# Code Remains For:**
- Mechanics (movement, physics, combat resolution)
- Data models and invariants (inventory data, quest definitions)
- Services with non-trivial algorithms or perf constraints

**Graphs Handle:**
- The "when X happens, under conditions Y, do Z and emit W" layer

#### 5. MToolKit Integration

**Plugin Architecture:**
- VisualGraphs is just another **MToolKit plugin**, configured through `VisualGraphConfig` and `VisualGraphRegistry`
- Stable public API surface (interfaces, messages, service accessors)
- Full test coverage for exporter, runtime core, integrations, and save/load
- Benchmarks and stress tests verifying it behaves under load

**Default Use Cases:**
- Quests and campaigns
- Dialogue trees and conditional conversations
- Tutorials, hints, and onboarding
- Dynamic world scripting (events, modifiers, seasonal behavior)
- UI flows tied to game state
- One-off content logic that designers or future-you can tweak visually

**That's the "final product":** A mature, tested visual orchestration layer baked into MToolKit, handling most game logic as graphs over a unified message bus, with C# services beneath it and enough tooling that you can trust it in production without thinking about the internals every time.

---

### Ultimate Goal: "Blueprint-like from UE5"

The VisualGraphs system should eventually:

1. ✅ **Replace C# for orchestration/policy logic** - Designers create high-level gameplay behavior (quests, dialogue, tutorials, cross-system coordination) without programming. Low-level mechanics (physics, rendering, tight loops) remain in C#.
2. ✅ **Support complex systems** - Handle SS14-level complexity (roles, inventory, crafting, atmospherics) through orchestration graphs that coordinate domain services
3. ✅ **Performance competitive with code** - JIT compilation makes graphs as fast as C# for orchestration workloads
4. ✅ **Professional tooling** - Editor UX matches Unreal Blueprints
5. ✅ **Extensible** - Easy to add custom nodes for project-specific logic
6. ✅ **Debuggable** - Breakpoints, watch windows, visual execution flow
7. ✅ **Type-safe** - Compile-time type checking prevents runtime errors
8. ✅ **Multiplayer-ready** - State replication and authority built-in
9. ✅ **Community-driven** - Node marketplace and package ecosystem

**Architectural Principle:** Graphs are for **orchestration and policy** (the "what happens when" layer), not low-level mechanics. See `README.md` "When to Use Graphs vs Code" section for the three-layer architecture pattern.

### Reference Games Built with Similar Systems

- **Space Station 14** - Complex multiplayer systems with ECS
- **RimWorld** - Complex story driven simulator
- **Dwarf Fortress** - Systems master class
- **Project Zomboid** - 
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

## Phased Rollout Strategy

### Version 0.5 (Current - Mostly Complete)
- ✅ Quest and Dialogue graphs
- ✅ Full MessagePipe event routing (bidirectional)
- ✅ xNode authoring
- ✅ Production-ready quest system with Quest Manager
- ✅ Full MToolKit plugin integration
- ✅ Message-based reward pattern
- ✅ Save system integration (Phase 1.2 - complete!)
- ⚠️ Dialogue UI service (Phase 3.1 - remaining)
- ❌ Test coverage (Phase 5 - remaining)

### Version 0.6 (Phase 1-2 + 3.1 Complete)
- Production-ready quest system with task tracking
- Full MToolKit integration
- Save system working (Phase 1.2)
- Dialogue UI service implemented (Phase 3.1)
- Test coverage 30%+

### Version 0.7 (Phase 9-10 Complete)
- General-purpose visual scripting
- Core programming constructs (variables, flow control, math)
- Unity integration nodes (GameObject, Transform, Physics)
- Type system with validation
- Test coverage 60%+

### Version 0.8 (Phase 11-12 Complete)
- Functions and subroutines
- Complex execution models (coroutines, parallel)
- Plugin-based game systems (inventory, crafting, roles, AI, atmospherics)
- "Can build SS14-level systems" milestone (using traditional GameObjects + plugins, single-player)
- Test coverage 90%+

### Version 0.9 (Phase 13-15 Complete)
- Blueprint-quality editor
- JIT compilation and performance
- Node marketplace and ecosystem
- "Blueprint-like from UE5" milestone achieved
- Test coverage 100%+

### Version 1.0 (Complete Vision - See "Version 1.0: First-Class Visual Scripting Engine" above)
- All features from Version 0.9 complete
- Full authoring experience with dedicated editor and debugger
- Complete node library (events, logic, math, state, Unity, domain nodes)
- Systemic gameplay support (SS14/RimWorld-style reactive systems)
- Production-ready with full test coverage, benchmarks, and stress tests
- **Optional:** Multiplayer/Networking support (Phase 16 - Stretch Goal)
  - Network-synced graph state
  - Authority/ownership model
  - "Full SS14 multiplayer capability" milestone

---

## Summary of Critical Path

**Must-Have for Production (Phase 1-2):**
1. ✅ **Type-based subscriptions** - DONE (1.0.2)
2. ✅ **MessagePipe architecture** - DONE (direct IGameMessage integration)
3. ✅ **Asset reference system** - DONE (1.0.1)
4. ✅ **Per-graph execution limits** - DONE (1.0.3)
5. ✅ **Addressables loading** - DONE (1.0.4)
6. ✅ **MessagePipe implementation** - DONE (1.3 - bidirectional pub/sub working!)
7. ✅ **Plugin architecture integration** - DONE (1.1 - full lifecycle + config!)
8. ✅ **Quest progress tracking + Quest Manager** - DONE (2.1 - full orchestration!)
9. ✅ **Save system integration** - DONE (Phase 1.2 - complete!)
10. ⚠️ **Dialogue UI service implementation** - TODO (Phase 3.1)
11. ❌ **Core test coverage (80%+)** - TODO (Phase 5)

**Nice-to-Have (Phase 3-4):**
- Quest conditions & rewards
- Advanced dialogue features
- Editor tools
- Asset reference validation UI

**Future Enhancements (Phase 5+):**
- Graph versioning (Phase 8)
- Visual Scripting integration (Phase 9+)
- Multiplayer support (Phase 16 - Stretch Goal)

---

## Success Criteria

### ✅ Phase 1.0 Complete! All 4 Core Architecture Tasks Done ✓

- [x] **Type-based subscriptions** - DONE (1.0.2)
- [x] **MessagePipe architecture** - DONE (interfaces use IGameMessage)
- [x] **Asset reference system** - DONE (1.0.1)
- [x] **Per-graph execution limits** - DONE (1.0.3)
- [x] **Addressables loading** - DONE (1.0.4)

### ✅ Phase 1.3 Complete! MessagePipe Integration Working ✓

- [x] **Graphs receive events from MessagePipe** - DONE (EventBusBridge subscribes dynamically)
- [x] **Graphs emit events to MessagePipe** - DONE (SimpleEventEmitter publishes)
- [x] **Type-safe message routing** - DONE (uses reflection for concrete types)
- [x] **Works with existing messages** - DONE (SceneLoadedMessage, NavigationRequestMessage, etc.)

### ✅ Phase 1 Complete! All Integration Tasks Done:
- [x] Graphs save/load properly with game saves (1.2) ✅ - **COMPLETE**
- [x] Graphs receive events from MessagePipe ✅ (1.3)
- [x] Graphs emit events to MessagePipe ✅ (1.3)
- [x] Plugin appears in PluginRegistry ✅ (1.1)
- [x] Config asset controls system behavior ✅ (1.1)

### ✅ Phase 2.1 Complete! Phase 2 Status:
- [x] Quests track objective progress (X/Y complete) ✅ - Quest Manager implemented
- [x] Can display quest progress in UI ✅ - Progress messages emitted, UI subscribes
- [x] Task completion triggers events ✅ - Full lifecycle messages

**Framework Support:**
- [x] Quest conditions support ✅ → Framework provides generic state nodes (Phase 2.2: GenericStateSetNode, GenericStateCheckNode, GenericStateGetNode) and message field checks. Games implement their own condition logic using these tools.
- [x] Quest rewards support ✅ → Framework provides `QuestClaimedMessage` emission (Phase 2.3). Games subscribe to handle rewards based on their own `QuestDefinition` data.

### ✅ System is Production-Ready When:
- [ ] 100% test coverage for core systems
- [ ] All integration TODOs removed
- [x] ~~Meta GUID system implemented and validated~~ ✅ Done via Phase 1.0.1 (Unity's AssetReference)
- [ ] Documentation complete
- [ ] No known critical bugs
- [ ] Performance targets met (1000+ nodes/sec)

---

## Risk Areas

1. ~~**Asset Reference Migration**~~ ✅ **Mitigated** - Using Unity's native `AssetReference` from the start
   
2. **Save System Compatibility** - Changes to state format may break saves
   - Mitigation: Version save data, provide migration path
   
3. **Performance** - Large graphs may execute slowly
   - Mitigation: Profile early, optimize hot paths, consider parallel execution
   
4. **xNode Dependency** - Reliance on external package
   - Mitigation: xNode is authoring-only, runtime has no dependency

---

## Event-Based Entry System Hardening

**Current Status:** Event-based entry system is production-ready but has known limitations that should be addressed as the system scales.

### Known Limitations & Risks

#### 1. Sequential Execution (No Parallelism)
- **Issue:** All matching graphs execute sequentially in `RouteAsync`
- **Impact:** Latency spikes with many graphs or slow executors
- **Mitigation:** Consider parallel execution option for independent graphs
- **Priority:** Medium (only matters at scale)

#### 2. No Execution Order Guarantee
- **Issue:** Graphs execute in registration order, not priority
- **Impact:** Race conditions possible if order matters
- **Mitigation:** Add priority/weighting system for graph execution
- **Priority:** Low (most games don't need this)

#### 3. Error Isolation Trade-offs
- **Issue:** Errors in one graph don't stop others (good for resilience, but partial failures can leave inconsistent state)
- **Impact:** Hard to detect "some graphs failed" scenarios
- **Mitigation:** Add execution metrics/telemetry to track failures
- **Priority:** Medium (important for debugging)

#### 4. No Cancellation Propagation
- **Issue:** If one graph is slow/hanging, others wait
- **Impact:** One bad graph can block all event processing
- **Mitigation:** Add timeout per graph execution, better cancellation handling
- **Priority:** Medium (important for production stability)

#### 5. Reflection Overhead
- **Issue:** EventBusBridge uses reflection for dynamic subscription
- **Impact:** Minimal (one-time setup), but adds complexity
- **Mitigation:** Consider code generation for hot paths (future optimization)
- **Priority:** Low (already optimized with caching)

#### 6. Runtime Type Safety
- **Issue:** `MessageTypeReference` validated at authoring, but reflection happens at runtime
- **Impact:** Runtime failures instead of compile-time errors if message types are renamed/deleted
- **Mitigation:** Add export-time validation that message types exist
- **Priority:** High (catches errors earlier)

#### 7. Debugging Complexity
- **Issue:** Hard to trace which graphs handle which events
- **Impact:** Difficult to debug event routing issues
- **Mitigation:** Add visual debugging tools, execution flow visualization (Phase 13)
- **Priority:** Medium (improves developer experience)

#### 8. Domain Matching Complexity
- **Issue:** Wildcard (empty domain) vs exact match logic can be confusing
- **Impact:** Subtle bugs if domain filtering is misunderstood
- **Mitigation:** Better documentation, validation warnings
- **Priority:** Low (documentation issue)

#### 9. Missing Message Type Validation
- **Issue:** If `MessageTypeReference` points to non-existent type, subscription fails silently
- **Impact:** Runtime failures that could be caught earlier
- **Mitigation:** Add export-time validation (same as #6)
- **Priority:** High (catches errors earlier)

#### 10. Performance with Many Graphs
- **Issue:** O(n) iteration through all matching graphs per event
- **Impact:** Performance degrades linearly with graph count
- **Mitigation:** Profile and optimize hot paths, consider batching
- **Priority:** Low (only matters at very large scale)

### Recommended Hardening Tasks

**High Priority:**
- [ ] Add export-time validation that message types exist (prevents runtime failures)
- [ ] Add execution metrics/telemetry (track failures, performance)

**Medium Priority:**
- [ ] Add timeout per graph execution (prevent hanging)
- [ ] Consider parallel execution option for independent graphs
- [ ] Add visual debugging tools (Phase 13 covers this)

**Low Priority:**
- [ ] Add priority/weighting system for graph execution
- [ ] Better domain matching documentation/validation
- [ ] Code generation for hot paths (future optimization)

**Already Mitigated:**
- ✅ Error handling (try/catch per graph)
- ✅ Cancellation token support
- ✅ O(1) lookup by (type, domain)
- ✅ Proper disposal pattern

---

## Architectural & Systemic Risks

**Current Status:** These risks emerge as the system scales and content accumulates. They require ongoing discipline and tooling to mitigate.

### Known Risks & Mitigations

#### 1. Cross-Graph Coupling on Shared Events ⚠️
- **Issue:** Multiple graphs can listen to the same message. If any of them start implicitly depending on others' side effects or state (even subtly), you get order-dependent bugs with no guaranteed execution order.
- **Impact:** Subtle, hard-to-reproduce bugs that depend on graph registration order
- **Mitigation:** 
  - Document that graphs should be independent and not rely on execution order
  - Add tooling to detect potential coupling (graph A modifies state that graph B reads)
  - Consider explicit dependencies/ordering if needed (but avoid if possible)
- **Priority:** High (can cause production bugs)

#### 2. Global Behavior Surface is Hard to See ⚠️
- **Issue:** Per-graph subscriptions are explicit, but "which graphs react to `XMessage`?" is still global. Without an index/trace, the combined behavior of all graphs on a core event is hard to reason about and audit.
- **Impact:** Difficult to understand system-wide behavior, hard to audit what happens when an event fires
- **Mitigation:**
  - Add editor tooling: "Find all graphs subscribing to MessageType X"
  - Add runtime debugging: "Show all graphs that handled this event"
  - Consider a "message impact analysis" tool that shows the full graph of reactions
- **Priority:** Medium (important for maintainability and debugging)

#### 3. Save/Load Lifecycle Ordering ⚠️ **HARDENING NEEDED** (Implementation Complete, Hardening Pending)

**Status:** ✅ **Implementation is complete and working** - Save/load functionality is functional. These are hardening tasks to prevent edge cases.

**Current Implementation:**
- ✅ Quest state restores first (line 169-170 in GraphStateSaveController)
- ✅ Graph states restore after quest graphs are loaded
- ✅ Late registration handling (save system loads before plugin)
- ✅ Proper cancellation token support

**Potential Edge Cases (Not Yet Hardened):**
- **Issue:** If events fire during load/restore, could cause double triggers or missed triggers
- **Impact:** 
  - Potential duplicate events during restoration
  - Events might fire before state is fully restored
  - Edge cases with complex graph state
- **Hardening Tasks (Not Blocking):**
  - [ ] Define clear save/load phases in `SaveSystemCoordinator` (if not already defined)
  - [ ] Add validation to detect out-of-order restoration
  - [ ] Consider a "restore mode" that suppresses events until fully loaded
  - [ ] **Test save/load extensively with complex graph state** (Phase 5 - Testing)
- **Priority:** **Medium** - Implementation works, but hardening would make it more robust. Not blocking production use.

#### 4. Message Schema / Taxonomy Drift 🔴 **CRITICAL**

**Goal:** Messages are a stable API. You evolve them, you don't fork them into chaos.

- **Issue:** If you evolve or fork message types (`ItemAcquired` vs `ItemPickedUp`, new enum values, renamed fields), existing graphs can silently become wrong: filters no longer match, or they match more/less than intended.
- **Impact:** 
  - **Silent failures** - Graphs stop working but no obvious error
  - Existing content breaks after refactoring
  - Hard to detect until players hit the bug
  - Can affect many graphs at once (cascading failures)
  - **Production bugs that only appear after message refactoring**

**Hardening Rules:**

1. **Single Source of Truth for Messages**
   - All `IGameMessage` types live in a dedicated assembly/namespace, owned by the framework:
     - `MToolKit.Runtime.Messages.*`
     - Domain subnamespaces: `*.Inventory`, `*.Quest`, `*.World`, etc.
   - Nobody defines ad-hoc messages in game code without going through that layer.

2. **No Synonyms. No "Helper" Messages.**
   - One canonical event per semantic thing:
     - `ItemAcquiredMessage` ✅
     - Not `ItemPickedUpMessage`, `ItemGrantedMessage`, `ItemGivenToPlayerMessage` ❌
   - If you need extra nuance, extend the payload, don't add a new message:
     ```csharp
     public sealed class ItemAcquiredMessage : IGameMessage
     {
         public ItemId ItemId { get; }
         public EItemAcquiredReason Reason { get; }  // Pickup, Craft, Reward, Scripted
         public int Quantity { get; }
         public EntityId? SourceEntity { get; }
     }
     ```

3. **Version by Data, Not by Type Name**
   - Don't do `ItemAcquiredV2Message` ❌
   - Evolve fields:
     - Add new fields ✅
     - Add new enum values ✅
     - Deprecate old ones with `[Obsolete]` if absolutely necessary ✅

4. **Export-Time Validation of Graph Subscriptions** (Must-Have)
   - In `XNodeGraphExporter.Validate`:
     - For each `MessageSubscription`:
       - Verify the `MessageType` exists and implements `IGameMessage`
       - **Fail export if not**
     - For `MessageFieldCheckNode` / `MessageFieldGetNode`:
       - Resolve the selected `FieldName` against the actual `MessageType` using reflection
       - If missing or type mismatch: **fail export with a loud error**
   - That way, changing `EnemyDefeatedMessage` breaks at export time, not silently at runtime.

5. **Central Registry for Message Metadata** (Recommended)
   - A static registry:
     ```csharp
     public static class GameMessageRegistry
     {
         public static readonly IReadOnlyList<Type> AllMessageTypes = new[]
         {
             typeof(ItemAcquiredMessage),
             typeof(ItemCraftedMessage),
             typeof(EnemyDefeatedMessage),
             typeof(QuestStartedMessage),
             // ...
         };
     }
     ```
   - Exporter can validate that all subscribed message types are in this list
   - Any new `IGameMessage` must be registered here or tests fail

6. **Tests for Schema Lock**
   - Add a test suite that:
     - Asserts all `IGameMessage` types live in the expected assembly/namespace
     - Asserts all `MessageSubscription.MessageType` used in sample graphs exist and are in the registry
   - When you change a message, tests break and force you to fix graphs or registry

**Implementation Tasks:**
- [ ] Create `MToolKit.Runtime.Messages` namespace structure
- [ ] Implement export-time validation in `XNodeGraphExporter` for message types and fields
- [ ] Create `GameMessageRegistry` with all message types
- [ ] Add schema lock tests
- [ ] Document message versioning strategy (evolve payloads, not types)
- [ ] Add CI/CD validation step for graph exports

- **Priority:** **Critical** - Silent failures are the worst kind. This can break production content without warning.

#### 5. Content Sprawl and Dead Graphs ⚠️
- **Issue:** Over time you accumulate graphs:
  - No longer referenced (dead content)
  - Overlapping responsibility on the same events
  - That raises maintenance cost and makes "what's actually active?" unclear unless you enforce references/cleanup.
- **Impact:** Maintenance burden, confusion about what's actually running, potential conflicts
- **Mitigation:**
  - Add tooling to detect unreferenced graphs (not in registry, not loaded)
  - Add analysis tool: "Which graphs subscribe to the same events?"
  - Consider graph lifecycle management (mark as deprecated, archive old versions)
  - Document graph ownership and cleanup process
- **Priority:** Medium (important for long-term maintainability)

#### 6. Graphs Encoding Invariants That Belong in Services 🔴 **CRITICAL**

**Goal:** Graphs orchestrate. Services own invariants and complex rules.

- **Issue:** The architecture encourages orchestration, but it's easy to start pushing business rules (quest validity, inventory constraints, etc.) into graphs instead of services. That makes invariants harder to test and reuse.
- **Impact:** 
  - Business logic scattered across graphs instead of centralized services
  - Hard to test invariants (can't unit test graph logic easily)
  - Logic duplication across multiple graphs
  - Violations of single source of truth principle

**Hard Line Definition:**

- **Invariants** = rules that must hold system-wide, regardless of how you call into the system:
  - Inventory capacity
  - Item stack limits
  - Quest completion criteria validity
  - Dialogue availability conditions
  - Save/load integrity rules

Those must live in services, not in graphs. **Graphs can assume invariants. They cannot define them.**

**Hardening Rules:**

1. **No Direct Writes to Core Domain State from Graphs**
   - Graph executors should not be writing directly into domain models:
     - No `inventory.Items.Add(...)` inside an executor ❌
     - No `quest.Status = Completed` inside an executor ❌
   - They should call into services:
     ```csharp
     public sealed class CompleteQuestNodeExecutor : IGraphNodeExecutor
     {
         readonly IQuestManager _questManager;
         
         public CompleteQuestNodeExecutor(IQuestManager questManager) 
         { 
             _questManager = questManager; 
         }
         
         public async UniTask ExecuteAsync(...)
         {
             var questId = node.Parameters["QuestId"] as Guid;
             await _questManager.CompleteQuestAsync(questId, ct);
             // QuestManager enforces invariants
         }
     }
     ```

2. **All "Mutating Nodes" are Thin Adapters Over Services**
   - For each domain:
     - Inventory nodes → `IInventoryService`
     - Crafting nodes → `ICraftingService`
     - Quest nodes → `IQuestManager`
     - Dialogue nodes → `IDialogueUIService` / `IDialogueService`
   - Any invariant must be enforced in those services, written once, tested there

3. **Graphs Only Combine Decisions, They Don't Define Hard Rules**

   **Example of Wrong vs Right:**

   - **Wrong (graph owns invariant):**
     - Graph: "If inventory is full, drop item on ground instead of adding"
     - Service: blindly adds to inventory ❌

   - **Right:**
     ```csharp
     public interface IInventoryService
     {
         bool CanAdd(ItemId id, int qty);
         InventoryAddResult TryAdd(ItemId id, int qty); // result contains overflow, dropped, etc.
     }
     ```
     - Graph:
       - Calls `TryAdd`
       - Branches on the result to decide what *extra* to do:
         - Show a popup
         - Trigger a tutorial
         - Start a quest about backpack upgrades

4. **Promote Logic from Graphs When It Smells Invariant-ish**

   When you find yourself writing the same pattern in graphs:
   - "If Quest A is complete and Quest B is not started and player has Item X, then allow Y"

   Treat that as a symptom that you need a domain-level predicate:
   ```csharp
   public interface IQuestService
   {
       bool CanStartQuest(QuestId questId);
       bool IsQuestChainUnlocked(QuestChainId chainId);
   }
   ```

   Then add a small node:
   ```csharp
   public sealed class QuestConditionNodeExecutor : IGraphNodeExecutor
   {
       readonly IQuestService _questService;
       
       public async UniTask ExecuteAsync(...)
       {
           var questId = (QuestId)node.Parameters["QuestId"];
           var canStart = _questService.CanStartQuest(questId);
           context.EnqueueNext(canStart ? "OnTrue" : "OnFalse");
       }
   }
   ```

   Now the invariant lives in code, and the graph simply branches on it.

5. **Review Rule: Any Node That Changes "Real World" State is Suspect**

   At code review level:
   - Flag any executor that:
     - Writes directly to domain objects ❌
     - Uses ES3 / save system directly ❌
     - Talks to Unity world in a way that bypasses a known service ❌
   - Those should almost always be rewritten as calls into an existing service, or force you to factor a service out first

6. **Testing Focus is on Services, Not on Graphs**

   - Unit tests must:
     - Hit `IInventoryService`, `IQuestManager`, `ICraftingService`, etc.
     - Assert invariants there
   - Graph tests:
     - Only need to confirm "wiring":
       - Given messages and service mocks, the right service methods are called and the expected graph paths fire
   - If you find yourself wanting "tests to assert invariant X via a graph," that invariant is in the wrong place

**Implementation Tasks:**
- [ ] Document architectural principle: "Graphs orchestrate, services enforce invariants" (in README)
- [ ] Create code review checklist: "Does this executor write directly to domain state?"
- [ ] Audit all existing executors for direct domain writes
- [ ] Create examples showing correct patterns (service validates, graph reacts)
- [ ] Add linting/validation to detect common anti-patterns (complex validation logic in graphs)
- [ ] Ensure all domain services expose needed APIs so graphs don't need to reimplement logic

**Short Version:**
- Graphs never own correctness; they only orchestrate services that do
- Any rule that must always hold goes into a service API and gets tested there
- Nodes are thin adaptors over services, not reimplementations of business logic

- **Priority:** **Critical** - This is the biggest architectural risk. Once violated, it's hard to fix.

#### 7. Performance Under Broad, Frequent Events ⚠️
- **Issue:** If you ever introduce high-frequency or overly generic events with many subscribers and weak filtering, routing plus executor overhead can become noticeable. It's controllable, but only if you stay disciplined about what gets put on the bus.
- **Impact:** Performance degradation with high-frequency events or many subscribers
- **Mitigation:**
  - **Discipline:** Keep events specific, not overly generic
  - **Filtering:** Use domain filters and message field checks to narrow subscriptions
  - **Profiling:** Add performance metrics to detect hot paths
  - **Documentation:** Guidelines on what should/shouldn't be an event
  - **Consider:** Event batching or throttling for high-frequency events
- **Priority:** Medium (only matters if discipline breaks down)

#### 8. Graph Complexity / Spaghetti Graphs 🔴 **CRITICAL**

**Goal:** Prevent graphs from becoming unreadable "worse code with wires" that defeats the purpose of visual orchestration.

- **Issue:** Graphs will accrete logic until they're unreadable and effectively "worse code with wires." That kills the whole point of visual orchestration.
- **Impact:**
  - No one can reason about behavior from the graph
  - Changes become dangerous
  - You're incentivized to bypass the system and "just write C#"
- **Mitigation Rules:**
  - **Hard cap node count / depth per graph**
  - **Enforce composition:** Small graphs and function/subgraph calls instead of megagraphs
  - **Lint for complexity:** Fan-in/fan-out, depth, node count

**Implementation Tasks:**
- [ ] Define complexity budget: e.g. "≤ N nodes, ≤ D depth, ≤ F outgoing branches per node"
- [ ] Implement `GraphComplexityAnalyzer` (editor utility):
  - Compute node count, depth, average branching factor, number of entry points
  - Tag graphs with "simple / medium / complex / forbidden" category
- [ ] Add editor warnings for graphs above threshold
- [ ] Add docs section: "When to split a graph / move logic to C# / move to function graphs"

- **Priority:** **Critical** - If graphs become unreadable, the system loses its value.

#### 9. Generic State Turning Into Global God Table 🔴 **CRITICAL**

**Goal:** Prevent generic state from becoming an unmaintainable global key/value dumping ground.

- **Issue:** The planned `GenericStateSet/Get` and generic state nodes can silently become a global key/value dumping ground used by every graph.
- **Impact:**
  - Hidden coupling between unrelated graphs and domains
  - Impossible to know who owns which piece of state
  - Bugs that depend on obscure key naming collisions
- **Mitigation Rules:**
  - **Namespaced keys** (`domain.subsystem.key`), never bare strings
  - **Separate state containers per domain** (quests, world, player, etc.)
  - **No cross-domain state writes** without going through a service

**Implementation Tasks:**
- [ ] Introduce `StateKey` value object:
  ```csharp
  public readonly struct StateKey {
      public string Domain { get; }
      public string Name { get; }
  }
  ```
- [ ] Replace raw string keys in state APIs with `StateKey`
- [ ] Enforce domain scoping: `IStateService` takes a domain or is injected per domain
- [ ] Add validation: forbid writes to "foreign" domains unless explicitly allowed
- [ ] Add analyzer listing all used `StateKey`s per graph and per domain

- **Priority:** **Critical** - Global state is a maintenance nightmare. Must be prevented from the start.

#### 10. xNode / Authoring Layer Fragility ⚠️

**Goal:** Treat DTO schema as the stable boundary, not xNode, to prevent editor changes from breaking content.

- **Issue:** xNode is authoring-only, but still a single point of failure for content: package updates, Unity API changes, or your own forks can corrupt graphs or break export.
- **Impact:**
  - Editor upgrades break graph editing/export
  - Subtle serialization changes break GUIDs or connections
  - You get stuck on a specific xNode/Unity version
- **Mitigation Rules:**
  - **Treat DTO schema as the stable boundary, not xNode**
  - **Keep exporter and DTOs decoupled from xNode internals**
  - **Have regression tests** that assert exporter behavior independent of editor

**Implementation Tasks:**
- [ ] Define explicit "Authoring Adapter Layer":
  - `IAuthoringGraphAdapter`, `IAuthoringNodeAdapter`, `IAuthoringPortAdapter`
  - xNode implementation lives behind this interface
- [ ] Refactor exporter to depend only on adapter interfaces, not xNode types
- [ ] Add tests that feed synthetic adapter graphs into exporter (no xNode dependency)
- [ ] Pin tested versions of xNode + Unity in `README` + CI config
- [ ] Add a "migration checklist" for upgrading xNode / Unity (run exporter tests, validate sample graphs)

- **Priority:** High - Prevents being locked to specific versions and protects content from editor changes.

#### 11. Asset / Addressables Integrity 🔴 **CRITICAL**

**Goal:** Ensure all graph-referenced assets are available at runtime, with clear failure modes.

- **Issue:** Graphs reference `QuestDefinition`, dialogue assets, prefabs, etc. via `AssetReference`. Addressables config changes can make those references fail at runtime or in some build variants.
- **Impact:**
  - Graphs silently fail because referenced assets aren't in any Addressables group or build
  - Behavior differs between "play mode in editor" and player builds
  - Hard to diagnose missing content vs logic bugs
- **Mitigation Rules:**
  - **Build-time validation** that all graph-referenced assets are in an Addressables group in the current build profile
  - **Explicit failure mode** when an asset can't be resolved: no silent nulls

**Implementation Tasks:**
- [ ] Implement `VisualGraphsBuildValidator` running in `IPreprocessBuildWithReport`:
  - Walk all `RuntimeGraphDefinition`s used in current build
  - Collect all `AssetReference`s
  - Ask Addressables API if each is included in build
  - **Fail build with a clear report if any are missing**
- [ ] In runtime loader:
  - **Fail fast with a clear error + log** when an `AssetReference` fails to load for a graph
  - Optionally emit a `GraphAssetMissingMessage` for debugging hooks
- [ ] Add editor window "Graph Asset Report" listing:
  - Each graph → referenced assets → Addressables group / status

- **Priority:** **Critical** - Silent failures at runtime are unacceptable. Must catch at build time.

#### 12. Plugin / Service Availability and Load Order 🔴 **CRITICAL**

**Goal:** Ensure graphs fail fast if required services/plugins are missing, rather than silently failing at runtime.

- **Issue:** Graph executors assume certain services/plugins exist (`IQuestManager`, `IInventoryService`, `IDialogueUIService`, etc.). If plugin load order or configuration changes, graphs may run without required services.
- **Impact:**
  - Runtime null references or no-op behavior
  - Behavior varies by scene or configuration profile
  - Bugs that only appear when a plugin is disabled or refactored
- **Mitigation Rules:**
  - **Explicit capability flags:** Plugin advertises what it provides
  - **Graphs declare required capabilities** at export time
  - **System fails early** if a graph requiring a capability is enabled without its plugin

**Implementation Tasks:**
- [ ] Define `IGraphCapability` and `IGraphCapabilityProvider`:
  ```csharp
  public interface IGraphCapabilityProvider {
      bool HasCapability(string capabilityId);
  }
  ```
- [ ] Give each domain plugin a static capability id set:
  - `VisualGraphs.Quest`, `VisualGraphs.Dialogue`, `Inventory.Core`, etc.
- [ ] Extend graph metadata to include `RequiredCapabilities: string[]`
- [ ] At plugin init:
  - Check all active graphs against available capabilities
  - **Fail fast (or hard-warn)** when requirements are not met
- [ ] Add export-time validation: if a graph uses an executor tied to capability X, add X to `RequiredCapabilities`

- **Priority:** **Critical** - Runtime failures due to missing services are unacceptable. Must validate at startup.

#### 13. Observability / Log Noise vs Signal ⚠️

**Goal:** Provide structured, sampled logging that enables debugging without drowning in noise.

- **Issue:** A system like this can either be opaque or drown you in logs. Both states are bad: either you can't see what's happening, or logs are unusable.
- **Impact:**
  - Hard to debug event flows and graph execution in real games
  - Logging overhead in hot paths
  - No way to answer "what the hell just handled this message?"
- **Mitigation Rules:**
  - **Structured, sampled logging** with execution correlation IDs
  - **Log levels scoped per subsystem**
  - **Ability to attach a temporary tracer** for one event or one graph

**Current Status:**

✅ **Already Complete:**
- Serilog structured logging with contextual logging (`ForContext<T>()`, `ForFeature("VisualGraphs")`)
- Structured properties in log messages (`{GraphId}`, `{MessageType}`, `{NodeId}`, etc.)
- Appropriate log levels (Debug, Information, Warning, Error)
- Node-level execution logging (started, completed, errors) in `GraphRunner`
- Subscription setup and routing error logging in `EventBusBridge`
- Plugin lifecycle logging in `VisualGraphPlugin`

❌ **Missing:**
- Correlation IDs / Execution IDs per routed event
- Graph-level execution tracking (started/finished/failed) - only node-level exists
- Event routing logs in `GraphEventRouter` (which graphs were targeted)
- Config knobs for log level and sampling rate
- Debug session helper for temporary verbosity

**Implementation Tasks:**
- [ ] Add correlation id per routed event:
  - `GraphExecutionContext` carries `ExecutionId` (Guid or incrementing id)
  - Pass `ExecutionId` through routing chain
- [ ] Add logging to `GraphEventRouter.RouteAsync`:
  - Log event routed → list of graphs targeted (with ExecutionId)
- [ ] Add graph-level execution tracking in `GraphRunner`:
  - Log graph execution started / finished / failed (with ExecutionId)
- [ ] Add config knobs to `VisualGraphConfig`:
  - Log level for VisualGraphs subsystem (default: Information)
  - Sampling rate for "normal" execution vs full trace mode
  - Enable/disable verbose node-level logging
- [ ] Implement a "Debug Session" helper:
  - Temporarily increase verbosity for specific graph id or message type
  - Auto-revert after N events or seconds

- **Priority:** High - Essential for debugging production issues without performance impact. **Foundation is solid, needs correlation IDs and config/sampling.**

### Recommended Mitigation Tasks

**Critical Priority (Architectural Integrity):**

**Risk #4 - Message Schema / Taxonomy Drift:**
- [ ] Create `MToolKit.Runtime.Messages` namespace structure
- [ ] Implement export-time validation in `XNodeGraphExporter` for message types and fields
- [ ] Create `GameMessageRegistry` with all message types
- [ ] Add schema lock tests
- [ ] Document message versioning strategy (evolve payloads, not types)
- [ ] Add CI/CD validation step for graph exports

**Risk #6 - Graphs Encoding Invariants:**
- [ ] Document architectural principle: "Graphs orchestrate, services enforce invariants" (in README)
- [ ] Create code review checklist: "Does this executor write directly to domain state?"
- [ ] Audit all existing executors for direct domain writes
- [ ] Create examples showing correct patterns (service validates, graph reacts)
- [ ] Add linting/validation to detect common anti-patterns (complex validation logic in graphs)
- [ ] Ensure all domain services expose needed APIs so graphs don't need to reimplement logic

**Risk #3 - Save/Load Lifecycle Ordering:**
- [ ] Define clear save/load phases in `SaveSystemCoordinator`
- [ ] Ensure graph state restores before event subscriptions are active
- [ ] Add validation to detect out-of-order restoration
- [ ] Consider a "restore mode" that suppresses events until fully loaded
- [ ] Test save/load extensively with complex graph state

**Risk #8 - Graph Complexity:**
- [ ] Define complexity budget (node count, depth, branching factor)
- [ ] Implement `GraphComplexityAnalyzer` editor utility
- [ ] Add editor warnings for graphs above threshold
- [x] Add docs section: "When to split a graph / move logic to C# / move to function graphs" ✅ (See README "When to Refactor Graphs")

**Risk #9 - Generic State God Table:**
- [ ] Introduce `StateKey` value object with domain scoping
- [ ] Replace raw string keys in state APIs with `StateKey`
- [ ] Enforce domain scoping in `IStateService`
- [ ] Add validation to forbid cross-domain writes
- [ ] Add analyzer listing all used `StateKey`s per graph and domain

**Risk #10 - xNode / Authoring Layer Fragility:**
- [ ] Define explicit "Authoring Adapter Layer" interfaces
- [ ] Refactor exporter to depend only on adapter interfaces
- [ ] Add tests with synthetic adapter graphs (no xNode dependency)
- [ ] Pin tested versions of xNode + Unity in README + CI
- [ ] Add migration checklist for upgrading xNode / Unity

**Risk #11 - Asset / Addressables Integrity:**
- [ ] Implement `VisualGraphsBuildValidator` (IPreprocessBuildWithReport)
- [ ] Fail build with clear report if referenced assets are missing
- [ ] Fail fast in runtime loader with clear error when asset fails to load
- [ ] Add editor window "Graph Asset Report"

**Risk #12 - Plugin / Service Availability:**
- [ ] Define `IGraphCapability` and `IGraphCapabilityProvider`
- [ ] Give each domain plugin static capability id set
- [ ] Extend graph metadata with `RequiredCapabilities`
- [ ] Validate capabilities at plugin init (fail fast if missing)
- [ ] Add export-time validation for capability requirements

**Risk #13 - Observability / Log Noise:**
- [ ] Add correlation id per routed event (`ExecutionId` in context)
- [ ] Emit structured logs (event routed, graph execution, node errors)
- [ ] Add config knobs (log level, sampling rate)
- [ ] Implement "Debug Session" helper (temporary verbosity for specific graph/message)

**High Priority (Tooling & Visibility):**
- [ ] Add tooling: "Find all graphs subscribing to MessageType X"
- [ ] Add runtime debugging: "Show all graphs that handled this event"
- [ ] Add execution metrics/telemetry (track failures, performance)

**Medium Priority:**
- [ ] Add tooling to detect unreferenced/dead graphs
- [ ] Add performance metrics for event routing
- [ ] Create graph lifecycle management process

---

## File Count Estimate

**New Files to Create:** ~60-80 files  
**Existing Files to Modify:** ~15-20 files  
**Test Files:** ~30-40 files  

**Current:** 43 files  
**After Roadmap:** ~130-160 files (3-4x growth)

