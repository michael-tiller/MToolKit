# Visual Graphs - Type-Based Architecture

## Overview

The Visual Graphs system now uses **type references** instead of **string IDs** for type-safe, refactor-safe graph authoring.

## Core Principle

> **Events = MessagePipe C# Types**  
> **Data = Asset References**

- **Events/Messages**: Subscribe to actual `Type` (e.g., `typeof(PlayerEnteredZoneMessage)`)
- **Game Data**: Reference assets directly (e.g., `QuestGraphAsset`, `AssetReferenceAudioClip`)
- **NO string-based identifiers** for types or assets

---

## Type System Components

### 1. `SerializableType`

Generic Unity-serializable wrapper for `System.Type`.

**Location:** `Runtime/Core/Types/SerializableType.cs`

```csharp
public sealed class SerializableType
{
    public Type Type { get; set; } // Actual System.Type
    public bool IsValid => Type != null;
    public string Name => Type?.Name;
}

// Generic version with constraints
public sealed class SerializableType<TBase> where TBase : class
{
    public Type Type { get; set; } // Must inherit from TBase
}
```

**Features:**
- ✅ Unity serialization (stores `AssemblyQualifiedName`)
- ✅ Type caching (resolves once, caches result)
- ✅ Null-safe
- ✅ Implicit operators (`SerializableType → Type`)

---

### 2. `MessageTypeReference`

Specialized type reference for MessagePipe messages with Odin inspector integration.

**Location:** `Runtime/Core/Types/MessageTypeReference.cs`

```csharp
public sealed class MessageTypeReference
{
    [ValueDropdown(nameof(GetMessageTypes))]
    public Type Type { get; set; } // Must implement IGameMessage
}
```

**Features:**
- ✅ **Dropdown of all `IGameMessage` types** in inspector
- ✅ Validates type implements `IGameMessage`
- ✅ Grouped by namespace for organization
- ✅ Searchable dropdown (10+ items)
- ✅ Inline property display in Odin

**Inspector UI:**

```
┌─────────────────────────────────────┐
│ Message Type: [▼ Select Type...]   │
├─────────────────────────────────────┤
│   Events/                           │
│     ├─ PlayerEnteredZoneMessage     │
│     ├─ PlayerExitedZoneMessage      │
│   Quest/                            │
│     ├─ QuestStartedMessage          │
│     ├─ QuestCompletedMessage        │
│   Combat/                           │
│     └─ EnemyDefeatedMessage         │
└─────────────────────────────────────┘
```

---

## Graph Subscription System

### Old (String-Based) ❌

```csharp
// FRAGILE - typos, no refactoring support
public string EventType = "Player.EnteredZone";
public string EventDomain = "Player";
```

**Problems:**
- Typos cause runtime failures
- Renaming breaks all graphs
- No compile-time validation
- Hard to find usages
- No type safety

---

### New (Type-Based) ✅

```csharp
// TYPE-SAFE - compiler checked, refactor safe
public MessageTypeReference MessageType = new();
```

#### Graph Asset (Explicit Subscriptions)

```csharp
[CreateAssetMenu(menuName = "MToolKit/Visual Graphs/Quest Graph")]
public sealed class QuestGraphAsset : NodeGraph
{
    [BoxGroup("Event Subscriptions")]
    public List<MessageSubscription> Subscriptions = new();
}

[Serializable]
public sealed class MessageSubscription
{
    public MessageTypeReference MessageType = new(); // ✅ TYPE
    public bool Required = true;
    public string DomainFilter; // Optional context filter
}
```

#### Entry Node

```csharp
[CreateNodeMenu("Quest/On Event")]
public sealed class QuestOnEventNode : EntryNodeBase
{
    public MessageTypeReference MessageType = new(); // ✅ TYPE
    public string DomainFilter;
}
```

#### Emit Node

```csharp
[CreateNodeMenu("Quest/Emit Event")]
public sealed class QuestEmitEventNode : VisualGraphNodeBase
{
    public MessageTypeReference MessageType = new(); // ✅ TYPE
    public List<MessagePayloadParameter> Payload;
}
```

