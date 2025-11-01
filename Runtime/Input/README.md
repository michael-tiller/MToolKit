@page input_system Input System

@brief Unity Input System Abstraction with Rebinding

# Input System - Unity Input System Abstraction with Rebinding

Input abstraction with Unity Input System integration, interactive rebinding, and device fallback.

## Purpose

The Input module provides:
- **Input Abstraction** - High-level input API over Unity Input System
- **Interactive Rebinding** - User-driven control rebinding
- **Device Fallback** - Automatic input device switching
- **Conflict Detection** - Detection of binding conflicts
- **Composite Binding Support** - Complex input binding support

## Structure

```
Input/
├── GamepadIconsData.cs         # Gamepad icons config
├── InputService.cs             # Main input service
├── InputRebinderService.cs     # Input rebinding service
├── InputRebinderPlugin.cs      # Plugin registration
├── IInputService.cs            # Input service interface
└── IInputRebinderService.cs    # Input rebinder interface
```

## Key Files

### Core Services

- **`InputService.cs`** - Main input handling service
- **`IInputService.cs`** - Input service interface
- **`InputRebinderService.cs`** - Input rebinding functionality
- **`IInputRebinderService.cs`** - Input rebinder interface
- **`InputRebinderPlugin.cs`** - Plugin registration

## Usage Examples

### Reading Input

```csharp
// Get input service from DI
var inputService = resolver.Resolve<IInputService>();

// Check if action is pressed
if (inputService.IsActionPressed("Jump"))
{
    // Player jumped
}

// Get input value
var moveVector = inputService.GetInputValue<Vector2>("Move");
```

### Rebinding Input

```csharp
// Get input rebinder
var rebinder = resolver.Resolve<IInputRebinderService>();

// Start rebinding
await rebinder.StartRebindingAsync("Jump");

// Listen for rebind completion
rebinder.OnRebindingCompleted.Subscribe(action =>
{
    Debug.Log($"Action {action} rebound successfully");
});
```

### Device Fallback

```csharp
// Input service automatically handles device switching
// If keyboard disconnected, switches to gamepad
// If gamepad disconnected, switches to keyboard
```

## Dependencies

- **Unity Input System** - Input handling
- **VContainer** - Dependency injection
- **Serilog** - Structured logging
- **Sirenix Odin Inspector** - Editor enhancements

## Integration Points

- **Settings** - Input settings module for control configuration
- **UI** - Input rebinding UI components
- **Core** - Uses MessageBus for input events

## Design Patterns

- **Service Pattern** - Input service for centralized input handling
- **Strategy Pattern** - Device switching strategies
- **Observer Pattern** - Event-driven input handling

## Test Coverage

**Status**: Unknown (no test files found)

Critical files need testing:
- `InputService.cs`
- `InputRebinderService.cs`
- Device fallback logic
- Conflict detection

## Known Issues

- Input rebinding UI components in Components/ module
- Settings integration could be improved
- Consider adding input recording for debugging

## Related Modules

- **Settings/Input/** - Input settings configuration
- **Components/** - Input binding UI components
- **Navigation** - Input for navigation

