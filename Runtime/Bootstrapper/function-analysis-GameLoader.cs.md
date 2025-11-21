# Function Analysis: GameLoader.cs

**File:** `Runtime/Bootstrapper/GameLoader.cs`  
**Date:** 2025-01-27  
**Total Functions Found:** 6

## Summary

`GameLoader` is the concrete implementation of `IGameLoader` that orchestrates game content loading during bootstrap. It loads a data-driven manifest (from Addressables or StreamingAssets fallback) that defines catalogs, labels, and scenes to preload. The class handles Addressables initialization, remote catalog loading, label preloading, and scene loading with fallback mechanisms.

**Key Characteristics:**
- Sealed class implementing `IGameLoader` interface
- Dependency injection via constructor (IContentLoaderService)
- Async/await pattern using UniTask
- Static lazy logger initialization (Serilog)
- Unity Addressables integration
- Fallback mechanisms for manifest and scene loading

## Function Inventory

### Public Interface Methods
- Total: 1
- Complexity: Complex

### Private Instance Methods
- Total: 3
- Complexity: Moderate to Complex

### Private Static Methods
- Total: 1
- Complexity: Complex

### Constructors
- Total: 1
- Complexity: Simple

## Detailed Function Analysis

### 1. Constructor

**Type:** Constructor  
**Location:** Lines 30-33  
**Export:** Internal (public constructor)  
**Complexity:** Simple

#### Signature
```csharp
public GameLoader(IContentLoaderService contentLoader)
```

#### Parameters
- `contentLoader: IContentLoaderService` - Required dependency for loading content. Must not be null (no explicit validation, but will throw NullReferenceException if null when used).

#### Returns
- **Type:** N/A (constructor)
- **Description:** Initializes a new GameLoader instance with the provided content loader service.

#### Dependencies
- **External:** None (pure dependency injection)
- **Internal:** None
- **Side Effects:** None (stores reference)

#### Testing Scenarios
1. **Happy Path:** 
   - Create GameLoader with valid IContentLoaderService mock
   - Verify instance is created successfully
   - Verify contentLoader field is stored correctly (via reflection if needed)

2. **Edge Cases:**
   - Constructor with null contentLoader (should be allowed, will fail later when used)
   - Multiple instances with different contentLoader services