---

## Runtime DTOs

### RuntimeSubscriptionDefinition

```csharp
[Serializable]
public sealed class RuntimeSubscriptionDefinition
{
    public MessageTypeReference MessageType; // ✅ TYPE (not string)
    public string DomainFilter;
    public bool Required;
}
```

**Export Process:**

```csharp
// XNodeGraphExporter.cs
if (graphAsset is QuestGraphAsset questGraph)
{
    foreach (var subscription in questGraph.Subscriptions)
    {
        def.Subscriptions.Add(new RuntimeSubscriptionDefinition
        {
            MessageType = subscription.MessageType, // ✅ Direct type copy
            DomainFilter = subscription.DomainFilter,
            Required = subscription.Required
        });
    }
}
```

**No string conversion, no lookup, no fragility.**

---

## Asset References (Not String IDs)

For referencing **game data** (not events), use **asset references**.

### Direct Asset Reference

```csharp
public sealed class QuestStartGraphNode : VisualGraphNodeBase
{
    public QuestGraphAsset TargetGraph; // ✅ Direct reference
}
```

### Addressable Asset Reference

```csharp
#if UNITY_ADDRESSABLES
using UnityEngine.AddressableAssets;
#endif

public sealed class QuestStartGraphNode : VisualGraphNodeBase
{
    public AssetReferenceAudioClip Sound; // ✅ Addressable reference
    public AssetReferenceGameObject Prefab;
}
```

