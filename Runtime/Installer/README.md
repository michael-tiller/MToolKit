@page installer_system Global Installer

@brief Dependency Injection Configuration

# Global Installer - Dependency Injection Configuration

Dependency injection configuration and service registration for MToolKit.

## Purpose

The Installer module provides:
- **Global Service Registration** - Register services used across all scenes
- **Plugin Integration** - Automatic plugin discovery and registration
- **Configuration Management** - ScriptableObject configuration registration
- **Lifetime Management** - Singleton and transient service lifetimes
- **Dependency Resolution** - Automatic dependency resolution

## Structure

```
Installer/
├── GlobalInstaller.cs         # Main global installer
└── README.md                  # This file
```

## Key Files

- **`GlobalInstaller.cs`** - Main global installer with VContainer integration

## Purpose

The `GlobalInstaller` is a Unity `LifetimeScope` that:
1. Registers global services via VContainer
2. Discovers and registers plugins
3. Configures message brokers
4. Sets up service lifetimes
5. Persists via DontDestroyOnLoad

## Usage

### Basic Setup

```csharp
// Attach GlobalInstaller MonoBehaviour to scene
// Configure in inspector:
// - Input Action Asset
// - Global Plugin Config
// - Other global settings
```

### Plugin Discovery

```csharp
// GlobalInstaller automatically discovers plugins
// Plugins must:
// 1. Inherit from AbstractGamePlugin
// 2. Be in the scene
// 3. Implement IRuntimePlugin if needed
```

### Service Registration

```csharp
// GlobalInstaller registers:
// - Message brokers (NavigationRequestMessage, ErrorRequestMessage, etc.)
// - Common services (Settings, Audio, Analytics, etc.)
// - Plugin instances
// - Configuration assets
```

### Extending GlobalInstaller

```csharp
public class MyGlobalInstaller : GlobalInstaller
{
    protected override void Configure(IContainerBuilder builder)
    {
        base.Configure(builder);
        
        // Add your services
        builder.Register<IMyService, MyService>(Lifetime.Singleton);
    }
}
```

## Integration Points

### Registered Services

The GlobalInstaller registers:
- **SettingsSystem** - Settings management
- **IS3SaveService** - Save system
- **InputService** - Input handling
- **AudioService** - Audio playback
- **AnalyticsService** - Analytics tracking
- **ErrorService** - Error handling
- **NavigationService** - UI navigation

### Message Brokers

GlobalInstaller sets up MessagePipe brokers for:
- `NavigationRequestMessage`
- `ErrorRequestMessage`
- `PauseToggledMessage`
- `SceneLoadedMessage`
- And more...

### Plugin Instances

GlobalInstaller tracks plugin instances:
- `NavigationPluginInstance`
- `SettingsPluginInstance`
- `GameSavePluginInstance`
- `InputRebinderPluginInstance`
- `AnalyticsPluginInstance`
- `ErrorSystemPluginInstance`

## Dependencies

- **VContainer** - DI framework
- **MessagePipe** - Messaging framework
- **Serilog** - Structured logging
- **Unity Input System** - Input handling
- **Core** - Plugin system

## Test Coverage

**Status**: ✅ **WELL TESTED**

Test files:
- `GlobalInstallerTests.cs` - Main installer tests
- `GlobalInstallerPropertyTests.cs` - Property-based tests
- `GlobalConfigLoaderTests.cs` - Configuration loader tests

## Design Patterns

- **Facade Pattern** - GlobalInstaller as facade over DI configuration
- **Registry Pattern** - Plugin registry for tracking plugins
- **Factory Pattern** - Service factory registration

## Known Issues

- Some services could be moved to scene-specific installers
- Plugin discovery could be more sophisticated
- Consider plugin hot-reloading for development

## Future Enhancements

- Scene-specific installer support
- Hot-reload plugin discovery
- Configuration validation
- Service graph visualization