3. **Error Cases:**
   - None (constructor doesn't validate)

#### Mock Requirements
- `IContentLoaderService` mock using NSubstitute
- No special setup needed for constructor

#### Testing Considerations
- **Unity-Specific:** None (pure C# constructor)
- **Async Considerations:** None
- **CRITICAL - Null Parameter Handling:** Constructor doesn't validate null, but LoadGameAsync will fail with NullReferenceException. Consider testing both scenarios.
- **CRITICAL - Reflection vs Direct Calls:** Can use direct instantiation for testing.

---

### 2. LoadGameAsync

**Type:** Public async method (interface implementation)  
**Location:** Lines 39-87  
**Export:** Public (implements IGameLoader)  
**Complexity:** Complex

#### Signature
```csharp
public async UniTask LoadGameAsync(CancellationToken ct = default)
```

#### Parameters
- `ct: CancellationToken` - Optional cancellation token for async operations. Defaults to `default` (CancellationToken.None).

#### Returns
- **Type:** `UniTask`
- **Description:** Completes when all game content (catalogs, labels, scenes) has been loaded successfully.

#### Dependencies
- **External:** 
  - `IContentLoaderService` (instance field)
  - `GlobalConstants.Instance` (static singleton)
  - `GlobalConstantsConfig.MenuSceneReference` (Unity AssetReference)
  - `UniTask.WaitForEndOfFrame()` (Unity frame wait)
  - Serilog `log` (static lazy logger)
- **Internal:** 
  - `LoadManifestAsync(ct)` (static method)
  - `LoadScenesFromManifest(manifest, ct)` (private method)
  - `LoadSceneFromAssetReference(menuSceneRef, ct)` (private method)
- **Side Effects:** 
  - Initializes Addressables system
  - Loads remote catalogs
  - Preloads assets by label
  - Loads Unity scenes
  - Logs information, warnings, and errors

#### Complexity Indicators
- **Cyclomatic Complexity:** High (multiple conditional branches, loops, async operations)
- **Branching Logic:** 
  - If statement for GlobalConstants.Instance check
  - If statement for MenuSceneReference null check
  - If statement for RuntimeKeyIsValid check
  - Else branches for fallback scenarios
- **Loop Constructs:** 
  - foreach loop for catalogs
  - foreach loop for labels
- **Error Handling:** None (exceptions propagate to caller)

#### Testing Scenarios
1. **Happy Path - MenuSceneReference from GlobalConstants:**
   - GlobalConstants.Instance is not null
   - GlobalConstantsConfig.MenuSceneReference is not null
   - RuntimeKeyIsValid() returns true
   - contentLoader.InitializeAsync succeeds
   - LoadManifestAsync returns valid manifest
   - All catalogs load successfully
   - All labels preload successfully
   - Scene loads successfully from AssetReference
   - Verify all async operations complete

2. **Happy Path - Manifest Scenes (no MenuSceneReference):**
   - GlobalConstants.Instance is null OR
   - GlobalConstantsConfig.MenuSceneReference is null OR
   - RuntimeKeyIsValid() returns false
   - Falls back to LoadScenesFromManifest
   - All manifest scenes load successfully

3. **Edge Cases:**
   - Empty manifest (no catalogs, no labels, no scenes)
   - Manifest with null arrays (should use Array.Empty fallback)
   - Multiple catalogs
   - Multiple labels
   - Multiple scenes
   - CancellationToken cancellation during operation
   - contentLoader already initialized

4. **Error Cases:**
   - contentLoader is null (NullReferenceException)
   - contentLoader.InitializeAsync throws exception
   - LoadManifestAsync throws exception
   - contentLoader.LoadCatalogAsync throws exception
   - contentLoader.PreloadLabelAsync throws exception
   - LoadSceneFromAssetReference throws exception
   - LoadScenesFromManifest throws exception
   - GlobalConstants.Instance access throws exception

5. **Lifecycle State Cases:**
   - Multiple calls to LoadGameAsync (should handle gracefully or throw)
   - LoadGameAsync called after cancellation

6. **Unity GameObject Lifecycle Cases:**
   - N/A (no GameObject dependencies)

7. **Exception Isolation Cases:**
   - Exception during catalog loading (should propagate)
   - Exception during label preloading (should propagate)
   - Exception during scene loading (should propagate)

8. **Log Assertion Cases:**
   - Information log: "Loading catalog: {Catalog}"
   - Debug log: "Preloading label: {Label}"
   - Information log: "Loading menu scene from GlobalConstantsConfig: {SceneGuid}"
   - Warning log: "MenuSceneReference from GlobalConstantsConfig is invalid, falling back to manifest scenes"
   - Debug log: "No MenuSceneReference in GlobalConstantsConfig, using manifest scenes"
   - Information log: "Game content loaded successfully. Manifest: {ManifestPath}, Version: {Version}"

9. **UniTask Async Testing Cases:**
   - Verify UniTask completes successfully
   - Verify UniTask fails with appropriate exception
   - Test cancellation token propagation
   - Verify WaitForEndOfFrame is called

#### Mock Requirements
- `IContentLoaderService` mock with:
  - `InitializeAsync(CancellationToken)` - returns completed UniTask
  - `LoadCatalogAsync(string, CancellationToken)` - returns completed UniTask
  - `PreloadLabelAsync(string, CancellationToken)` - returns completed UniTask
- `GlobalConstants.Instance` - static singleton mock (may require reflection or test helper)
- `GlobalConstantsConfig` - mock with MenuSceneReference property
- `AssetReferenceScene` - Unity mock with RuntimeKeyIsValid() and AssetGUID
- Serilog logger - may need LogAssert.Expect for log verification

#### Testing Considerations
- **Unity-Specific:** 
  - Requires Unity Test Framework for Addressables mocking
  - GlobalConstants is a singleton - may need test helper to reset/isolate
  - AssetReferenceScene is Unity-specific - may need custom mock or test double
  - UniTask.WaitForEndOfFrame() requires Unity context
- **Async Considerations:** 
  - Use async test methods with UniTask
  - Test cancellation token propagation
  - Verify all async operations are awaited
- **CRITICAL - Null Parameter Handling:** 
  - contentLoader field access will throw NullReferenceException if null
  - GlobalConstants.Instance may be null (handled with null-conditional operator)
  - MenuSceneReference may be null (handled with fallback)
- **CRITICAL - Static State Persistence:** 
  - GlobalConstants.Instance is static - may persist between tests
  - Requires test isolation or reset mechanism
- **CRITICAL - Log Assertion Requirements:** 
  - Multiple log statements require LogAssert.Expect
  - Structured logging with parameters requires regex matching
- **CRITICAL - Reflection vs Direct Calls:** 
  - Can test directly via public interface
  - May need reflection to verify private method calls

---

### 3. LoadScenesFromManifest

**Type:** Private async instance method  
**Location:** Lines 91-99  
**Export:** Internal (private)  
**Complexity:** Moderate

#### Signature
```csharp
private async UniTask LoadScenesFromManifest(RuntimeContentManifest manifest, CancellationToken ct)
```

#### Parameters
- `manifest: RuntimeContentManifest` - Required manifest containing scene keys. Must not be null (no explicit validation).
- `ct: CancellationToken` - Cancellation token for async operations.

#### Returns
- **Type:** `UniTask`
- **Description:** Completes when all scenes from manifest have been loaded.

#### Dependencies
- **External:** 
  - Serilog `log` (static lazy logger)
- **Internal:** 
  - `LoadSceneAsync(string, LoadSceneMode, CancellationToken)` (private method)
- **Side Effects:** 
  - Loads Unity scenes via Addressables
  - Logs debug messages for each scene

#### Complexity Indicators
- **Cyclomatic Complexity:** Low (single loop)
- **Branching Logic:** None
- **Loop Constructs:** 
  - foreach loop over manifest.Scenes
- **Error Handling:** None (exceptions propagate)

#### Testing Scenarios
1. **Happy Path:**
   - Manifest with multiple scenes
   - All scenes load successfully
   - Scenes loaded in order
   - Verify LoadSceneAsync called for each scene with LoadSceneMode.Single

2. **Edge Cases:**
   - Empty scenes array (no iterations)
   - Null scenes array (will throw NullReferenceException)
   - Single scene
   - Many scenes (10+)

3. **Error Cases:**
   - manifest is null (NullReferenceException)
   - manifest.Scenes is null (NullReferenceException)
   - LoadSceneAsync throws exception (propagates)
   - Cancellation during scene loading

4. **Log Assertion Cases:**
   - Debug log: "Loading Addressable scene from manifest: {Scene}" (one per scene)

#### Mock Requirements
- `RuntimeContentManifest` - test data with Scenes array
- `LoadSceneAsync` - verify calls via reflection or make protected for testing
- Serilog logger - LogAssert.Expect for debug logs

#### Testing Considerations
- **Unity-Specific:** 
  - LoadSceneMode.Single is Unity-specific enum
  - Scene loading requires Unity Addressables context
- **Async Considerations:** 
  - Test async completion
  - Test cancellation propagation
- **CRITICAL - Null Parameter Handling:** 
  - No null validation - will throw NullReferenceException
  - Should test null manifest scenario
- **CRITICAL - Reflection vs Direct Calls:** 
  - Private method - requires reflection or make protected/internal for testing
  - Can test indirectly through LoadGameAsync
- **CRITICAL - Log Assertion Requirements:** 
  - Debug log per scene requires LogAssert.Expect

---

### 4. LoadSceneFromAssetReference

**Type:** Private async instance method  
**Location:** Lines 101-119  
**Export:** Internal (private)  
**Complexity:** Moderate

#### Signature
```csharp
private async UniTask LoadSceneFromAssetReference(AssetReference assetRef, CancellationToken ct)
```

#### Parameters
- `assetRef: AssetReference` - Unity AssetReference to scene. Must not be null (no explicit validation).
- `ct: CancellationToken` - Cancellation token for async operations.

#### Returns
- **Type:** `UniTask`
- **Description:** Completes when scene has been loaded from AssetReference.

#### Dependencies
- **External:** 
  - `AssetReference.LoadSceneAsync()` (Unity Addressables)
  - `AsyncOperationHandle<SceneInstance>.ToUniTask()` (UniTask extension)
  - Serilog `log` (static lazy logger)
- **Internal:** None
- **Side Effects:** 
  - Loads Unity scene from AssetReference
  - Logs debug messages on success
  - Logs warning messages on failure
  - Logs error messages on exception

#### Complexity Indicators
- **Cyclomatic Complexity:** Moderate (try-catch, if-else)
- **Branching Logic:** 
  - If statement checking handle.Status == AsyncOperationStatus.Succeeded
  - Else branch for failure
- **Loop Constructs:** None
- **Error Handling:** 
  - Try-catch block catches all exceptions, logs, and rethrows

#### Testing Scenarios
1. **Happy Path:**
   - Valid AssetReference
   - LoadSceneAsync succeeds
   - handle.Status == AsyncOperationStatus.Succeeded
   - Scene loads successfully
   - Verify success log

2. **Edge Cases:**
   - AssetReference with invalid GUID
   - AssetReference pointing to non-existent scene

3. **Error Cases:**
   - assetRef is null (NullReferenceException)
   - LoadSceneAsync throws exception (caught, logged, rethrown)
   - handle.Status != Succeeded (warning logged, but no exception)
   - Cancellation during load

4. **Log Assertion Cases:**
   - Debug log: "Successfully loaded scene from AssetReference: {Guid}" (on success)
   - Warning log: "Scene load failed from AssetReference: {Guid}, Status: {Status}" (on failure)
   - Error log: "Failed to load scene from AssetReference: {Guid}" (on exception)

#### Mock Requirements
- `AssetReference` - Unity mock or test double
- `AsyncOperationHandle<SceneInstance>` - mock with Status property
- `ToUniTask()` extension - UniTask test utilities
- Serilog logger - LogAssert.Expect for all log levels

#### Testing Considerations
- **Unity-Specific:** 
  - AssetReference is Unity-specific - requires custom mock or test double
  - AsyncOperationHandle is Unity Addressables-specific
  - SceneInstance is Unity-specific
- **Async Considerations:** 
  - Test async completion
  - Test exception handling in async context
  - Test cancellation
- **CRITICAL - Null Parameter Handling:** 
  - No null validation - will throw NullReferenceException
  - Should test null assetRef scenario
- **CRITICAL - Reflection vs Direct Calls:** 
  - Private method - requires reflection or make protected/internal for testing
  - Can test indirectly through LoadGameAsync
- **CRITICAL - Log Assertion Requirements:** 
  - Multiple log statements (debug, warning, error) require LogAssert.Expect
  - Exception logging requires exception parameter matching

---

### 5. LoadSceneAsync

**Type:** Private async instance method  
**Location:** Lines 121-140  
**Export:** Internal (private)  
**Complexity:** Moderate

#### Signature
```csharp
private async UniTask LoadSceneAsync(string sceneKey, LoadSceneMode loadMode, CancellationToken ct)
```

#### Parameters
- `sceneKey: string` - Addressable scene key. Must not be null (no explicit validation).
- `loadMode: LoadSceneMode` - Unity scene load mode (Single or Additive). Always passed as LoadSceneMode.Single in current usage.
- `ct: CancellationToken` - Cancellation token for async operations.

#### Returns
- **Type:** `UniTask`
- **Description:** Completes when scene has been loaded via Addressables.

#### Dependencies
- **External:** 
  - `Addressables.LoadSceneAsync(string, LoadSceneMode)` (Unity Addressables static API)
  - `AsyncOperationHandle<SceneInstance>.ToUniTask()` (UniTask extension)
  - Serilog `log` (static lazy logger)
- **Internal:** None
- **Side Effects:** 
  - Loads Unity scene via Addressables
  - Logs debug messages on success
  - Logs warning messages on failure
  - Logs error messages on exception

#### Complexity Indicators
- **Cyclomatic Complexity:** Moderate (try-catch, if-else)
- **Branching Logic:** 
  - If statement checking handle.Status == AsyncOperationStatus.Succeeded
  - Else branch for failure
- **Loop Constructs:** None
- **Error Handling:** 
  - Try-catch block catches all exceptions, logs, and rethrows

#### Testing Scenarios
1. **Happy Path:**
   - Valid sceneKey
   - Scene exists in Addressables
   - LoadSceneAsync succeeds
   - handle.Status == AsyncOperationStatus.Succeeded
   - Scene loads successfully
   - Verify success log

2. **Edge Cases:**
   - Empty sceneKey string
   - Invalid sceneKey (not in Addressables)
   - LoadSceneMode.Single vs LoadSceneMode.Additive (currently only Single used)

3. **Error Cases:**
   - sceneKey is null (NullReferenceException)
   - Addressables.LoadSceneAsync throws exception (caught, logged, rethrown)
   - handle.Status != Succeeded (warning logged, but no exception)
   - Cancellation during load

4. **Log Assertion Cases:**
   - Debug log: "Successfully loaded Addressable scene: {Scene}" (on success)
   - Warning log: "Addressable scene load failed for: {Scene}, Status: {Status}" (on failure)
   - Error log: "Failed to load Addressable scene: {Scene}. Ensure the scene is configured as Addressable." (on exception)

#### Mock Requirements
- `Addressables` static class - requires Unity Test Framework or custom wrapper
- `AsyncOperationHandle<SceneInstance>` - mock with Status property
- `ToUniTask()` extension - UniTask test utilities
- Serilog logger - LogAssert.Expect for all log levels

#### Testing Considerations
- **Unity-Specific:** 
  - Addressables is static Unity API - difficult to mock directly
  - May need wrapper interface for testability
  - LoadSceneMode is Unity enum
  - SceneInstance is Unity-specific
- **Async Considerations:** 
  - Test async completion
  - Test exception handling in async context
  - Test cancellation
- **CRITICAL - Null Parameter Handling:** 
  - No null validation for sceneKey - will throw NullReferenceException
  - Should test null sceneKey scenario
- **CRITICAL - Reflection vs Direct Calls:** 
  - Private method - requires reflection or make protected/internal for testing
  - Can test indirectly through LoadGameAsync
- **CRITICAL - Log Assertion Requirements:** 
  - Multiple log statements (debug, warning, error) require LogAssert.Expect
  - Exception logging requires exception parameter matching
- **CRITICAL - Static API Testing:** 
  - Addressables is static - consider wrapper interface for better testability

---

### 6. LoadManifestAsync

**Type:** Private static async method  
**Location:** Lines 142-209  
**Export:** Internal (private static)  
**Complexity:** Complex

#### Signature
```csharp
private static async UniTask<(RuntimeContentManifest, string)> LoadManifestAsync(CancellationToken ct)
```

#### Parameters
- `ct: CancellationToken` - Cancellation token for async operations.

#### Returns
- **Type:** `UniTask<(RuntimeContentManifest, string)>`
- **Description:** Returns tuple of (manifest, path) where manifest is the loaded RuntimeContentManifest and path is the source path (Addressables URL or file path).

#### Dependencies
- **External:** 
  - `Addressables.LoadAssetAsync<TextAsset>(string)` (Unity Addressables static API)
  - `AsyncOperationHandle<TextAsset>.ToUniTask()` (UniTask extension)
  - `Addressables.Release(AsyncOperationHandle)` (Unity Addressables static API)
  - `Application.streamingAssetsPath` (Unity static property)
  - `File.Exists(string)` (.NET file system)
  - `File.ReadAllTextAsync(string, CancellationToken)` (.NET async file I/O)
  - `JsonUtility.FromJson<T>(string)` (Unity JSON deserialization)
  - Serilog `log` (static lazy logger)
- **Internal:** None
- **Side Effects:** 
  - Loads TextAsset from Addressables
  - Reads file from StreamingAssets
  - Creates default manifest if file not found
  - Logs information, warnings, and errors
  - Releases Addressables handles

#### Complexity Indicators
- **Cyclomatic Complexity:** High (multiple try-catch, if-else, nested conditions)
- **Branching Logic:** 
  - Try-catch for Addressables load
  - If statement checking handle.Status == Succeeded && handle.Result != null
  - Else branch for fallback
  - If statement checking handle.IsValid()
  - If statement checking File.Exists()
  - Try-catch for file read
- **Loop Constructs:** None
- **Error Handling:** 
  - Multiple try-catch blocks
  - Fallback mechanisms (Addressables → StreamingAssets → Default)

#### Testing Scenarios
1. **Happy Path - Addressables:**
   - Manifest exists in Addressables at "manifest" address
   - LoadAssetAsync succeeds
   - handle.Status == Succeeded
   - handle.Result != null
   - JSON deserialization succeeds
   - Returns manifest and "Addressables://manifest" path
   - Handle is released

2. **Happy Path - StreamingAssets Fallback:**
   - Manifest not in Addressables (or load fails)
   - File exists at StreamingAssets/manifest.txt
   - File.ReadAllTextAsync succeeds
   - JSON deserialization succeeds
   - Returns manifest and file path

3. **Happy Path - Default Manifest:**
   - Manifest not in Addressables
   - File not found in StreamingAssets
   - Returns default manifest with empty catalogs, default labels ["core", "ui", "localization"], empty scenes, version "1.0"
   - Returns StreamingAssets path

4. **Edge Cases:**
   - Empty manifest JSON
   - Invalid manifest JSON (should throw during deserialization)
   - Manifest with null arrays (handled by RuntimeContentManifest properties)
   - Very large manifest file
   - Cancellation during Addressables load
   - Cancellation during file read

5. **Error Cases:**
   - Addressables.LoadAssetAsync throws exception (caught, falls back to StreamingAssets)
   - handle.Status != Succeeded (falls back to StreamingAssets)
   - handle.Result is null (falls back to StreamingAssets)
   - File.ReadAllTextAsync throws exception (rethrown)
   - JsonUtility.FromJson throws exception (rethrown)
   - File.Exists throws exception (unlikely, but possible)

6. **Log Assertion Cases:**
   - Information log: "Loaded manifest from Addressables ({Address}) with {Catalogs} catalogs, {Labels} labels, {Scenes} scenes"
   - Debug log: "Manifest not found in Addressables at {Address}, trying StreamingAssets fallback"
   - Warning log: "Failed to load manifest from Addressables, trying StreamingAssets fallback: {Message}"
   - Warning log: "Manifest not found at {Path}, using defaults"
   - Information log: "Loaded manifest from StreamingAssets ({Path}) with {Catalogs} catalogs, {Labels} labels, {Scenes} scenes"
   - Error log: "Failed to load manifest from {Path}"

7. **Resource Management Cases:**
   - Verify Addressables handle is released after use
   - Verify handle is released even if Result is null
   - Verify handle is released in exception cases (if IsValid)

#### Mock Requirements
- `Addressables` static class - requires Unity Test Framework or custom wrapper
- `AsyncOperationHandle<TextAsset>` - mock with Status, Result, IsValid properties
- `Application.streamingAssetsPath` - Unity static property (may need test helper)
- `File.Exists()` - .NET static method (may need wrapper for testability)
- `File.ReadAllTextAsync()` - .NET static method (may need wrapper for testability)
- `JsonUtility.FromJson<T>()` - Unity static method (may need wrapper for testability)
- Serilog logger - LogAssert.Expect for all log levels

#### Testing Considerations
- **Unity-Specific:** 
  - Addressables is static Unity API - difficult to mock directly
  - Application.streamingAssetsPath is Unity static property
  - JsonUtility is Unity static API
  - May need wrapper interfaces for better testability
- **Async Considerations:** 
  - Test async completion
  - Test exception handling in async context
  - Test cancellation at different points
  - Test fallback chain (Addressables → StreamingAssets → Default)
- **CRITICAL - Static API Testing:** 
  - Multiple static APIs (Addressables, File, JsonUtility, Application)
  - Consider wrapper interfaces for testability
  - May need test helpers to set up file system state
- **CRITICAL - Log Assertion Requirements:** 
  - Multiple log statements (information, debug, warning, error) require LogAssert.Expect
  - Structured logging with parameters requires regex matching
  - Exception logging requires exception parameter matching
- **CRITICAL - Resource Management:** 
  - Must verify Addressables handles are released
  - Test handle release in success and failure scenarios
- **CRITICAL - Reflection vs Direct Calls:** 
  - Private static method - requires reflection or make protected/internal for testing
  - Can test indirectly through LoadGameAsync
- **CRITICAL - File System Testing:** 
  - File operations require test file system setup
  - May need temporary directories for isolation
  - Cleanup required in TearDown

---

## Testing Strategy Recommendations

### High Priority Functions

1. **LoadGameAsync** - Critical public interface method, orchestrates entire loading process
2. **LoadManifestAsync** - Complex fallback logic, multiple error paths, resource management
3. **LoadSceneAsync** - Core scene loading functionality, used by multiple callers
4. **LoadSceneFromAssetReference** - Alternative scene loading path, error handling

### Medium Priority Functions

5. **LoadScenesFromManifest** - Simpler orchestration method, but important for manifest-based loading
6. **Constructor** - Simple, but foundation for all tests

### Lifecycle State Testing Strategy

- **Multiple LoadGameAsync Calls:** Test behavior when called multiple times (should handle gracefully or document expected behavior)
- **Cancellation State:** Test cancellation at different points in the loading process
- **Partial Failure State:** Test behavior when some operations succeed and others fail

### Mock Strategy

**CRITICAL Recommendations:**

1. **Create IAddressablesWrapper Interface:**
   - Wrap Unity Addressables static API for testability
   - Methods: LoadAssetAsync<T>, LoadSceneAsync, Release
   - Inject via constructor or service locator

2. **Create IFileSystemWrapper Interface:**
   - Wrap File.Exists, File.ReadAllTextAsync for testability
   - Inject via constructor or service locator

3. **Create IJsonDeserializer Interface:**
   - Wrap JsonUtility.FromJson for testability
   - Inject via constructor or service locator

4. **GlobalConstants Test Helper:**
   - Create helper class to set/reset GlobalConstants.Instance
   - Use in SetUp/TearDown for test isolation

5. **Test Data Factory:**
   - Create RuntimeContentManifest test data factory methods
   - Create AssetReference test doubles
   - Create AsyncOperationHandle test doubles

6. **Parameterized Tests:**
   - Use [TestCase] for similar scenarios (empty arrays, null arrays, etc.)
   - Use [TestCase] for different load modes

7. **Exception Isolation:**
   - Use try/finally blocks for proper cleanup
   - Test exception propagation through async chain
   - Verify resource cleanup in exception scenarios

### Test File Organization

Suggested structure:
```
Tests/Runtime/Bootstrapper/
  ├── GameLoaderTests.cs (main test class)
  ├── GameLoaderPropertyTests.cs (property-based tests if applicable)
  └── TestHelpers/
      ├── GameLoaderTestData.cs (test data factory)
      ├── AddressablesWrapperMock.cs (Addressables mock)
      ├── FileSystemWrapperMock.cs (File system mock)
      └── GlobalConstantsTestHelper.cs (singleton test helper)
```

## Special Considerations

### Unity Addressables Integration

- **CRITICAL**: Addressables static API is difficult to mock
- **CRITICAL**: Consider wrapper interface for better testability
- **CRITICAL**: AsyncOperationHandle requires careful mocking
- **CRITICAL**: Handle release must be verified in tests

### Unity Scene Loading

- **CRITICAL**: Scene loading requires Unity context
- **CRITICAL**: LoadSceneMode.Single vs Additive behavior
- **CRITICAL**: AssetReference vs string key loading paths

### Static Dependencies

- **CRITICAL**: GlobalConstants.Instance is static singleton - requires test isolation
- **CRITICAL**: Application.streamingAssetsPath is static - may need test override
- **CRITICAL**: File static methods - consider wrapper for testability
- **CRITICAL**: JsonUtility static method - consider wrapper for testability

### Async/UniTask Patterns

- **CRITICAL**: All methods return UniTask - use async test methods
- **CRITICAL**: Test cancellation token propagation
- **CRITICAL**: Test exception handling in async context
- **CRITICAL**: Verify WaitForEndOfFrame is called in LoadGameAsync

### Logging Patterns

- **CRITICAL**: Multiple log statements require LogAssert.Expect
- **CRITICAL**: Structured logging with parameters requires regex matching
- **CRITICAL**: Exception logging requires exception parameter matching
- **CRITICAL**: Count log messages to match expected calls

### Resource Management

- **CRITICAL**: Addressables handles must be released
- **CRITICAL**: Test handle release in success and failure scenarios
- **CRITICAL**: Verify no handle leaks in exception paths

### Error Handling

- **CRITICAL**: No null parameter validation in most methods
- **CRITICAL**: Exceptions propagate to caller (no swallowing)
- **CRITICAL**: Fallback mechanisms (Addressables → StreamingAssets → Default)
- **CRITICAL**: Test all fallback paths

### Test Isolation

- **CRITICAL**: GlobalConstants.Instance persists between tests
- **CRITICAL**: File system state may persist between tests
- **CRITICAL**: Use TearDown to clean up test artifacts
- **CRITICAL**: Use temporary directories for file system tests

## Implementation Bug Detection

### Potential Issues Identified

1. **Missing Null Validation:**
   - Constructor doesn't validate contentLoader parameter
   - LoadScenesFromManifest doesn't validate manifest parameter
   - LoadSceneFromAssetReference doesn't validate assetRef parameter
   - LoadSceneAsync doesn't validate sceneKey parameter
   - **Recommendation:** Add null checks with ArgumentNullException

2. **Resource Management:**
   - Addressables handle release is correct in LoadManifestAsync
   - **Recommendation:** Verify no other resource leaks

3. **Exception Handling:**
   - Exceptions are properly caught and rethrown where appropriate
   - Fallback mechanisms are in place
   - **Recommendation:** Consider more specific exception types

4. **Static Dependencies:**
   - Heavy reliance on static APIs (Addressables, File, JsonUtility, Application)
   - **Recommendation:** Consider dependency injection for better testability

5. **Error Messages:**
   - Error messages are descriptive and include context
   - **Recommendation:** Consider adding more context in some error messages

## Quality Checks

✅ All functions identified  
✅ Complex functions have adequate scenario coverage  
✅ Mock requirements clearly identified  
✅ Test priorities established  
✅ Unity-specific considerations documented  
✅ Static dependencies identified  
✅ Async patterns documented  
✅ Logging patterns documented  
✅ Resource management documented  
✅ Error handling documented  
✅ Test isolation requirements documented  