**Benefits:**
- ✅ Unity automatically updates references when assets move
- ✅ Find References works (right-click asset → Find References)
- ✅ Type-safe (can't assign wrong asset type)
- ✅ Inspector shows missing/broken references
- ✅ Addressables support built-in

---

## Benefits Summary

### 1. **Type Safety**

```csharp
// ❌ OLD: Runtime error
EventType = "Playerr.EnterdZone"; // Typo not caught

// ✅ NEW: Compile-time error
MessageType = typeof(NotAMessage); // Compiler error: doesn't implement IGameMessage
```

### 2. **Refactoring**

```csharp
// ❌ OLD: Breaks all graphs
"QuestStarted" → "QuestInitialized" // Must find/replace

// ✅ NEW: Automatic updates
QuestStartedMessage → QuestInitializedMessage // Unity updates all refs
```

### 3. **Discoverability**

```csharp
// ❌ OLD: Text search
// Where is "Player.EnteredZone" used?

// ✅ NEW: Find References
// Right-click PlayerEnteredZoneMessage → Find References
// Shows all graphs/nodes using it
```

### 4. **Validation**

```csharp
// ❌ OLD: No validation
EventType = ""; // Empty, runtime failure

// ✅ NEW: Editor validation
MessageType = null; // Inspector shows error: [Required]
```

### 5. **Documentation**

```csharp
// ❌ OLD: What does "Quest.TaskComplete" mean?
// Must read code/wiki

// ✅ NEW: IntelliSense shows XML docs
/// <summary>Fired when any quest task is completed...</summary>
public readonly struct QuestTaskCompleteMessage : IGameMessage { }
```

---

## Migration Guide

### Updating Existing Code

**Before:**
```csharp
public string EventType = "Player.EnteredZone";
public string EventDomain = "Player";
```

**After:**
```csharp
public MessageTypeReference MessageType = new();
// In inspector: Select "PlayerEnteredZoneMessage" from dropdown
```

### Creating New Message Types

1. **Define the message** (C# struct/class)

```csharp
// Runtime/MessageBus/Events/PlayerEnteredZoneMessage.cs
using MToolKit.Runtime.MessageBus.Interfaces;

public readonly struct PlayerEnteredZoneMessage : IGameMessage
{
    public readonly string ZoneId;
    public readonly Vector3 Position;
    
    public PlayerEnteredZoneMessage(string zoneId, Vector3 position)
    {
        ZoneId = zoneId;
        Position = position;
    }
}
```

2. **Use in graphs** (dropdown automatically includes it)

```csharp
// In QuestGraphAsset inspector:
// Subscriptions → Add → Message Type → [dropdown] → "PlayerEnteredZoneMessage"
```

3. **Emit from code**

```csharp
var message = new PlayerEnteredZoneMessage("Forest", player.Position);
publisher.Publish(message);
```

---

## Technical Implementation

### Serialization

Unity doesn't serialize `System.Type` directly. We store `AssemblyQualifiedName`:

```csharp
[SerializeField]
[HideInInspector]
private string assemblyQualifiedName; // e.g., "MyGame.PlayerEnteredZoneMessage, MyGame"

public Type Type
{
    get => Type.GetType(assemblyQualifiedName);
    set => assemblyQualifiedName = value?.AssemblyQualifiedName;
}
```

### Odin Integration

`ValueDropdown` auto-populates all `IGameMessage` types:

```csharp
#if UNITY_EDITOR
private static IEnumerable<ValueDropdownItem<Type>> GetMessageTypes()
{
    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    {
        foreach (var type in assembly.GetTypes())
        {
            if (typeof(IGameMessage).IsAssignableFrom(type) && 
                !type.IsInterface && 
                !type.IsAbstract)
            {
                yield return new ValueDropdownItem<Type>(
                    $"{type.Namespace}/{type.Name}",
                    type
                );
            }
        }
    }
}
#endif
```

### Runtime Lookup

At runtime, `MessageTypeReference` resolves to actual `Type`:

```csharp
var messageType = subscription.MessageType.Type; // System.Type
var publisher = container.Resolve(typeof(IPublisher<>).MakeGenericType(messageType));
publisher.Publish(messageInstance);
```

---

## File Structure

```
MToolKit/Runtime/
├─ Core/
│  └─ Types/
│     ├─ SerializableType.cs           // Generic type wrapper
│     ├─ MessageTypeReference.cs       // IGameMessage-specific
│     └─ MessageTypeAttribute.cs       // Odin attribute helper
│
├─ MessageBus/
│  ├─ Interfaces/
│  │  └─ IGameMessage.cs               // Base interface
│  └─ Events/
│     ├─ SceneLoadedMessage.cs
│     ├─ PlayerEnteredZoneMessage.cs   // Example
│     └─ QuestStartedMessage.cs        // Example
│
└─ VisualGraphs/
   ├─ Authoring/
   │  ├─ Graphs/
   │  │  └─ QuestGraphAsset.cs         // Has MessageSubscription list
   │  └─ Nodes/
   │     └─ Quest/
   │        ├─ QuestOnEventNode.cs     // Uses MessageTypeReference
   │        └─ QuestEmitEventNode.cs   // Uses MessageTypeReference
   │
   ├─ Runtime/
   │  └─ DTOs/
   │     └─ RuntimeSubscriptionDefinition.cs // Uses MessageTypeReference
   │
   └─ Export/
      └─ XNodeGraphExporter.cs         // Exports from graph.Subscriptions
```

---

## Future Enhancements

### 1. Generic Message Publisher Node

```csharp
public sealed class EmitMessageNode<T> : VisualGraphNodeBase where T : IGameMessage
{
    // Auto-generate input ports from T's fields
}
```

### 2. Message Payload Validation

Validate payload parameters match message fields at export time:

```csharp
var messageType = EmitNode.MessageType.Type;
foreach (var param in EmitNode.Payload)
{
    var field = messageType.GetField(param.ParameterName);
    if (field == null)
        throw new InvalidGraphException($"Message {messageType.Name} has no field '{param.ParameterName}'");
}
```

### 3. Message Contract Generation

Auto-generate message classes from graph payload definitions.

---

## Conclusion

The type-based architecture provides:

- ✅ **Compile-time safety** - Typos caught by compiler
- ✅ **Refactor support** - Rename works across all graphs
- ✅ **Discoverability** - Find References shows all usages
- ✅ **Validation** - Editor catches missing/invalid types
- ✅ **IntelliSense** - Documentation at authoring time
- ✅ **Performance** - No runtime string lookups

**No more string-based fragility. Type-safe from authoring to runtime.**

