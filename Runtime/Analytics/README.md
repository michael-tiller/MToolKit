@page analytics_system Analytics System

@brief GameAnalytics Integration and Event Tracking

# Analytics System - GameAnalytics Integration and Event Tracking

Analytics integration with GameAnalytics backend, event tracking, revenue tracking, and consent management.

## Purpose

The Analytics module provides:
- **GameAnalytics Integration** - Backend analytics service integration
- **Event Tracking** - Custom event tracking with parameters
- **Revenue Tracking** - In-app purchase revenue tracking
- **Consent Management** - GDPR/ATT consent handling
- **Session Management** - Session lifecycle tracking
- **MessagePipe Integration** - Decoupled analytics via messaging

## Structure

```
Analytics/
├── IAnalyticsService.cs          # Main analytics service interface
├── AnalyticsService.cs            # Facade service implementation
├── AnalyticsPlugin.cs             # Plugin registration
├── AnalyticsConfig.cs             # Configuration
├── AnalyticsEventBridge.cs        # MessagePipe integration
├── AnalyticsEvents.cs             # MessagePipe event types
├── GameAnalyticsBackend.cs        # GameAnalytics backend implementation
├── IAnalyticsBackend.cs           # Backend interface
├── ConsentPanel.cs                # Consent UI panel
├── EnvironmentLoader.cs            # Environment variable loading
└── README.md                      # This file
```

## Key Files

### Core Service

- **`IAnalyticsService.cs`** - Main analytics service interface
- **`AnalyticsService.cs`** - Facade implementation over backend
- **`AnalyticsPlugin.cs`** - Plugin registration with consent handling
- **`AnalyticsConfig.cs`** - Configuration ScriptableObject

### Backend

- **`GameAnalyticsBackend.cs`** - GameAnalytics SDK integration
- **`IAnalyticsBackend.cs`** - Backend abstraction interface

### MessagePipe Integration

- **`AnalyticsEventBridge.cs`** - MessagePipe event bridge
- **`AnalyticsEvents.cs`** - MessagePipe message types

### Consent & Privacy

- **`ConsentPanel.cs`** - GDPR/ATT consent UI
- **`EnvironmentLoader.cs`** - Environment variable loader

## Usage Examples

### Tracking Events

```csharp
// Get analytics service from DI
var analytics = resolver.Resolve<IAnalyticsService>();

// Track simple event
analytics.TrackEvent("GameStarted");

// Track event with parameters
var parameters = new Dictionary<string, object>
{
    { "level", 5 },
    { "difficulty", "hard" }
};
analytics.TrackEvent("LevelStarted", parameters);
```

### Tracking Revenue

```csharp
// Track in-app purchase
analytics.TrackRevenue(
    currency: "USD",
    amount: 9.99,
    itemType: "weapon",
    itemId: "sword_legendary"
);
```

### Tracking Progression

```csharp
// Track progression
analytics.TrackProgression(
    progression1: "Level01",
    progression2: "boss_encounter",
    score: 85000
);
```

### Error Tracking

```csharp
// Track error
analytics.TrackError("Save failed", "error");
```

### Using MessagePipe Integration

```csharp
// Get publisher
var publisher = GameMessageBroker.GetPublisher<AnalyticsGameEvent>();

// Publish analytics event
publisher?.Publish(new AnalyticsGameEvent(
    name: "PlayerDied",
    @params: new Dictionary<string, object> { { "cause", "fall" } }
));
```

### Consent Management

```csharp
// Get analytics service
var analytics = resolver.Resolve<IAnalyticsService>();

// Set consent
analytics.SetConsent(analyticsEnabled: true, adsEnabled: false);
```

## Configuration

### Environment Variables

Create a `.env` file in your project root:

```env
GA_GAME_KEY=your_game_key_here
GA_SECRET_KEY=your_secret_key_here
```

**Important**: Never commit `.env` files with real keys!

### Creating Analytics Config

1. Create `AnalyticsConfig` ScriptableObject
2. Set enable flags
3. Assign to `AnalyticsPlugin` in scene

## Dependencies

- **GameAnalytics** - Analytics SDK
- **MessagePipe** - Event publishing
- **VContainer** - Dependency injection
- **Serilog** - Structured logging
- **Sirenix Odin Inspector** - Editor enhancements

## Integration Points

- **Core** - Uses MessageBus for analytics events
- **Settings** - Consent managed through game settings
- **ErrorSystem** - Errors can be tracked via analytics

## Design Patterns

- **Facade Pattern** - `AnalyticsService` is a facade over backend
- **Strategy Pattern** - `IAnalyticsBackend` allows swapping backends
- **Pub/Sub** - MessagePipe for decoupled analytics
- **Adapter Pattern** - `GameAnalyticsBackend` adapts SDK to interface

## Test Coverage

**Status**: Unknown (no test files in Tests directory)

Consider adding:
- Service initialization tests
- Event tracking tests
- Revenue tracking tests
- Consent management tests

## Known Issues

- No unit tests currently
- Consider adding mock backend for testing
- Revenue tracking could be more robust

## Security Notes

- **NEVER** commit real analytics keys to source control
- Use environment variables for keys
- Add `.env` to `.gitignore`
- Use secure environment variable injection in CI/CD
