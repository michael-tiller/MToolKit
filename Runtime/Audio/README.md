@page audio_system Audio System

@brief Managed Playback with Mixer Integration

# Audio System - Managed Playback with Mixer Integration

Audio playback service with mixer integration, volume persistence, and settings integration.

## Purpose

The Audio module provides:
- **Audio Playback** - Managed audio playback service
- **Mixer Integration** - Unity AudioMixer integration
- **Volume Persistence** - Automatic volume setting persistence
- **Settings Integration** - Reactive audio settings
- **Platform Compatibility** - Cross-platform audio support

## Structure

```
Audio/
├── AudioService.cs            # Main audio service implementation
├── IAudioService.cs           # Audio service interface
├── AudioPlugin.cs             # Plugin registration
├── AudioConfig.cs             # Configuration ScriptableObject
└── README.md                  # This file
```

## Key Files

### Core Service

- **`AudioService.cs`** - Main audio playback service
- **`IAudioService.cs`** - Audio service interface
- **`AudioPlugin.cs`** - Plugin registration
- **`AudioConfig.cs`** - Audio configuration

## Usage Examples

### Playing Audio Clips

```csharp
// Get audio service from DI
var audioService = resolver.Resolve<IAudioService>();

// Play a one-shot sound
await audioService.PlayOneShotAsync(clip);

// Play a 3D positioned sound
await audioService.PlayOneShotAsync(clip, position);
```

### Mixer Integration

```csharp
// AudioService automatically subscribes to volume changes
// Changes are applied to mixer parameters
// Mixer groups must be configured in AudioConfig
```

### Configuration

```csharp
// Create AudioConfig ScriptableObject
// Configure:
// - MasterMixerGroup
// - VolumeSfxGroup
// - VolumeMusicGroup
// - etc.
```

## Dependencies

- **Unity Audio** - Unity's audio system
- **Settings** - Integrates with Settings module for volume control
- **VContainer** - Dependency injection
- **Serilog** - Structured logging

## Integration Points

- **Settings** - Volume settings managed through Settings module
- **Music** - Music playback uses audio service
- **Components** - AudioButtonComponent uses audio service

## Design Patterns

- **Service Pattern** - Audio service for centralized audio management
- **Facade Pattern** - Simplified audio API over Unity's audio system
- **Reactive Pattern** - Reactive volume setting integration

## Related Modules

- **Music Module** - See `Music/README.md` for music playback
- **Components Module** - See AudioButtonComponent for UI sounds
- **Settings Module** - See Settings/Audio/ for volume settings

## Test Coverage

**Status**: Unknown (no test files found)

Consider adding:
- Service initialization tests
- Playback tests
- Mixer integration tests
- Volume persistence tests

## Known Issues

- Consider adding audio source pooling for better performance
- 3D audio positioning could be enhanced
- Audio buffer management could be optimized

