@page messagebus_system MessageBus System

@brief Decoupled Communication with MessagePipe

# MessageBus System - Decoupled Communication with MessagePipe

Decoupled message publishing/subscription system using MessagePipe for game-wide communication.

## Purpose

The MessageBus module provides:
- **Global Message Broker** - Cross-scene message communication
- **Game Message Broker** - Scene-scoped message communication
- **Async Message Support** - Async/await message handling
- **Request/Response Pattern** - Request messages with response handling
- **Type-Safe Messaging** - Strongly-typed message definitions

## Structure

```
MessageBus/
├── GlobalAsyncMessageBroker.cs   # Global cross-scene broker
├── GameMessageBroker.cs          # Game-scene broker
├── Messages/                      # Message type definitions
│   ├── IGameMessage.cs          # Base message interface
│   ├── NavigationRequestMessage.cs
│   ├── PauseToggledMessage.cs
│   ├── SceneLoadedMessage.cs
│   └── ... (12+ message types)
└── README.md                     # This file
```

## Key Files

### Message Brokers

- **`GlobalAsyncMessageBroker.cs`** - Global message broker for cross-scene communication
- **`GameMessageBroker.cs`** - Game message broker for scene-scoped communication

### Message Types

- **`IGameMessage.cs`** - Base interface for all game messages
- **`NavigationRequestMessage.cs`** - Navigation requests
- **`PauseToggledMessage.cs`** - Pause state changes
- **`SceneLoadedMessage.cs`** - Scene load events
- **`FadeBlackoutMessage.cs`** - Blackout fade requests
- **`ErrorRequestMessage.cs`** - Error display requests
- **`PlayerDeathMessage.cs`** - Player death events
- **`PlayerRespawnRequestMessage.cs`** - Respawn requests
- **`BackRequestMessage.cs`** - Back navigation requests
- **`QuitRequestMessage.cs`** - Quit game requests
- And more...

## Usage Examples

### Using Global Message Broker (Cross-Scene)

```csharp
// Get publisher from global broker
var publisher = GlobalAsyncMessageBroker.GetPublisher<NavigationRequestMessage>();

// Publish a navigation message
publisher?.Publish(new NavigationRequestMessage("MainMenu"));
```

### Using Game Message Broker (Scene-Scoped)

```csharp
// Get publisher from game broker
var publisher = GameMessageBroker.GetPublisher<PauseToggledMessage>();

// Publish a pause message
publisher?.Publish(new PauseToggledMessage(isPaused: true));
```

### Subscribing to Messages

```csharp
// Get subscriber
var subscriber = GlobalAsyncMessageBroker.GetSubscriber<NavigationRequestMessage>();

// Subscribe to messages
var disposable = subscriber.Subscribe(message =>
{
    Debug.Log($"Navigating to: {message.TargetScene}");
    await NavigateToSceneAsync(message.TargetScene);
});

// Don't forget to dispose when done
disposable.Dispose();
```

### Async Message Handling

```csharp
// Subscribe with async handler
subscriber.Subscribe(async message =>
{
    await HandleMessageAsync(message);
});
```

### Request/Response Pattern

```csharp
// Publish request and await response
var result = await GlobalAsyncMessageBroker.PublishRequestAsync<MyRequestMessage, MyResponseMessage>(
    new MyRequestMessage(data)
);
```

## When to Use Which Broker

### Use GlobalAsyncMessageBroker for:
- Messages that should persist across scenes
- Settings changes that affect all scenes
- Analytics events
- Error messages
- Save/load operations

### Use GameMessageBroker for:
- Scene-specific messages
- UI interactions
- Gameplay events (pause, quit, etc.)
- Scene-loaded events
- Player actions within a scene

## Creating Custom Messages

```csharp
using MToolKit.Runtime.MessageBus.Messages;

// Create a new message type
public readonly struct MyCustomMessage : IGameMessage
{
    public readonly string Data;
    public readonly int Value;
    
    public MyCustomMessage(string data, int value)
    {
        Data = data;
        Value = value;
    }
}

// Register in GlobalInstaller
builder.RegisterMessageBroker<MyCustomMessage>(options);
```

## Dependencies

- **MessagePipe** - Core messaging infrastructure
- **VContainer** - DI container for message broker resolution
- **Serilog** - Structured logging
- **UniTask** - Async support

## Integration Points

- **Core** - MessagePipe integration
- **Navigation** - Navigation requests via MessageBus
- **ErrorSystem** - Error messages via MessageBus
- **Analytics** - Event messages via MessageBus
- **All Modules** - All modules can publish/subscribe to messages

## Design Patterns

- **Pub/Sub Pattern** - Publisher/subscriber pattern for decoupled communication
- **Broker Pattern** - Central message broker for routing messages
- **Mediator Pattern** - Mediates communication between components
- **Request/Response Pattern** - Request messages with response handling

## Test Coverage

**Status**: ✅ **WELL TESTED**

Test files:
- `GlobalAsyncMessageBrokerTests.cs` - Global broker tests
- Request/response pattern tests
- Async message handling tests

## Known Issues

- Some messages could benefit from more detailed documentation
- Consider adding message validation
- Message filtering could be enhanced

## Best Practices

1. **Use appropriate broker** - Global vs Game scope
2. **Dispose subscriptions** - Always dispose IDisposable from Subscribe()
3. **Type safety** - Use strongly-typed messages
4. **Async handling** - Use async handlers for async operations
5. **Error handling** - Wrap message handlers in try-catch
6. **Logging** - Log important message publications

## Message Types

### Navigation Messages
- `NavigationRequestMessage` - Navigate to a scene/view
- `BackRequestMessage` - Back navigation request

### Game State Messages
- `PauseToggledMessage` - Pause/unpause game
- `QuitRequestMessage` - Quit game request
- `SceneLoadedMessage` - Scene loaded event

### Player Messages
- `PlayerDeathMessage` - Player death event
- `PlayerRespawnRequestMessage` - Respawn request
- `EnablePlayerMovementMessage` - Enable/disable player movement

### UI Messages
- `FadeBlackoutMessage` - Blackout fade requests
- `InterstitialAlertRequestMessage` - Alert dialog request
- `ErrorRequestMessage` - Error display request

See `Messages/` folder for complete list.

