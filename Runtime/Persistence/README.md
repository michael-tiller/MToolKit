@page persistence_system Persistence System

@brief ES3 Save System with Profile Management

# Persistence System - ES3 Save System with Profile Management

Save system integration with ES3, reactive state management, profile support, and cloud backup.

## Purpose

The Persistence module provides:
- **ES3 Integration** - Easy Save 3 integration for game saves
- **Profile Management** - Multiple save profiles
- **Reactive Save State** - R3 ReactiveProperties for save state tracking
- **Auto-Save** - Automatic saving behavior
- **Domain Controllers** - Organized save domains
- **Cloud Backup** - Platform-specific cloud save support

## Structure

```
Persistence/
├── Abstractions/      # Abstract save behavior classes
├── Enums/             # Save domain enums
├── ES3Integration/    # ES3-specific implementations
├── Interfaces/        # Save system interfaces
└── README.md          # This file
```

## Key Files

### Core System

- **`SaveSystemCoordinator.cs`** - Main save system coordinator
- **`ES3GameSaveSystem.cs`** - ES3 game save system implementation
- **`IES3GameSaveSystem.cs`** - ES3 game save system interface
- **`ES3GameSavePlugin.cs`** - Plugin registration

### ES3 Services

- **`ES3SaveService.cs`** - ES3 save service implementation
- **`IES3Service.cs`** - ES3 service interface
- **`ProfileAwareES3Service.cs`** - Profile-aware save service
- **`ES3SaveConfig.cs`** - ES3 configuration

### Profile Management

- **`ProfileManager.cs`** - Profile management system
- **`IProfileManager.cs`** - Profile manager interface
- **`ProfileMetaData.cs`** - Profile metadata structure

### Domain Controllers

- **`ES3DomainController.cs`** - Domain-specific save controller
- **`ISaveDomainController.cs`** - Save domain controller interface
- **`SaveDomainControllerRegistry.cs`** - Registry for domain controllers

### Auto-Save

- **`ES3AutoSaveBehaviour.cs`** - Automatic save behavior MonoBehaviour

### Abstractions

- **`AbstractSaveBehaviour.cs`** - Abstract base for save behaviors
- **`ISaveable.cs`** - Saveable interface

## Usage Examples

### Creating Saveable Data

```csharp
using MToolKit.Runtime.Persistence;
using System;

[Serializable]
public class PlayerData : ISaveable
{
    public int Level { get; set; }
    public int Score { get; set; }
    public string PlayerName { get; set; }
    
    public string GetSaveKey()
    {
        return "PlayerData";
    }
    
    public Type GetSaveType()
    {
        return typeof(PlayerData);
    }
}
```

### Saving Data

```csharp
// Get save system
var saveSystem = resolver.Resolve<IES3GameSaveSystem>();

// Save data
await saveSystem.SaveAsync("PlayerData", playerData);

// Save with domain
await saveSystem.SaveAsync("PlayerData", playerData, ESaveDomain.Player);
```

### Loading Data

```csharp
// Load data
var playerData = await saveSystem.LoadAsync<PlayerData>("PlayerData");

// Load with default if not exists
var data = await saveSystem.LoadAsync("PlayerData", 
    defaultValue: new PlayerData { Level = 1 });
```

### Using Auto-Save Behavior

```csharp
public class MySaveableComponent : AbstractSaveBehaviour
{
    private MyData data;
    
    protected override async UniTask OnSave()
    {
        var saveSystem = SaveSystemCoordinator.Instance;
        await saveSystem.SaveAsync("MyData", data);
    }
    
    protected override async UniTask OnLoad()
    {
        var saveSystem = SaveSystemCoordinator.Instance;
        data = await saveSystem.LoadAsync<MyData>("MyData", 
            defaultValue: new MyData());
    }
}
```

### Profile Management

```csharp
// Get profile manager
var profileManager = resolver.Resolve<IProfileManager>();

// Create a new profile
await profileManager.CreateProfileAsync("Profile1");

// Switch profiles
await profileManager.SwitchProfileAsync("Profile1");

// Delete a profile
await profileManager.DeleteProfileAsync("Profile1");
```

### Reactive Save State

```csharp
// Get save system
var saveSystem = resolver.Resolve<IES3GameSaveSystem>();

// Subscribe to save state changes
saveSystem.IsSaving.Property.Subscribe(isSaving =>
{
    if (isSaving)
    {
        Debug.Log("Saving...");
    }
    else
    {
        Debug.Log("Save complete");
    }
});
```

## Dependencies

- **ES3** - Easy Save 3 for serialization
- **R3** - Reactive properties for save state
- **VContainer** - Dependency injection
- **Serilog** - Structured logging

## Integration Points

- **Core** - Uses MessageBus for save events
- **Settings** - Settings can be persisted
- **Analytics** - Save events can be tracked

## Design Patterns

- **Repository Pattern** - Save system as data repository
- **Strategy Pattern** - Different save strategies for domains
- **Reactive Pattern** - Reactive save state management
- **Lifecycle Pattern** - AbstractSaveBehaviour for MonoBehaviour lifecycle

## Test Coverage

**Status**: 🔶 **PARTIAL COVERAGE** - Core coordinator tested, but many components missing tests (See TESTS_GOALS.md)

Untested files:
- `ES3GameSaveSystem.cs`
- `ProfileManager.cs`
- `ES3AutoSaveBehaviour.cs`
- Domain controllers

## Documentation

- See `PlatformCloudBackupGuide.md` for cloud backup implementation details

## Known Issues

- Some hardcoded save paths (consider making configurable)
- Save versioning system not yet implemented

