@page music_system Music System

@brief Cross-Scene Music with Crossfading

# Music System - Cross-Scene Music with Crossfading

Cross-scene music playback with crossfading, looping, and mixer integration.

## Purpose

The Music module provides:
- **Persistent Music** - Music that persists across scenes
- **Crossfading** - Smooth transitions between tracks
- **Looped Playback** - Automatic music looping
- **Mixer Integration** - Volume control via Unity AudioMixer
- **Singleton Manager** - Global music manager instance

## Structure

```
Music/
├── MusicManager.cs             # Singleton music manager
└── README.md                   # This file
```

## Key Files

### Core Manager

- **`MusicManager.cs`** - Singleton MonoBehaviour for music playback

## Usage Examples

### Playing Music with Crossfade

```csharp
// Get music manager
var musicManager = MusicManager.Instance;

// Play music with default crossfade (2 seconds)
musicManager.PlayMusic(audioClip);

// Play with custom crossfade duration
musicManager.PlayMusic(audioClip, crossfadeDuration: 3f);
```

### Music Manager Setup

```csharp
// MusicManager is a singleton MonoBehaviour
// Create it in your initial scene or GameRoot
// Configure in inspector:
// - AudioMixerGroup for music
// - Crossfade settings (optional)
```

### Stopping Music

```csharp
// Stop music immediately
musicManager.StopMusic();

// Stop music with fade out
musicManager.StopMusic(fadeDuration: 2f);
```

### Checking Playback State

```csharp
// Check if music is playing
if (musicManager.IsPlaying)
{
    Debug.Log("Music is currently playing");
}
```

## Dependencies

- **Audio Module** - Uses mixer integration from Audio module
- **Unity Audio** - AudioSource components
- **DOTween** - Crossfade animation support
- **Serilog** - Structured logging

## Integration Points

- **Audio** - Shares mixer groups with audio service
- **Settings** - Volume controlled via audio settings
- **Bootstrapper** - Music manager created during bootstrap

## Design Patterns

- **Singleton Pattern** - Single music manager instance
- **Dual AudioSource Pattern** - Two sources for crossfading
- **Crossfade Pattern** - Smooth audio transitions

## Implementation Details

### Dual AudioSource Approach

MusicManager uses two AudioSource components for seamless crossfading:

```csharp
AudioSource audioSource1;  // Current playing source
AudioSource audioSource2;  // Next source for crossfade

// Crossfade logic:
// 1. Play new clip on inactive source
// 2. Fade out current source
// 3. Fade in new source
// 4. Swap references
```

### Crossfade Algorithm

```csharp
// Fade from current volume to 0
fadeOut.volume = Lerp(currentVolume, 0, t);

// Fade from 0 to current volume
fadeIn.volume = Lerp(0, currentVolume, t);
```

## Test Coverage

**Status**: Unknown (no test files found)

Consider adding:
- Crossfade algorithm tests
- Loop playback tests
- Singleton instance tests
- Volume control tests

## Known Issues

- Consider adding music queue for playlist support
- Volume normalization could be improved
- Memory management for audio clips could be optimized

## Future Enhancements

- Music queue/playlist system
- Shuffle and repeat modes
- Music sync for gameplay events
- Dynamic music intensity adjustment

