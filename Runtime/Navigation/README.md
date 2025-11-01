@page navigation_system Navigation System

@brief UI Navigation with View Stacks and Modals

# Navigation System - UI Navigation with View Stacks and Modals

UI navigation system with view stacks, modals, subviews, and canvas management.

## Purpose

The Navigation module provides:
- **View Management** - Hierarchical view system with stacks and modals
- **Canvas Management** - Multiple canvas types (UI, Overlay, etc.)
- **Navigation Service** - High-level navigation API
- **View Lifecycle** - Automatic show/hide/pause lifecycle management
- **Subview Support** - Nested view hierarchies

## Structure

```
Navigation/
├── Config/              # Canvas and view configuration
├── DataStructures/      # Navigation data structures
├── Enums/               # Navigation enums
├── Interfaces/          # Navigation service interfaces
├── Services/            # Navigation service implementations
├── Views/               # View implementations
└── README.md            # This file
```

## Key Files

### Core System

- **`NavigationSystem.cs`** - Main navigation controller (1000+ lines)
- **`NavigationService.cs`** - Core navigation service implementation
- **`NullNavigationService.cs`** - Null object pattern implementation
- **`NavigationPlugin.cs`** - Plugin registration
- **`NavigationInstaller.cs`** - DI registration

### Interfaces

- **`INavigationService.cs`** - Navigation service interface
- **`IModalService.cs`** - Modal service interface
- **`IView.cs`** - View interface
- **`ISubview.cs`** - Subview interface

### View Classes

- **`View.cs`** - Base view class with lifecycle
- **`ModalView.cs`** - Modal view implementation
- **`Subview.cs`** - Subview implementation
- **`TimedModalView.cs`** - Modal with automatic dismissal
- **`InterstitialAlertView.cs`** - Alert dialog implementation

### View Managers

- **`SubviewManager.cs`** - Subview management
- **`AbstractSubviewManager.cs`** - Base subview manager

### Configuration

- **`CanvasConfig.cs`** - Canvas configuration
- **`NavigationCanvasConfig.cs`** - Navigation-specific canvas config
- **`ViewConfig.cs`** - View configuration
- **`ECanvasType.cs`** - Canvas type enum

## Usage Examples

### Registering Views

```csharp
public class MyView : View
{
    protected override void OnInitialize()
    {
        // Setup view
    }
    
    protected override void OnShow()
    {
        // View is showing
    }
    
    protected override void OnHide()
    {
        // View is hiding
    }
    
    protected override void OnPause()
    {
        // View is pausing
    }
    
    protected override void OnResume()
    {
        // View is resuming
    }
}
```

### Navigation Service Usage

```csharp
// Get navigation service
var navigation = resolver.Resolve<INavigationService>();

// Show a view
await navigation.ShowViewAsync(viewName);

// Hide a view
await navigation.HideViewAsync();

// Show modal
await navigation.ShowModalAsync(modalName, modalData);

// Navigate with back stack
await navigation.PushViewAsync(viewName);
await navigation.PopViewAsync();
```

### Publishing Navigation Messages

```csharp
// Navigate via MessagePipe
var publisher = GameMessageBroker.GetPublisher<NavigationRequestMessage>();
publisher?.Publish(new NavigationRequestMessage("MainMenu"));
```

### Subscribing to Navigation

```csharp
// Subscribe to navigation events
var subscriber = GameMessageBroker.GetSubscriber<NavigationRequestMessage>();
subscriber.Subscribe(message =>
{
    Debug.Log($"Navigation request: {message.TargetScene}");
});
```

## Dependencies

- **MessagePipe** - Navigation message publishing
- **VContainer** - Dependency injection
- **DOTween** - Animation support
- **Sirenix Odin Inspector** - Editor enhancements

## Integration Points

- **Core** - Uses MessageBus from Core module
- **Analytics** - Navigation events can be tracked
- **Settings** - Navigation integrates with settings UI
- **Localization** - Views can be localized

## Design Patterns

- **View Stack Pattern** - Managing view hierarchies
- **Modal Pattern** - Modal overlay management
- **Lifecycle Management** - View lifecycle hooks
- **Null Object Pattern** - `NullNavigationService` for safe usage

## Test Coverage

**Status**: ⚠️ **MINIMAL TESTS** - Only NavigationInstaller has tests (See TESTS_GOALS.md)

Critical untested files:
- `NavigationSystem.cs` - Main controller (1000+ lines)
- `NavigationService.cs` - Core service
- All view classes

## Known Issues

- Complex system with 1000+ lines in NavigationSystem.cs
- Consider breaking down into smaller services
- Back navigation can be improved

