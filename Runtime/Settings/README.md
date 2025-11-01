@page settings_system Settings System

@brief Reactive Settings with UI Binding

# Settings System - Reactive Settings with UI Binding

Reactive settings system with automatic UI binding, dirty tracking, and persistence integration.

## Purpose

The Settings module provides:
- **Reactive Settings** - Bind UI elements to settings using R3 ReactiveProperties
- **Module-Based Organization** - Separate settings modules (Audio, Graphics, Input, Game)
- **Dirty Tracking** - Automatic tracking of unsaved changes
- **UI Components** - Pre-built UI components for common setting types
- **Type-Safe Bindings** - Strongly-typed bindings for Bool, Float, Int, String, Enum

## Structure

```
Settings/
├── Audio/              # Audio-specific settings
├── BoundSettings/      # Reactive setting implementations
├── Enums/              # Shared enums
├── Game/               # Game-specific settings
├── Graphics/           # Graphics settings
├── Input/              # Input settings
├── Interfaces/         # Settings system interfaces
├── UI/                 # UI components for settings
└── README.md           # This file
```

## Key Files

### Core System

- **`SettingsSystem.cs`** - Main settings controller and coordinator
- **`SettingsPlugin.cs`** - Plugin registration for settings system
- **`ISettingsSystem.cs`** - Settings system interface

### Reactive Settings

- **`ReactiveSetting.cs`** - Base reactive setting implementation
- **`AbstractBoundReactiveSetting.cs`** - Base class for bound reactive settings
- **`BoolBoundReactiveSetting.cs`** - Boolean settings
- **`FloatBoundReactiveSetting.cs`** - Float settings
- **`IntBoundReactiveSetting.cs`** - Integer settings
- **`StringBoundReactiveSetting.cs`** - String settings
- **`EnumBoundReactiveSetting.cs`** - Enum settings

### Settings Modules

#### Audio Settings
- **`AudioSettingsModule.cs`** - Audio settings management
- **`AudioSettingsInitializer.cs`** - Audio settings initialization
- **`IAudioSettings.cs`** - Audio settings interface

#### Graphics Settings
- **`GraphicsSettingsModule.cs`** - Graphics settings management
- **`GraphicsSettingsInitializer.cs`** - Graphics settings initialization
- **`IGraphicsSettings.cs`** - Graphics settings interface

#### Input Settings
- **`InputSettingsModule.cs`** - Input settings management
- **`InputSettingsInitializer.cs`** - Input settings initialization
- **`IInputSettings.cs`** - Input settings interface

#### Game Settings
- **`GameSettingsModule.cs`** - Game settings management
- **`GameSettingsInitializer.cs`** - Game settings initialization
- **`IGameSettings.cs`** - Game settings interface

### UI Components

- **`BoolBoundToggle.cs`** - Toggle UI component
- **`FloatBoundSlider.cs`** - Slider UI component
- **`IntBoundDropdown.cs`** - Dropdown UI component
- **`ModalButton.cs`** - Modal navigation button
- **`SubviewButton.cs`** - Subview navigation button

## Usage Examples

### Accessing Settings

```csharp
// Get settings system from DI
var settingsSystem = resolver.Resolve<ISettingsSystem>();

// Access specific settings modules
var audioSettings = settingsSystem.AudioSettings;
var graphicsSettings = settingsSystem.GraphicsSettings;

// Get reactive properties
var masterVolume = audioSettings.MasterVolume;
var isFullscreen = graphicsSettings.IsFullscreen;
```

### Subscribing to Setting Changes

```csharp
// Subscribe to setting changes
masterVolume.Property.Subscribe(volume =>
{
    Debug.Log($"Master volume changed to: {volume}");
});
```

### Binding UI to Settings

```csharp
public class VolumeSlider : MonoBehaviour
{
    [SerializeField] private FloatBoundSlider slider;
    
    private void Start()
    {
        var settingsSystem = FindFirstObjectByType<SettingsSystem>();
        var audioSettings = settingsSystem.AudioSettings;
        
        // Bind slider to setting
        slider.Bind(audioSettings.MasterVolume);
    }
}
```

### Creating Custom Settings Modules

```csharp
public interface ICustomSettings
{
    ReactiveSetting<string> PlayerName { get; }
    ReactiveSetting<int> Difficulty { get; }
}

public class CustomSettingsModule : ISettingsModule, ICustomSettings
{
    public ReactiveSetting<string> PlayerName { get; }
    public ReactiveSetting<int> Difficulty { get; }
    
    public CustomSettingsModule()
    {
        PlayerName = new ReactiveSetting<string>("Player", "PlayerName");
        Difficulty = new ReactiveSetting<int>(1, "Difficulty");
    }
}
```

## Dependencies

- **R3** - Reactive properties (`ReactiveProperty<T>`)
- **Sirenix Odin Inspector** - Editor enhancements
- **VContainer** - Dependency injection

## Integration Points

- **Persistence** - Settings can be persisted via the persistence module
- **UI/Navigation** - Settings UI components integrate with navigation system
- **Audio** - Audio settings control mixer parameters
- **Input** - Input settings configure input rebinding

## Design Patterns

- **Reactive Programming** - R3 for reactive state management
- **Module Pattern** - Separate settings modules for different domains
- **Binding Pattern** - Automatic UI-to-setting binding
- **Dirty Tracking** - Automatic change detection

## Test Coverage

**Status**: ⚠️ **ZERO TESTS** (See TESTS_GOALS.md for details)

Critical untested files:
- `SettingsSystem.cs`
- `ReactiveSetting.cs`
- All settings modules
- All UI components

## Known Issues

- **Persistence**: INI-based persistence not yet implemented
- **Graphics Settings**: Implementation incomplete

