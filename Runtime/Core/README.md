@page core_system Core System

@brief Plugin Architecture and Dependency Injection

# Core System - Plugin Architecture and Dependency Injection

The foundation of the MToolKit framework, providing dependency injection, plugin architecture, and core abstractions.

## Purpose

The Core module provides:
- **Plugin Architecture** - Abstract base classes for building game plugins
- **Dependency Injection** - VContainer integration and service registration
- **Runtime Host** - Game runtime lifecycle management
- **Configuration** - Global constants and plugin configuration

## Structure

```
Core/
├── Abstractions/     # Base classes for plugins and runtime systems
├── Config/          # Configuration ScriptableObjects
├── Host/            # Runtime host and game root management
├── Interfaces/      # Core interfaces (IGamePlugin, IRuntimePlugin, etc.)
├── Singletons/      # Global singletons (GlobalConstants, GlobalConfigLoader)
└── README.md        # This file
```

## Key Files

### Plugins & Lifecycle

- **`AbstractGamePlugin.cs`** - Base class for all game plugins
- **`AbstractRuntimePlugin.cs`** - Plugin with runtime initialization support
- **`IRuntimePlugin.cs`** - Interface for plugins that need runtime initialization
- **`IGamePlugin.cs`** - Core interface for all plugins
- **`IDependencyDeclaration.cs`** - Interface for declaring plugin dependencies

### Configuration

- **`GlobalConstants.cs`** - Singleton providing global constants
- **`GlobalConstantsConfig.cs`** - ScriptableObject configuration for constants
- **`PluginConfigAsset.cs`** - Base class for plugin-specific configs
- **`GlobalPluginConfigAsset.cs`** - Global plugin configuration

### Runtime Management

- **`GameRoot.cs`** - Main game root MonoBehaviour
- **`GameRuntime.cs`** - Core runtime implementation
- **`GameRuntimeHost.cs`** - Runtime host wrapper
- **`IGameRuntime.cs`** - Runtime interface

**Note**: Message bus functionality has been moved to the [MessageBus Module](../MessageBus/README.md).

### Dependency Resolution

- **`PluginDependencyResolver.cs`** - Automatic topological sorting of plugin dependencies
- **`PluginRegistry.cs`** - Registry for tracking loaded plugins

## Usage Examples

### Creating a Plugin

```csharp
public sealed class MyPlugin : AbstractGamePlugin, IRuntimePlugin
{
    [SerializeField, Required] private MyConfig config;

    public override void Register(IContainerBuilder builder)
    {
        builder.RegisterInstance(config).As<MyConfig>();
        builder.Register<IMyService, MyService>(Lifetime.Singleton);
    }

    public void PerformRuntimeInitialization(IObjectResolver resolver)
    {
        var service = resolver.Resolve<IMyService>();
        service.Initialize();
    }
}
```

### Declaring Dependencies

```csharp
public sealed class DependentPlugin : AbstractGamePlugin, IDependencyDeclaration, IRuntimePlugin
{
    public IEnumerable<Type> RequiredServices => new[] { typeof(IMyService) };
    public IEnumerable<Type> OptionalServices => Array.Empty<Type>();
    
    // ... implementation
}
```

### Message Publishing

See [MessageBus Module](../MessageBus/README.md) for message publishing examples.

## Dependencies

- **VContainer** - Dependency injection framework
- **MessagePipe** - Async messaging system
- **Sirenix Odin Inspector** - Editor enhancements
- **Serilog** - Structured logging (via Slog module)

## Integration Points

All other MToolKit modules depend on Core:
- Plugins register themselves via `Register()`
- Services are resolved through VContainer
- Inter-module communication uses MessagePipe
- Configuration flows through GlobalConstants

## Design Patterns

- **Plugin Pattern** - Extensible plugin architecture
- **Dependency Injection** - Constructor and property injection
- **Pub/Sub** - Event-driven messaging
- **Singleton Pattern** - GlobalConstants instance
- **Factory Pattern** - Service registration and resolution
- **Null Object Pattern** - Used in Navigation module

