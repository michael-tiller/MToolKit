# Function Analysis: GlobalConstants.cs

**File:** `MToolKit/Runtime/Core/Singletons/GlobalConstants.cs`  
**Date:** 2025-11-18
**Total Functions Found:** 5

## Summary

`GlobalConstants` is a MonoBehaviour-based singleton that manages global configuration constants for the MToolKit framework. It inherits from `Singleton<GlobalConstants>` and provides environment-specific configuration loading with override support. The class automatically initializes before scene load via `RuntimeInitializeOnLoadMethod` and loads configuration from Unity Resources, with support for override configurations similar to the SlogConfig pattern.

**Key Characteristics:**
- Singleton pattern with auto-creation before scene load
- Environment-specific configuration override support
- Resources-based asset loading
- Default configuration fallback
- DontDestroyOnLoad enabled for persistence

## Function Inventory

### Lifecycle Methods
- Total: 2
- Complexity: Simple to Moderate

### Configuration Methods
- Total: 3
- Complexity: Simple to Moderate

## Detailed Function Analysis

### 1. OnRuntimeMethodLoad

**Type:** Static method with `RuntimeInitializeOnLoadMethod` attribute  
**Location:** Lines 26-33  
**Export:** Internal (private static)  
**Complexity:** Simple

#### Signature
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void OnRuntimeMethodLoad()
```

#### Parameters
- None

#### Returns
- **Type:** `void`
- **Description:** No return value

#### Dependencies
- **External:** `UnityEngine.GameObject`, `UnityEngine.Object`
- **Internal:** `nameof(GlobalConstants)`
- **Side Effects:** 
  - Creates a new GameObject with name `[Singleton] GlobalConstants`
  - Adds `GlobalConstants` component to the GameObject
  - Triggers Unity's component lifecycle (Awake will be called)

#### Testing Scenarios
1. **Happy Path:** 
   - Method is called automatically by Unity before scene load
   - Creates singleton GameObject with correct name
   - Component is added successfully

2. **Edge Cases:**
   - Multiple calls (should be idempotent due to singleton pattern)
   - Called during application quit (should be handled by base Singleton class)

3. **Error Cases:**
   - None expected (Unity handles GameObject creation)

4. **Unity-Specific Cases:**
   - **CRITICAL**: This method is called automatically by Unity - cannot be directly tested in EditMode
   - **CRITICAL**: Testing should verify the result (singleton instance exists) rather than calling the method directly
   - **CRITICAL**: Use reflection to verify singleton state after Unity initialization
   - **CRITICAL**: Test through public static `Instance` property access

5. **Log Assertion Cases:**
   - Expected Information log: "Creating GlobalConstants singleton"
   - Use `LogAssert.Expect()` for log verification

#### Mock Requirements
- None (static method, Unity runtime initialization)
- **CRITICAL**: Cannot mock Unity's `RuntimeInitializeOnLoadMethod` behavior
- **CRITICAL**: Test indirectly through singleton instance verification

#### Testing Considerations
- **Unity-Specific:** This is a Unity runtime initialization method - test the result, not the method itself
- **Unity-Specific:** Verify singleton instance exists and is properly initialized
- **Unity-Specific:** Use static `Instance` property to verify creation
- **CRITICAL - Static Class Testing:** Verify singleton state after Unity initialization
- **CRITICAL - Reflection vs Direct Calls:** Use reflection to verify private static state if needed
- **CRITICAL - Log Assertion Requirements:** Expect Information log message on creation

---

### 2. Awake

**Type:** Protected override method (Unity lifecycle)  
**Location:** Lines 35-41  
**Export:** Protected (Unity lifecycle)  
**Complexity:** Simple

#### Signature
```csharp
protected override void Awake()
```

#### Parameters
- None

#### Returns
- **Type:** `void`
- **Description:** No return value

#### Dependencies
- **External:** Base class `Singleton<GlobalConstants>.Awake()`
- **Internal:** `LoadGlobalConstantsConfiguration()`, `IsInitialized` property
- **Side Effects:**
  - Calls base `Awake()` which handles singleton instance management
  - Loads global constants configuration
  - Sets `IsInitialized` to true

#### Testing Scenarios
1. **Happy Path:**
   - Awake is called when component is created
   - Base Awake handles singleton logic correctly
   - Configuration is loaded successfully
   - IsInitialized is set to true

2. **Edge Cases:**
   - Awake called multiple times (should be idempotent due to base class)
   - Configuration file exists in Resources
   - Override configuration exists in Resources

3. **Error Cases:**
   - Configuration file missing (should create default config)
   - Resources.Load returns null (should create default config)

4. **Unity GameObject Lifecycle Cases:**
   - **CRITICAL**: Awake is called automatically when component is added to GameObject
   - **CRITICAL**: Cannot directly call Awake() in tests - use `AddComponent<T>()` to trigger
   - **CRITICAL**: Expect error logs if configuration is missing (requires LogAssert.Expect)
   - **CRITICAL**: Test through component creation, not direct method calls

5. **Log Assertion Cases:**
   - Expected Information log: "GlobalConstantsConfig loaded from [path]"
   - Expected Warning log: "Using default GlobalConstantsConfig values" (when config missing)
   - Expected Error log: "Failed to load GlobalConstantsConfig from [path]. Creating default config."
   - Use `LogAssert.Expect()` for all expected log messages

#### Mock Requirements
- Mock or provide `GlobalConstantsConfigAsset` in Resources
- **CRITICAL**: Create test ScriptableObject instances for configuration
- **CRITICAL**: Use `Resources.Load` override or test-specific Resources folder

#### Testing Considerations
- **Unity-Specific:** Awake is called automatically - test through component creation
- **Unity-Specific:** Use `AddComponent<GlobalConstants>()` to trigger Awake
- **Unity-Specific:** Mock Resources.Load or provide test assets
- **CRITICAL - Log Assertion Requirements:** Expect multiple log messages (Information, Warning, Error)
- **CRITICAL - Reflection vs Direct Calls:** Use reflection to verify private `IsInitialized` state
- **CRITICAL - VContainer Exception Types:** No VContainer dependencies in this method

---

### 3. LoadGlobalConstantsConfiguration

**Type:** Private method  
**Location:** Lines 47-68  
**Export:** Internal (private)  
**Complexity:** Moderate

#### Signature
```csharp
private void LoadGlobalConstantsConfiguration()
```

#### Parameters
- None

#### Returns
- **Type:** `void`
- **Description:** No return value, but sets `GlobalConstantsConfig` property

#### Dependencies
- **External:** `UnityEngine.Resources`, `UnityEngine.Object`
- **Internal:** `DoesResourceAssetExist()`, `CreateDefaultConfig()`, `GlobalConstantsConfig` property
- **Side Effects:**
  - Loads ScriptableObject from Resources
  - Sets `GlobalConstantsConfig` property
  - May create default configuration if loading fails
  - Logs information, warnings, or errors

#### Testing Scenarios
1. **Happy Path:**
   - Base config file exists in Resources
   - Config is loaded successfully
   - Property is set correctly

2. **Edge Cases:**
   - Override config file exists (should use override instead of base)
   - Both base and override exist (should prefer override)
   - Neither config exists (should create default)

3. **Error Cases:**
   - Resources.Load returns null for base config
   - Resources.Load returns null for override config
   - Default config creation fails (unlikely, but possible)

4. **Unity GameObject Lifecycle Cases:**
   - **CRITICAL**: This is a private method - test through public API (Awake/Instance)
   - **CRITICAL**: Use reflection to call directly if needed for isolated testing
   - **CRITICAL**: Verify configuration state through public property

5. **Log Assertion Cases:**
   - Expected Information log: "Override config found. Using [OverrideGlobalConstantsConfig]"
   - Expected Information log: "GlobalConstantsConfig loaded from [path]"
   - Expected Error log: "Failed to load GlobalConstantsConfig from [path]. Creating default config."
   - Use `LogAssert.Expect()` with regex patterns for structured logging

#### Mock Requirements
- Mock `Resources.Load<GlobalConstantsConfigAsset>()` behavior
- Provide test ScriptableObject instances
- **CRITICAL**: Create test Resources folder structure
- **CRITICAL**: Use reflection to access private method for isolated testing

#### Testing Considerations
- **Unity-Specific:** Private method - test through public API or reflection
- **Unity-Specific:** Mock Resources.Load or provide test assets
- **CRITICAL - Reflection vs Direct Calls:** Use reflection to call private method for isolated testing
- **CRITICAL - Log Assertion Requirements:** Expect multiple log messages based on configuration state
- **CRITICAL - Framework-Specific Behavior:** Resources.Load behavior is Unity-specific and may need mocking

---

### 4. CreateDefaultConfig

**Type:** Private method  
**Location:** Lines 73-77  
**Export:** Internal (private)  
**Complexity:** Simple

#### Signature
```csharp
private void CreateDefaultConfig()
```

#### Parameters
- None

#### Returns
- **Type:** `void`
- **Description:** No return value, but sets `GlobalConstantsConfig` property to a new default instance

#### Dependencies
- **External:** `UnityEngine.ScriptableObject`
- **Internal:** `GlobalConstantsConfig` property
- **Side Effects:**
  - Creates new `GlobalConstantsConfigAsset` instance via `ScriptableObject.CreateInstance`
  - Sets `GlobalConstantsConfig` property
  - Logs warning message

#### Testing Scenarios
1. **Happy Path:**
   - Default config is created successfully
   - Property is set to new instance
   - Instance is not null

2. **Edge Cases:**
   - Called when config already exists (should overwrite)
   - Called multiple times (should create new instance each time)

3. **Error Cases:**
   - ScriptableObject.CreateInstance fails (unlikely, but possible)
   - Property assignment fails (unlikely)

4. **Unity GameObject Lifecycle Cases:**
   - **CRITICAL**: This is a private method - test through public API
   - **CRITICAL**: Use reflection to call directly if needed
   - **CRITICAL**: Verify default config creation through property access

5. **Log Assertion Cases:**
   - Expected Warning log: "Using default GlobalConstantsConfig values"
   - Use `LogAssert.Expect()` for warning message

#### Mock Requirements
- None (uses Unity's ScriptableObject.CreateInstance)
- **CRITICAL**: Verify ScriptableObject instance creation
- **CRITICAL**: Use reflection to access private method for isolated testing

#### Testing Considerations
- **Unity-Specific:** Private method - test through public API or reflection
- **Unity-Specific:** ScriptableObject.CreateInstance is Unity-specific
- **CRITICAL - Reflection vs Direct Calls:** Use reflection to call private method for isolated testing
- **CRITICAL - Log Assertion Requirements:** Expect Warning log message
- **CRITICAL - Framework-Specific Behavior:** ScriptableObject behavior is Unity-specific

---

### 5. DoesResourceAssetExist

**Type:** Private static method  
**Location:** Lines 82-86  
**Export:** Internal (private static)  
**Complexity:** Simple

#### Signature
```csharp
private static bool DoesResourceAssetExist(string filename)
```

#### Parameters
- `filename: string` - The name of the resource file to check (without extension)

#### Returns
- **Type:** `bool`
- **Description:** Returns true if the resource exists, false otherwise

#### Dependencies
- **External:** `UnityEngine.Resources`, `UnityEngine.Object`
- **Internal:** None
- **Side Effects:** 
  - Calls `Resources.Load<Object>(filename)`
  - No state mutation

#### Testing Scenarios
1. **Happy Path:**
   - Resource exists - returns true
   - Resource does not exist - returns false

2. **Edge Cases:**
   - Empty string filename
   - Null filename (should throw NullReferenceException or ArgumentNullException)
   - Filename with extension (Resources.Load handles this)
   - Filename with path separators

3. **Error Cases:**
   - Resources.Load throws exception (unlikely, but possible)
   - Invalid filename format

4. **Unity GameObject Lifecycle Cases:**
   - **CRITICAL**: This is a private static method - use reflection to call directly
   - **CRITICAL**: Mock Resources.Load or provide test assets
   - **CRITICAL**: Test both true and false return values

5. **Exception Isolation Cases:**
   - **CRITICAL**: Test null parameter handling (NullReferenceException vs ArgumentNullException)
   - **CRITICAL**: Verify actual exception behavior vs assumed behavior

#### Mock Requirements
- Mock `Resources.Load<Object>()` behavior
- Provide test Resources folder structure
- **CRITICAL**: Use reflection to access private static method
- **CRITICAL**: Test with both existing and non-existing resources

#### Testing Considerations
- **Unity-Specific:** Private static method - use reflection to call directly
- **Unity-Specific:** Mock Resources.Load or provide test assets
- **CRITICAL - Reflection vs Direct Calls:** Use reflection to call private static method
- **CRITICAL - Null Parameter Handling:** Test null filename parameter - verify actual exception type
- **CRITICAL - Framework-Specific Behavior:** Resources.Load behavior is Unity-specific
- **CRITICAL - Implementation Bug Detection:** Verify null parameter validation (may throw NullReferenceException instead of ArgumentNullException)

---

## Testing Strategy Recommendations

### High Priority Functions

1. **Awake** - Critical lifecycle method that initializes the singleton
   - Test configuration loading paths
   - Test default config fallback
   - Test override config priority

2. **LoadGlobalConstantsConfiguration** - Core configuration loading logic
   - Test all configuration loading scenarios
   - Test override vs base config priority
   - Test default config creation

3. **DoesResourceAssetExist** - Utility method used by configuration loading
   - Test resource existence checking
   - Test null/edge case parameter handling

### Lifecycle State Testing Strategy

For `GlobalConstants` singleton:
- **Multiple Initialization Cycles**: Test singleton creation and destruction cycles
- **State Guard Testing**: Verify `IsInitialized` flag behavior
- **Early Return Impact**: Verify configuration loading happens only once
- **Reflection-Based Testing**: Use reflection to access private `IsInitialized` field and `GlobalConstantsConfig` property

### Mock Strategy

Recommended approach for mocking dependencies:

- **CRITICAL**: Create test ScriptableObject instances for `GlobalConstantsConfigAsset`
- **CRITICAL**: Use test-specific Resources folder or mock `Resources.Load`
- **CRITICAL**: Use reflection helper methods for accessing private members
- **CRITICAL**: Create test GameObject instances for singleton testing
- **CRITICAL**: Use `ContainerBuilder` for any dependency injection needs (if applicable)
- **CRITICAL**: Implement test helper methods for creating isolated test scenarios

**Test Data Factory Pattern:**
```csharp
public static class GlobalConstantsTestData
{
    public static GlobalConstantsConfigAsset CreateTestConfig()
    {
        return ScriptableObject.CreateInstance<GlobalConstantsConfigAsset>();
    }
    
