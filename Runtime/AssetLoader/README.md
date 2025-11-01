@page asset_loader Asset Loader

@brief Addressables Integration with Parallel Loading

# Asset Loader - Addressables Integration with Parallel Loading

Asset loading system with Addressables integration, parallel loading, and caching.

## Purpose

The AssetLoader module provides:
- **Addressables Integration** - Unity Addressables asset loading
- **Parallel Loading** - Concurrent asset loading for performance
- **Lifecycle Management** - Proper asset loading/unloading
- **Caching** - Asset caching system
- **Dependency Tracking** - Track asset dependencies
- **Progress Tracking** - UI progress indication for asset loading

## Structure

```
AssetLoader/
├── IAssetLoader.cs                # Asset loader interface
├── AddressablesAssetLoader.cs     # Addressables implementation
├── ResourcesAssetLoader.cs        # Resources implementation (fallback)
├── IContentLoaderService.cs       # Content loader service interface
├── ContentLoaderService.cs        # Content loader implementation
├── IRuntimeAssetService.cs        # Runtime asset service interface
├── RuntimeAssetService.cs         # Runtime asset service
├── RuntimeContentManifest.cs      # Content manifest
├── AssetReferenceBase.cs          # Base asset reference
├── AssetReferences.cs              # Asset reference definitions
└── README.md                       # This file
```

## Key Files

### Asset Loaders

- **`IAssetLoader.cs`** - Asset loader abstraction
- **`AddressablesAssetLoader.cs`** - Addressables implementation
- **`ResourcesAssetLoader.cs`** - Resources fallback implementation

### Content Loading

- **`IContentLoaderService.cs`** - Content loader service interface
- **`ContentLoaderService.cs`** - Content loading coordination
- **`IRuntimeAssetService.cs`** - Runtime asset service interface
- **`RuntimeAssetService.cs`** - Runtime asset service implementation

### Asset References

- **`AssetReferenceBase.cs`** - Base asset reference class
- **`AssetReferences.cs`** - Typed asset references

### Manifest

- **`RuntimeContentManifest.cs`** - Content manifest for asset tracking

## Usage Examples

### Loading Assets

```csharp
// Get asset loader from DI
var assetLoader = resolver.Resolve<IAssetLoader>();

// Load asset asynchronously
var asset = await assetLoader.LoadAssetAsync<GameObject>("PlayerPrefab");

// Load asset with dependencies
var result = await assetLoader.LoadAssetAsync<GameObject>(
    "PlayerPrefab", 
    trackDependencies: true
);
```

### Loading Multiple Assets in Parallel

```csharp
// Load multiple assets concurrently
var tasks = new[]
{
    assetLoader.LoadAssetAsync<GameObject>("PlayerPrefab"),
    assetLoader.LoadAssetAsync<AudioClip>("BackgroundMusic"),
    assetLoader.LoadAssetAsync<Texture2D>("SplashImage")
};

await UniTask.WhenAll(tasks);
```

### Content Loading Service

```csharp
// Get content loader
var contentLoader = resolver.Resolve<IContentLoaderService>();

// Load content with progress tracking
var progress = new Progress<float>(p => Debug.Log($"Loading: {p * 100}%"));
await contentLoader.LoadContentAsync(contentManifest, progress);
```

### Asset Reference Usage

```csharp
// Define typed asset references
[Serializable]
public class MyAssetReferences : AssetReferences
{
    public AssetReferenceGameObject PlayerPrefab;
    public AssetReferenceAudioClip BackgroundMusic;
}

// Use asset references
var playerRef = myAssetReferences.PlayerPrefab;
var player = await playerRef.LoadAssetAsync<GameObject>();
```

## Dependencies

- **Unity Addressables** - Asset loading system
- **VContainer** - Dependency injection
- **R3** - Reactive properties for progress
- **Serilog** - Structured logging

## Integration Points

- **Core** - Plugin registration and lifecycle
- **Navigation** - Asset loading for view instantiation
- **Audio** - Audio clip loading
- **Analytics** - Asset loading metrics tracking

## Design Patterns

- **Strategy Pattern** - Different asset loading strategies
- **Factory Pattern** - Asset loader factory
- **Cache Pattern** - Asset caching system
- **Observer Pattern** - Progress tracking

## Test Coverage

**Status**: Unknown (no test files found)

Consider adding:
- Asset loader tests
- Parallel loading tests
- Caching tests
- Dependency tracking tests

## Known Issues

- Resources fallback needs more testing
- Addressables catalog management could be improved
- Asset unloading could be more aggressive

## Future Enhancements

- Asset streaming for large assets
- Hot reload support for content updates
- Asset compression strategies
- Platform-specific asset variants

