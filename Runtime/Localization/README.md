@page localization_system Localization System

@brief Unity Localization Integration

# Localization System - Unity Localization Integration

Unity Localization package integration for multi-language support.

## Purpose

The Localization module provides:
- **Unity Localization Integration** - Unity's Localization package integration
- **Asset Tables** - Localized asset references
- **String Tables** - Localized text strings
- **Locale Switching** - Runtime language switching
- **Text Refresh** - Automatic UI text updates on locale change

## Structure

```
Localization/
├── LocalizationSystem.cs        # Main localization system
└── LocalizationHelper.cs        # Localization helper utilities
```

## Key Files

### Core System

- **`LocalizationSystem.cs`** - Main localization system
- **`LocalizationHelper.cs`** - Utility methods for localization

## Usage Examples

### Getting Localized Strings

```csharp
// Get localization system
var localization = FindFirstObjectByType<LocalizationSystem>();

// Get localized string
var localizedText = await localization.GetLocalizedStringAsync(
    tableName: "UI",
    key: "MainMenu_StartButton"
);
```

### Switching Locale

```csharp
// Switch to different locale
await localization.SetLocaleAsync("en-US");
await localization.SetLocaleAsync("es-ES");
```

### Getting Available Locales

```csharp
// Get available locales
var locales = await localization.GetAvailableLocalesAsync();

foreach (var locale in locales)
{
    Debug.Log($"Available: {locale}");
}
```

### Using Localization Helper

```csharp
// Get localized string with helper
var text = LocalizationHelper.GetString("UI", "Welcome");
```

## Dependencies

- **Unity Localization** - Unity's Localization package
- **Serilog** - Structured logging
- **R3** - Reactive properties for locale changes

## Integration Points

- **Navigation** - UI text localization
- **Settings** - Locale selection in settings
- **Components** - UI components use localized strings

## Design Patterns

- **Service Pattern** - Localization service for centralized localization
- **Observer Pattern** - Reactive locale change handling

## Test Coverage

**Status**: Unknown (no test files found)

Consider adding:
- Locale switching tests
- String retrieval tests
- Asset table tests
- Text refresh tests

## Known Issues

- Consider adding locale-specific formatting (dates, numbers)
- Text refresh could be more efficient
- Asset variant support could be improved

## Future Enhancements

- Right-to-left language support
- Locale-specific UI layouts
- Automatic locale detection
- Font fallback system