    public static GlobalConstants CreateTestInstance()
    {
        GameObject go = new GameObject("[Singleton] GlobalConstants");
        return go.AddComponent<GlobalConstants>();
    }
}
```

### Test File Organization

Suggested structure:
```
Assets/TemplateGame/Source/Tests/Runtime/Core/Singletons/
├── GlobalConstantsTests.cs
└── GlobalConstantsPropertyTests.cs (if property-based testing is needed)
```

## Special Considerations

### Unity MonoBehaviour Components

- **AVOID direct instance creation in tests** - Use `AddComponent<T>()` to trigger Unity lifecycle
- **CRITICAL**: `OnRuntimeMethodLoad` is called automatically by Unity - cannot be directly tested
- **CRITICAL**: Test through public static `Instance` property and public `IsInitialized` property
- **CRITICAL**: Use reflection to access private methods (`LoadGlobalConstantsConfiguration`, `CreateDefaultConfig`, `DoesResourceAssetExist`)
- **CRITICAL**: Use reflection to verify private state (`IsInitialized` field, `GlobalConstantsConfig` property)
- **CRITICAL**: Singleton pattern requires careful test isolation - reset singleton state between tests

### Unity Singleton Systems

- **CRITICAL**: `GlobalConstants` inherits from `Singleton<GlobalConstants>` with `selfCreate = true`
- **CRITICAL**: Singleton instance is created automatically via `OnRuntimeMethodLoad` before scene load
- **CRITICAL**: `dontDestroyOnLoad = true` means instance persists across scene loads
- **CRITICAL**: Test singleton state management (instance creation, duplicate handling)
- **CRITICAL**: Use base class `HasInstance` property to verify singleton state
- **CRITICAL**: Plan singleton state reset strategies for test isolation
- **CRITICAL**: Test singleton behavior through `Instance` static property

### Unity Resources System

- **CRITICAL**: Configuration loading uses `Resources.Load` which requires actual Resources folder structure
- **CRITICAL**: Test scenarios require either:
  - Actual Resources folder with test assets
  - Mocking `Resources.Load` behavior (may require custom test framework)
  - Reflection-based testing to bypass Resources.Load
- **CRITICAL**: Test both base config and override config scenarios
- **CRITICAL**: Test default config creation when Resources.Load returns null

### Static Methods

- **CRITICAL**: `OnRuntimeMethodLoad` is static and called by Unity - test indirectly
- **CRITICAL**: `DoesResourceAssetExist` is private static - use reflection to test directly
- **CRITICAL**: Test static method behavior through reflection or indirect verification

### Logging Patterns

- **CRITICAL**: All methods use Serilog structured logging with `log.ForMethod()`
- **CRITICAL**: Expect multiple log messages:
  - Information: Config loaded, override found, singleton creation
  - Warning: Using default config
  - Error: Failed to load config
- **CRITICAL**: Use `LogAssert.Expect()` with regex patterns for structured logging
- **CRITICAL**: Count log messages to match expected behavior (e.g., one log per config load attempt)

### Configuration Loading Pattern

- **CRITICAL**: Follows SlogConfig override pattern:
  1. Check for override config first
  2. Fall back to base config
  3. Create default config if both fail
- **CRITICAL**: Test all three paths (override, base, default)
- **CRITICAL**: Verify config priority (override > base > default)

### Property Testing Opportunities

- **CRITICAL**: Test configuration loading invariants:
  - Config is never null after initialization
  - IsInitialized is always true after Awake
  - Singleton instance is always available after initialization
- **CRITICAL**: Test state transitions:
  - Uninitialized → Initialized
  - Config missing → Default config created
- **CRITICAL**: Test reversibility:
  - Destroy singleton → Recreate → State is consistent

### Implementation Bug Detection

**Potential Issues Identified:**

1. **Null Parameter Handling in `DoesResourceAssetExist`:**
   - **POTENTIAL BUG**: Method does not validate `filename` parameter
   - **RISK**: `Resources.Load<Object>(null)` may throw `NullReferenceException` instead of `ArgumentNullException`
   - **RECOMMENDATION**: Add null check and throw `ArgumentNullException` for better API contract

2. **Configuration State Consistency:**
   - **POTENTIAL BUG**: `IsInitialized` is set to `true` even if configuration loading fails
   - **RISK**: System may proceed with invalid/default configuration without proper error handling
   - **RECOMMENDATION**: Consider error state handling or validation

3. **Singleton State Management:**
   - **POTENTIAL BUG**: `OnRuntimeMethodLoad` creates singleton directly, bypassing `Instance` property logic
   - **RISK**: May create duplicate instances if called multiple times
   - **MITIGATION**: Base class `Awake()` handles duplicate detection, but timing could be an issue

## Quality Checks

✅ All methods identified and analyzed  
✅ Complex methods have adequate scenario coverage  
✅ Mock requirements clearly identified  
✅ Test priorities established  
✅ Unity-Specific considerations documented  
✅ Static methods and reflection-accessible methods prioritized  
✅ Log assertion requirements documented  
✅ Singleton state management patterns identified  
✅ Resources loading patterns documented  
✅ Implementation bug indicators identified  

## Test Generation Notes

When generating tests for `GlobalConstants`:

1. **Start with singleton instance verification** - Test that singleton is created and accessible
2. **Test configuration loading paths** - Override, base, and default scenarios
3. **Use reflection for private methods** - `LoadGlobalConstantsConfiguration`, `CreateDefaultConfig`, `DoesResourceAssetExist`
4. **Mock Resources.Load** - Or provide test Resources folder structure
5. **Expect log messages** - Use `LogAssert.Expect()` for all expected logs
6. **Test singleton state isolation** - Reset singleton state between tests
7. **Test through public API** - Use `Instance` property and `IsInitialized` property for verification
8. **Handle Unity lifecycle** - Use `AddComponent<T>()` to trigger `Awake()`, don't call directly

