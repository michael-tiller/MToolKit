@page slog_system Slog System

@brief Structured Logging with Serilog

# Slog System - Structured Logging with Serilog

Structured logging integration with Serilog for MToolKit.

## Purpose

The Slog module provides:
- **Structured Logging** - Serilog integration for structured logs
- **Feature Enrichment** - Automatic feature tagging
- **Method Enrichment** - Automatic method name logging
- **File Logging** - Automatic log file generation
- **Unity Console** - Unity console integration
- **Flush on Quit** - Automatic log flushing on application quit

## Structure

```
Slog/
├── SlogConfig.cs                  # Slog configuration
├── SlogConfigAsset.cs            # Config ScriptableObject
├── SlogLoader.cs                  # Slog initializer
├── Enrichers/                    # Serilog enrichers
│   ├── FeatureEnricher.cs        # Feature enrichment
│   ├── MethodEnricher.cs         # Method name enrichment
│   └── ScalarValueEnricher.cs    # Scalar value enrichment
├── Extensions/                   # Extension methods
│   └── SerilogILoggerExtension.cs
├── FlushSlogOnQuit.cs            # Application quit handler
└── Library/                      # Serilog DLLs
```

## Key Files

### Configuration

- **`SlogConfig.cs`** - Slog configuration class
- **`SlogConfigAsset.cs`** - Config ScriptableObject
- **`SlogLoader.cs`** - Slog initialization system

### Enrichers

- **`FeatureEnricher.cs`** - Adds feature names to log context
- **`MethodEnricher.cs`** - Adds method names to log context
- **`ScalarValueEnricher.cs`** - Adds scalar values to log context

### Utilities

- **`FlushSlogOnQuit.cs`** - Ensures logs are flushed on quit
- **`SerilogILoggerExtension.cs`** - Extension methods for logging

## Usage Examples

### Basic Logging

```csharp
private static readonly Lazy<ILogger> logLazy = 
    new(() => Log.Logger.ForContext<MyClass>().ForFeature("MyFeature"));
private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

// Log information
log.Information("Starting game");

// Log with parameters
log.Information("Player {PlayerName} started level {Level}", 
    playerName, level);

// Log errors
log.Error(exception, "Failed to save game");
```

### Feature Context

```csharp
// Log with feature context
log.ForFeature("Analytics").Information("Event tracked");
log.ForFeature("Audio").Warning("Volume out of range");
```

### Method Context

```csharp
// Log with method name
log.ForMethod().Information("Processing save data");
// Results in: "Processing save data [Method=ProcessSaveData]"
```

### Structured Logging

```csharp
// Structured logging with complex objects
log.Information("Player data: {PlayerData}", 
    new 
    { 
        Level = player.Level, 
        Score = player.Score 
    });
```

## Configuration

### Creating Slog Config

1. Create `SlogConfigAsset` ScriptableObject
2. Configure log levels
3. Configure output sinks (File, Unity Console, etc.)

### Log Levels

- **Verbose** - Detailed diagnostic information
- **Debug** - Debug information
- **Information** - General information
- **Warning** - Warning messages
- **Error** - Error messages
- **Fatal** - Critical errors

## Dependencies

- **Serilog** - Structured logging framework
- **Serilog.Sinks.File** - File logging
- **Serilog.Sinks.Unity3D** - Unity console integration
- **Serilog.Enrichers.GlobalLogContext** - Global context

## Integration Points

- **All Modules** - All modules use Slog for logging
- **Editor Tools** - Editor diagnostics use Slog
- **Error System** - Error logging via Slog

## Design Patterns

- **Service Pattern** - Centralized logging service
- **Enrichment Pattern** - Automatic log context enrichment
- **Facade Pattern** - Simple logging API over Serilog

## Test Coverage

**Status**: Unknown (no test files found)

Consider adding:
- Enricher tests
- Configuration tests
- File logging tests
- Unity console tests

## Known Issues

- Log rotation could be improved
- Performance profiling could be added
- Remote logging could be added

## Best Practices

1. **Always use feature context** - Log.ForFeature("FeatureName")
2. **Use structured parameters** - Pass objects, not formatted strings
3. **Log level appropriately** - Use Information for user-facing events
4. **Include context** - Add player ID, session ID, etc.
5. **Avoid sensitive data** - Never log passwords, API keys, etc.

