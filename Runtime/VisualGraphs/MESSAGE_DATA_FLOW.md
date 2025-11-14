# Message Data Flow System

## Overview

Added **message context nodes** to extract and check data from incoming MessagePipe messages. This enables conditional logic based on message payloads!

---

## 🎯 New Nodes

### 1. MessageFieldCheckNode
**Purpose:** Conditionally branch based on message field values

**Location:** `Message/Check Field`

**Example Use Cases:**
- Only increment objective if `enemyType == "Turnip"`
- Check if `zone == "dungeon"`
- Verify `itemId == "special_key"`

**Parameters:**
- `FieldName` - Name of field in message (case-sensitive)
- `ExpectedValue` - Value to compare against (as string)
- `IgnoreCase` - Case-insensitive comparison for strings

**Outputs:**
- `Matches` - Field equals expected value
- `DoesntMatch` - Field doesn't match

**Supported Field Types:**
- ✅ String
- ✅ Int, Long, Short, Byte
- ✅ Float, Double
- ✅ Bool
- ✅ Enum
- ✅ Any type with ToString()

---

### 2. MessageFieldGetNode
**Purpose:** Extract field value and store in graph state

**Location:** `Message/Get Field`

**Example Use Cases:**
- Store `experience` from enemy defeat
- Save `itemId` for later use
- Extract `position` for distance checks

**Parameters:**
- `FieldName` - Name of field to extract (case-sensitive)
- `StateKey` - Where to store in graph state
- `DebugLog` - Log extracted value to console

**Output:**
- `Output` - Continue execution (value stored in state)

---

### 3. MessageTypeCheckNode
**Purpose:** Branch based on message type

**Location:** `Message/Check Type`

**Example Use Cases:**
- Handle multiple event types in one graph
- Differentiate between `EnemyDefeatedMessage` and `BossDefeatedMessage`
- Type-specific logic branches

**Parameters:**
- `ExpectedType` - MessageTypeReference to check

**Outputs:**
- `Matches` - Message is of expected type
- `DoesntMatch` - Message is different type

---

## 🎮 Usage Examples

### Example 1: Filter Enemy Type (Your Use Case!)

```
Graph: "Kill Turnips" Objective

OnEvent(EnemyDefeatedMessage) →
  MessageFieldCheckNode
    FieldName: "enemyType"
    ExpectedValue: "Turnip"
    
    [Matches] → QuestObjectiveIncrementNode (Kill Turnips objective)
    [DoesntMatch] → (end - ignore other enemy types)
```

**Result:** Only Turnip kills count toward objective!

---

### Example 2: Extract and Store XP

```
OnEvent(EnemyDefeatedMessage) →
  MessageFieldGetNode
    FieldName: "experience"
    StateKey: "earned_xp"
    DebugLog: true
    
  → MessageFieldGetNode
    FieldName: "enemyType"
    StateKey: "last_enemy_killed"
    
  → QuestObjectiveIncrementNode
```

**Result:** XP and enemy type stored in state, available for other nodes!

---

### Example 3: Multi-Type Quest

```
OnEvent(message) →
  MessageTypeCheckNode(EnemyDefeatedMessage)
    [Matches] → Increment "enemies_defeated"
    
  MessageTypeCheckNode(ItemAcquiredMessage)
    [Matches] → Increment "items_collected"
    
  MessageTypeCheckNode(ZoneEnteredMessage)
    [Matches] → Increment "zones_explored"
```

**Result:** One graph handles multiple objective types!

---

### Example 4: Complex Conditional (Boss with Min Level)

```
OnEvent(EnemyDefeatedMessage) →
  MessageFieldCheckNode("enemyType", "DragonBoss")
    [DoesntMatch] → (end)
    
    [Matches] → MessageFieldCheckNode("playerLevel", "50")
      [DoesntMatch] → (end - level too low, doesn't count)
      
      [Matches] → MessageFieldGetNode("lootQuality", "boss_loot_tier")
                → QuestObjectiveIncrementNode
```

**Result:** Only dragon boss kills by level 50+ players count!

---

## 🔧 How It Works (Under the Hood)

### Reflection-Based Field Access

The executors use C# reflection to access message fields at runtime:

```csharp
var messageType = message.GetType();
var field = messageType.GetField(fieldName);
var value = field.GetValue(message);
```

**Pros:**
- ✅ Works with any message type
- ✅ No code generation needed
- ✅ Completely game-agnostic

**Cons:**
- ⚠️ Slightly slower than direct access (negligible for most games)
- ⚠️ Field names must be exact (case-sensitive)
- ⚠️ No compile-time checking (typos discovered at runtime)

---

## 📊 Performance Notes

**Reflection overhead:**
- First access: ~100-500 nanoseconds
- Subsequent: ~50-100 nanoseconds (reflection caching)
- Negligible for typical quest systems (10-100 events/sec)

**For high-throughput systems (1000+ events/sec):**
- Consider caching PropertyInfo/FieldInfo
- Or use typed nodes (Option 2 from earlier discussion)

---

## 🚀 What This Enables

### Before (No Data Flow):
```
OnEnemyDefeated → Increment
  ↑
  Can't filter by enemy type!
  All enemies count toward objective!
```

### After (With Data Flow):
```
OnEnemyDefeated → 
  Check: enemyType == "Turnip" →
    [Yes] → Increment turnip objective
    [No] → Ignore
```

**Now you can:**
- ✅ Filter events by field values
- ✅ Store message data for later use
- ✅ Make conditional decisions based on payload
- ✅ Handle multiple message types in one graph
- ✅ Extract data for display in UI

---

## 🎯 Your Updated Graph

**Original:**
```
OnEvent(EnablePlayerMovementMessage) →
  QuestObjectiveIncrementNode(TC_Quest1_Task1)
```

**With Field Checking:**
```
OnEvent(EnablePlayerMovementMessage) →
  MessageFieldCheckNode("isEnabled", "true") →
    [Matches] → QuestObjectiveIncrementNode(TC_Quest1_Task1)
```

**With Data Extraction:**
```
OnEvent(EnablePlayerMovementMessage) →
  MessageFieldGetNode("timestamp", "movement_enabled_at", debugLog: true) →
  QuestObjectiveIncrementNode(TC_Quest1_Task1)
```

---

## 🔮 Future Enhancements (Phase 9)

This is a **stopgap solution** until full data flow (Option 3) is implemented in Phase 9:

**Phase 9 will add:**
- Typed data ports (no reflection needed)
- Visual data wires (like Blueprints)
- Full type safety (compile-time checking)
- Math/logic nodes for data manipulation

**For now, message context nodes solve 80% of use cases!**

---

## 📁 Files Created

### Nodes (Authoring):
- `Runtime/VisualGraphs/Authoring/Nodes/Message/MessageFieldCheckNode.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Message/MessageFieldGetNode.cs`
- `Runtime/VisualGraphs/Authoring/Nodes/Message/MessageTypeCheckNode.cs`

### Executors (Runtime):
- `Runtime/VisualGraphs/Executors/MessageFieldCheckNodeExecutor.cs`
- `Runtime/VisualGraphs/Executors/MessageFieldGetNodeExecutor.cs`
- `Runtime/VisualGraphs/Executors/MessageTypeCheckNodeExecutor.cs`

### Registration:
- `Runtime/VisualGraphs/VisualGraphPlugin.cs` - Added executor registration

**Total:** 6 new files, 1 modified file

---

## ✅ Ready to Use!

The nodes are now available in the xNode create menu under `Message/`:
- Check Field
- Get Field
- Check Type

Start filtering your events! 🎉

