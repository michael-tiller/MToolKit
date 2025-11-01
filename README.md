# MToolKit Runtime Modules

Core framework modules for production Unity games. Each module is documented with its own README.

## Overview

MToolKit provides essential systems required for shipped game titles. It's a modular framework built on dependency injection, reactive programming, and message-driven architecture.

## Modules

### Core System
- **Core** - Dependency injection, plugin architecture, configuration
- **MessageBus** - Decoupled message publishing/subscription
- **Bootstrapper** - Game bootstrapping with dependency preloading
- **Installer** - DI registration and service configuration

### UI & Interaction
- **Navigation** - View management, modals, subviews, canvas management
- **Components** - Reusable UI components (buttons, inputs, spinners, etc.)
- **Localization** - Multi-language support via Unity Localization

### Data & Persistence
- **Persistence** - Save system with ES3 integration, profiles, cloud backup
- **Settings** - Reactive settings system with UI binding

### Media & Content
- **Audio** - Audio playback service with mixer integration
- **Music** - Cross-scene music playback with crossfading
- **AssetLoader** - Addressables integration with parallel loading

### Services
- **Input** - Input abstraction with rebinding and device fallback
- **Analytics** - GameAnalytics integration with event tracking
- **ErrorSystem** - Global error handling with graceful degradation

### Infrastructure
- **Slog** - Structured logging with Serilog
- **Utilities** - Helper classes, extensions, and data structures

## Quick Start

### 1. Bootstrap Your Game

```csharp
// Use the Bootstrapper module to initialize everything
var bootstrapper = FindFirstObjectByType<Bootstrapper>();
bootstrapper.InitializeAsync();
```

### 2. Register Services

```csharp
// Create your global installer
public class MyGlobalInstaller : GlobalInstaller
{
    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);
        
        // Register your services
        builder.Register<IMyService, MyService>(Lifetime.Singleton);
    }
}
```

### 3. Use Services

```csharp
// Resolve services from dependency injection
var settingsSystem = resolver.Resolve<ISettingsSystem>();
var audioService = resolver.Resolve<IAudioService>();
var analytics = resolver.Resolve<IAnalyticsService>();
```

## Architecture Principles

1. **Loose Coupling** - Modules are independent and loosely coupled
2. **Dependency Injection** - VContainer-based DI for all services
3. **Reactive Programming** - R3 for reactive state management
4. **Event-Driven** - MessagePipe for decoupled communication
5. **Plugin Architecture** - Extensible plugin-based design

## Module Documentation

Each module has its own README with detailed documentation:

- @ref core_system "Core System"
- @ref messagebus_system "MessageBus System"
- @ref analytics_system "Analytics System"
- @ref asset_loader "Asset Loader"
- @ref audio_system "Audio System"
- @ref bootstrapper_system "Bootstrapper"
- @ref ui_components "UI Components"
- @ref error_system "Error System"
- @ref input_system "Input System"
- @ref installer_system "Global Installer"
- @ref localization_system "Localization System"
- @ref music_system "Music System"
- @ref navigation_system "Navigation System"
- @ref persistence_system "Persistence System"
- @ref settings_system "Settings System"
- @ref slog_system "Slog System"
- @ref utilities_system "Utilities"

## Dependencies

- **VContainer** - Dependency injection
- **MessagePipe** - Async messaging
- **R3** - Reactive properties
- **Serilog** - Structured logging
- **ES3** - Save system
- **DOTween Pro** - Animation
- **Sirenix Odin Inspector** - Editor enhancements
- **Unity Input System** - Input handling
- **Unity Addressables** - Asset loading
- **Unity Localization** - Localization
- **GameAnalytics** - Analytics

## Current Status

See [GOALS.md](../../../GOALS.md) for current status and progress.

**Status**: 15/17 core systems complete (93%)
- ✅ Complete: 15 systems
- 🚧 In Progress: 2 systems (Settings 95%, Accessibility)
- ❌ Missing: 0 critical gaps

## Getting Help

1. Check the module-specific README
2. See [GOALS.md](../../../GOALS.md) for roadmap
3. See [TESTS_GOALS.md](../../../TESTS_GOALS.md) for testing status
4. See [cursorrules.md](../../../cursorrules.md) for architecture guidelines

## Contributing

When adding new modules:
1. Add module directory under Runtime/
2. Create module-specific README.md
3. Register module with DI system
4. Add integration tests
5. Update GOALS.md

## License

MToolKit - Unity Production Framework
See LICENSE file for details.

