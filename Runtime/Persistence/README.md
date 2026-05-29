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
├── Migration/         # Forward-only schema-versioned save migration
├── Prewarm/           # Pre-load header check (version / hash / mod diff)
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

## Save Migration & Schema Versioning

The `Migration/` directory provides forward-only, schema-versioned save migration. Every persisted DTO carries a stamped `SaveVersion` (int) and `SaveSchemaHash` (a deterministic hash of its serialized shape); a per-domain migrator decides what happens when a loaded save's stamp doesn't match the current build.

### Core types

- **`ISchemaStampedSaveData`** — marker interface your save DTO implements (`SaveVersion`, `SaveSchemaHash`). The framework stamps these on save and reads them on load.
- **`ForwardMigrator<T>`** — abstract base your per-domain migrator subclasses. You declare the schema surface (`SchemaRoots`, `NamespacePrefixes`) and implement `EnsureContainers` (null-coalesce collections), `Normalize` (clamp / repair; idempotent; returns true when it changed something), and — once you support older versions — `Migrate(data, oldVersion, oldHash)`.
- **`SchemaHashWalker`** — computes the deterministic schema hash from the DTO graph. Hash drift is what flags "this save's shape no longer matches the code."
- **`SavePrewarmChecker`** (`Prewarm/`) — reads only the save header (version + per-domain hashes + mod manifest) *before* a full load, so the UI can warn the player before committing to a load.
- **`MigrationOutcome`** — `None` (exact match), `Migrated` (forward-migrated within `[floor, current)` or editor-side legacy-shape repair), `TruncatedBestEffort` (best-effort load of a newer / below-floor / hash-drifted *stamped* save, with a `TruncationReport`), `RefusedFatal` (only an unstamped save — no hash to validate; per ADR-0016 stamped saves are never refused).

### The compatibility floor (ADR-0016)

`MinimumSupportedVersion` is the **validated-transform boundary** — *not* a refusal boundary. It defaults to `CurrentSchemaVersion`. At or above it, a version-specific `Migrate` body runs. **Below it, a stamped save still loads best-effort** (`EnsureContainers` + `Normalize` + re-stamp + a `below-floor` `TruncationReport`) — it is **not** refused; it simply skips the (unwritten) version transform. The only `RefusedFatal` path is an **unstamped** save (no `SaveSchemaHash` to validate against) — unless a domain opts into `AllowsLegacyHashlessLoad`.

Adopt *validated* forward migration by **lowering the floor and overriding `Migrate`** when a schema change warrants a golden-tested transform — typically at first real ship, for versions players actually hold. Best-effort-below-floor is the safety net; a written `Migrate` body is the upgrade.

### The per-bump ritual

When you change a persisted schema at or after the floor:

1. Snapshot the OLD shape as a golden fixture (`golden_<domain>_v<OLD>.es3`) via the domain's `[Explicit]` generator test.
2. Bump `CurrentSchemaVersion`.
3. Extend `Migrate(data, oldVersion, oldHash)` to transform the old shape forward.
4. Add a forward-migration golden test asserting the old fixture loads to `Migrated`.
5. Update the pinned hash in the domain's drift test.

### Load dispatch (`MigrateForLoad`, in order)

1. Unstamped (no hash) → `RefusedFatal`, unless `AllowsLegacyHashlessLoad`. (The only refusal path.)
2. Newer build (`loaded > current`) → best-effort load + `newer-build` truncation report → `TruncatedBestEffort`.
3. Below floor (`loaded < MinimumSupportedVersion`) → best-effort load + `below-floor` truncation report → `TruncatedBestEffort`. No version-specific `Migrate` runs.
4. Within `[floor, current)` → `Migrate` → `Migrated`.
5. Exact match → `None` (or `Migrated` if `Normalize` repaired a legacy shape).
6. Hash drift, editor build → re-normalize + re-stamp → `Migrated`.
7. Hash drift, shipping build → best-effort load + `hash-drift-shipping` truncation report → `TruncatedBestEffort`.

### Testing discipline

Each domain pins its schema with a **drift test** (asserts `CurrentSchemaVersion` + `CurrentSchemaHash`) and a **golden-corpus test** (loads every `golden_<domain>_v*.es3` and asserts the outcome). The golden generator is `[Explicit]` so it never runs in CI — invoke it manually when intentionally rotating a schema, and commit the produced fixture.

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

