@page error_system Error System

@brief Global Error Handling with Graceful Degradation

<!-- usewhen: You need to show a fatal/dev error ON SCREEN (e.g. a boot/DI failure) instead of only logging to the console -->
<!-- entrypoint: GlobalAsyncMessageBroker.Publish(new ErrorRequestMessage("msg", fatal: true)) -->

# Error System - Global Error Handling with Graceful Degradation

Global error handling with graceful degradation and user-friendly error messages.

## Purpose

The Error System module provides:
- **Global Error Handling** - Centralized error management
- **Graceful Degradation** - Error recovery without crashes
- **User-Friendly Messages** - Clear error messages for users
- **Analytics Integration** - Error tracking via analytics
- **MessagePipe Integration** - Decoupled error reporting

## Structure

```
ErrorSystem/
├── ErrorService.cs                 # Main error service
├── IErrorService.cs                # Error service interface
├── ErrorSystemPlugin.cs            # Plugin registration
├── ErrorRequestMessage.cs          # MessagePipe message type
├── ErrorView.cs                    # Error UI view
└── README.md                       # This file
```

## Key Files

### Core Service

- **`ErrorService.cs`** - Main error handling service
- **`IErrorService.cs`** - Error service interface
- **`ErrorSystemPlugin.cs`** - Plugin registration

### Messages

- **`ErrorRequestMessage.cs`** - MessagePipe error message type

### Views

- **`ErrorView.cs`** - Error display UI view

## Usage Examples

### Reporting Errors

```csharp
// Get error service from DI
var errorService = resolver.Resolve<IErrorService>();

// Report an error
await errorService.ReportErrorAsync(
    message: "Save failed: Disk full",
    severity: ErrorSeverity.Warning
);
```

### Using MessagePipe

```csharp
// Get publisher
var publisher = GameMessageBroker.GetPublisher<ErrorRequestMessage>();

// Publish error
publisher?.Publish(new ErrorRequestMessage(
    message: "Network connection lost",
    severity: ErrorSeverity.Error
));
```

### Error View Integration

```csharp
// ErrorView automatically subscribes to error messages
// Shows errors to user in friendly format
// Provides options for recovery when available
```

### Custom Error Recovery

```csharp
// Register custom error handlers
errorService.RegisterRecoveryAction(
    errorType: "SaveError",
    recoveryAction: async () =>
    {
        // Attempt to save to alternative location
        await SaveToBackupAsync();
    }
);
```

## Dependencies

- **MessagePipe** - Error message publishing
- **VContainer** - Dependency injection
- **Serilog** - Structured logging (error logging)
- **Analytics** - Error tracking

## Integration Points

- **Core** - Uses MessageBus for error events
- **Analytics** - Errors tracked via analytics
- **Navigation** - ErrorView integrates with navigation system

## Design Patterns

- **Service Pattern** - Error service for centralized error handling
- **Pub/Sub** - MessagePipe for decoupled error reporting
- **Graceful Degradation** - Error recovery without crashes

## Test Coverage

**Status**: Unknown (no test files found)

Consider adding:
- Error reporting tests
- Error recovery tests
- Error view tests
- Analytics integration tests

## Known Issues

- Automatic retry mechanisms not yet implemented (Phase 2)
- Error recovery could be more robust
- Error queue for rapid error handling not yet implemented

## Future Enhancements (Phase 2)

- **Automatic Retry** - Retry operations before showing errors
- **Error Recovery** - Automatic fallback options
- **Error Queue** - Queue multiple rapid errors gracefully
- **Error Reporting** - Enhanced error reporting to backend
- **Error Analytics** - Detailed error analytics

## Design Philosophy

The Error System follows these principles:
1. **Never crash** - Always attempt graceful degradation
2. **User clarity** - Always show clear, actionable errors
3. **Analytics first** - Track all errors for debugging
4. **Recovery over failure** - Always attempt to recover

