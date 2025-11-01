@page bootstrapper_system Bootstrapper

@brief Game Initialization with Dependency Preloading

# Bootstrapper - Game Initialization with Dependency Preloading

Game bootstrapping with dependency preloading, timeout handling, and progress indication.

## Purpose

The Bootstrapper module provides:
- **Dependency Preloading** - Preload all dependencies before game starts
- **Timeout Handling** - Handle dependency initialization timeouts
- **Progress Indication** - Show progress to user during bootstrapping
- **Bootstrap Lifecycle** - Controlled game startup sequence

## Structure

```
Bootstrapper/
├── Bootstrapper.cs              # Main bootstrapper MonoBehaviour
├── IGameLoader.cs               # Game loader interface
└── GameLoader.cs                # Game loader implementation
```

## Key Files

### Core System

- **`Bootstrapper.cs`** - Main bootstrapper with progress tracking
- **`IGameLoader.cs`** - Game loader interface
- **`GameLoader.cs`** - Game loading implementation

## Usage Examples

### Basic Bootstrapper Setup

```csharp
// Attach Bootstrapper MonoBehaviour to scene GameObject
// Configure in inspector:
// - Timeout duration
// - Progress callback
// - Dependency requirements
```

### Custom Game Loader

```csharp
public class MyGameLoader : MonoBehaviour, IGameLoader
{
    public async UniTask LoadAsync(CancellationToken ct)
    {
        // Custom loading logic
        await LoadGameDataAsync();
        await LoadAssetsAsync();
    }
}
```

### Bootstrap Events

```csharp
// Bootstrapper exposes reactive properties
var bootstrapper = FindFirstObjectByType<Bootstrapper>();

// Subscribe to bootstrap state
bootstrapper.Bootstrapped.Property.Subscribe(isBootstrapped =>
{
    if (isBootstrapped)
    {
        Debug.Log("Bootstrap complete!");
    }
});

// Subscribe to loading state
bootstrapper.IsLoading.Property.Subscribe(isLoading =>
{
    if (isLoading)
    {
        ShowLoadingScreen();
    }
});
```

## Dependencies

- **VContainer** - Dependency injection
- **R3** - Reactive properties for progress
- **Serilog** - Structured logging
- **Core** - Uses Core services

## Integration Points

- **Core** - Coordinates with GameRoot and plugins
- **Navigation** - Shows loading screen views
- **Analytics** - Tracks bootstrap metrics
- **Assets** - Preloads game assets

## Design Patterns

- **Lifecycle Pattern** - Controlled bootstrap lifecycle
- **Progress Pattern** - Progress tracking and reporting
- **Timeout Pattern** - Graceful timeout handling
- **Reactive Pattern** - Reactive bootstrap state

## Test Coverage

**Status**: Unknown (no test files found)

Consider adding:
- Bootstrap lifecycle tests
- Timeout handling tests
- Progress tracking tests
- Dependency preloading tests

## Known Issues

- Timeout handling could be more sophisticated
- Progress indication could be more granular
- Consider adding skip bootstrap option for development

## Bootstrap Flow

```
1. SceneLoaded
2. Bootstrapper starts
3. Wait for non-UI dependencies
4. Wait for user ready (UI input)
5. Load game content
6. Bootstrap complete
7. Start game
```

## Configuration

```csharp
[Inspector]
public class BootstrapperConfig
{
    public float timeout = 30f;
    public bool showProgress = true;
    public List<GameObject> preloadAssets;
}
```

