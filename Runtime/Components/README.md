@page ui_components UI Components

@brief Reusable UI Elements and Helpers

# UI Components - Reusable UI Elements and Helpers

UI components, helpers, and reusable game components.

## Purpose

The Components module provides:
- **UI Components** - Reusable UI components (buttons, toggles, etc.)
- **Input Components** - Input binding UI components
- **Audio Components** - Audio integration components
- **Version Display** - Version label component
- **Spinner UI** - Loading spinner component
- **Animated Button** - Tween-animated button component

## Structure

```
Components/
├── AudioButtonComponent.cs         # Audio click feedback
├── BindingComponentElement.cs      # Input binding UI element
├── CompositeBindingHelper.cs       # Composite input bindings
├── InputFieldWithText.cs           # Input field with text label
├── KeybindListElement.cs           # Keyboard shortcut list
├── SpinnerImage.cs                 # Loading spinner
├── TweenAnimatedButton.cs          # Animated button
└── VersionLabel.cs                 # Version display label
```

## Key Files

### Audio Components

- **`AudioButtonComponent.cs`** - Plays audio on button clicks/selects

### Input Components

- **`BindingComponentElement.cs`** - Input binding UI element
- **`CompositeBindingHelper.cs`** - Composite input binding helper
- **`InputFieldWithText.cs`** - Input field with label
- **`KeybindListElement.cs`** - Keyboard shortcut list

### UI Components

- **`SpinnerImage.cs`** - Loading spinner image
- **`TweenAnimatedButton.cs`** - Button with DOTween animation
- **`VersionLabel.cs`** - Displays game version

## Usage Examples

### Audio Button Component

```csharp
// Attach AudioButtonComponent to Unity Button
// Configure in inspector:
// - Click Audio Clip
// - Select Audio Clip
// - Per-component volume override (optional)

// Audio plays automatically on click/select
```

### Spinner Image

```csharp
// Attach SpinnerImage to Image component
// Configure in inspector:
// - Rotation speed
// - Animation style

// Start spinning
spinner.Start();

// Stop spinning
spinner.Stop();
```

### Version Label

```csharp
// Attach VersionLabel to Text/TextMeshPro component
// Automatically displays:
// - Game version
// - Build number (if available)
```

### Animated Button

```csharp
// Attach TweenAnimatedButton to Button
// Configure in inspector:
// - Animation type (Scale, Shake, etc.)
// - Animation duration
// - Animation intensity

// Animations play automatically on click
```

### Input Binding Component

```csharp
// Attach BindingComponentElement to UI
// Configure in inspector:
// - Binding name
// - Display text

// Automatically updates when binding changes
```

## Dependencies

- **DOTween** - Animation support
- **Unity Input System** - Input handling
- **Audio Module** - Audio playback
- **Slog** - Logging

## Integration Points

- **Audio** - AudioButtonComponent uses AudioModule
- **Input** - Input components use InputModule
- **Settings** - Components integrate with SettingsModule
- **Navigation** - Components used in views

## Design Patterns

- **Component Pattern** - Reusable UI components
- **Decorator Pattern** - Components enhance Unity UI elements
- **Observer Pattern** - Reactive UI updates

## Test Coverage

**Status**: ⚠️ **ZERO TESTS** (See TESTS_GOALS.md)

Critical untested files:
- `AudioButtonComponent.cs`
- `BindingComponentElement.cs`
- `CompositeBindingHelper.cs`
- All other component files

## Known Issues

- Some hardcoded UI behavior (consider making configurable)
- Component initialization could be more robust
- Performance optimizations for animations could be improved

