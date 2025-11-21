# Function Analysis: ConfigPlugin.cs

**File:** `MToolKit/Runtime/Core/Abstractions/ConfigPlugin.cs`  
**Date:** 2025-01-27  
**Total Functions Found:** 3 (2 concrete methods, 1 abstract method, 2 properties)

## Summary

`ConfigPlugin<TService, TInterface, TConfig>` is an abstract generic base class that extends `DomainPlugin<TService, TInterface>` to add configuration management capabilities. It provides config validation, registration, and dependency checking for plugins that manage services with ScriptableObject-based configuration.

**Inheritance Chain:**
- `ConfigPlugin<TService, TInterface, TConfig>` 
  - extends `DomainPlugin<TService, TInterface>`
    - extends `AbstractRuntimePlugin`
      - extends `AbstractGamePlugin` (MonoBehaviour)
      - implements `IRuntimeSystem`, `IRuntimePlugin`
    - implements `IDependencyDeclaration`

**Key Characteristics:**
- Generic class with three type parameters (service, interface, config)
- Config must be a ScriptableObject
- Validates config assignment before registration
- Registers config instance with VContainer
- Extends dependency checking to include config validation
- Abstract method requires derived classes to implement service creation

## Function Inventory

### Public Methods
- Total: 2
- Complexity: Simple to Moderate

### Abstract Methods
- Total: 1
- Complexity: N/A (implementation-dependent)

### Properties
- Total: 2
- Complexity: Simple

## Detailed Function Analysis

### 1. RequiredServices Property

**Type:** Property (override)  
**Location:** Lines 29  
**Access:** Public  
**Complexity:** Simple

#### Signature
```csharp
public override IEnumerable<Type> RequiredServices => Array.Empty<Type>();
```

#### Returns
- **Type:** `IEnumerable<Type>`
- **Description:** Returns an empty collection. Override in derived classes to specify custom required dependencies.

#### Dependencies
- **External:** `System.Collections.Generic`, `System`
- **Internal:** None
- **Side Effects:** None (pure property getter)

#### Testing Scenarios
1. **Happy Path:** 
   - Verify property returns empty collection by default
   - Verify derived classes can override to provide custom dependencies

2. **Edge Cases:**
   - Verify property is accessible without instance creation (static-like behavior)
   - Verify property can be overridden in derived classes

3. **Error Cases:**
   - None (pure property getter)

#### Mock Requirements
- None required for property access
- For testing derived classes, verify override behavior

#### Testing Considerations
- **Unity-Specific:** This is a property on a MonoBehaviour-derived class, but can be tested via reflection or by creating a test GameObject with a derived component
- **CRITICAL - Static Class Testing:** Not applicable - this is an instance property
- **CRITICAL - Reflection vs Direct Calls:** Can use direct property access for testing
- **CRITICAL - Class Visibility:** Class is public, no visibility issues

---

### 2. OptionalServices Property

**Type:** Property (override)  
**Location:** Lines 34  
**Access:** Public  
**Complexity:** Simple

#### Signature
```csharp
public override IEnumerable<Type> OptionalServices => Array.Empty<Type>();
```

#### Returns
- **Type:** `IEnumerable<Type>`
- **Description:** Returns an empty collection. Override in derived classes to specify custom optional dependencies.

#### Dependencies
- **External:** `System.Collections.Generic`, `System`
- **Internal:** None
- **Side Effects:** None (pure property getter)

#### Testing Scenarios
1. **Happy Path:** 
   - Verify property returns empty collection by default
   - Verify derived classes can override to provide custom dependencies

2. **Edge Cases:**
   - Verify property is accessible without instance creation (static-like behavior)
   - Verify property can be overridden in derived classes

3. **Error Cases:**
   - None (pure property getter)

#### Mock Requirements
- None required for property access
- For testing derived classes, verify override behavior

#### Testing Considerations
- **Unity-Specific:** This is a property on a MonoBehaviour-derived class, but can be tested via reflection or by creating a test GameObject with a derived component
- **CRITICAL - Static Class Testing:** Not applicable - this is an instance property
- **CRITICAL - Reflection vs Direct Calls:** Can use direct property access for testing
- **CRITICAL - Class Visibility:** Class is public, no visibility issues

---

### 3. Register Method

**Type:** Method (override)  
**Location:** Lines 40-58  
**Access:** Public  
**Complexity:** Moderate

#### Signature
```csharp
public override void Register(IContainerBuilder builder)
```

#### Parameters
- `builder: IContainerBuilder` - VContainer builder for service registration. Must not be null (VContainer will handle null checks).

#### Returns
- **Type:** `void`
- **Description:** Registers the config instance and calls base registration. Returns early if config is null.

#### Dependencies
- **External:** `VContainer`, `Serilog`, `UnityEngine`
- **Internal:** `base.Register()`, `log` (static logger), `config` (protected field), `gameObject` (Unity MonoBehaviour)
- **Side Effects:** 
  - Registers config instance with VContainer
  - Calls base Register() which registers service and plugin
  - Logs debug and error messages

#### Complexity Indicators
- **Cyclomatic Complexity:** 2 (simple if statement)
- **Branching Logic:** 1 if statement (null check with early return)
- **Loop Constructs:** None
- **Error Handling:** Early return on null config, logs error

#### Testing Scenarios
1. **Happy Path:** 
   - Register with valid config assigned
   - Verify config is registered with container
   - Verify base.Register() is called
   - Verify debug logs are written

2. **Edge Cases:**
   - Register with null config (should log error and return early)
   - Register multiple times (should handle gracefully)
   - Register with builder that already has registrations

3. **Error Cases:**
   - Null builder parameter (VContainer may throw, but not explicitly handled here)
   - Config is null (handled with early return and error log)

4. **Unity GameObject Lifecycle Cases:**
   - Register when GameObject is destroyed (should handle gracefully or throw appropriate exception)
   - Register when component is disabled (should still work)

5. **Log Assertion Cases:**
   - Expected debug log: "Registering {TypeName}"
   - Expected error log when config is null: "{ConfigTypeName} is not assigned! Make sure the config is assigned in the prefab."
   - Expected debug log: "{TypeName} registration completed"
   - **CRITICAL**: Use `LogAssert.Expect()` for all expected log messages

6. **VContainer Integration Cases:**
   - Verify config instance is registered with `builder.RegisterInstance(config).AsSelf()`
   - Verify base.Register() is called after config registration
   - Test with mock IContainerBuilder to verify registration calls

#### Mock Requirements
- **IContainerBuilder** - Mock or substitute to verify registration calls
- **TConfig** - Create test ScriptableObject instance for config
- **GameObject** - Create test GameObject with component attached
- **Serilog ILogger** - Mock logger or use LogAssert for log verification
- **CRITICAL**: Always register the class being tested in test container
- **CRITICAL**: Create fresh ContainerBuilder instances per test to avoid registration conflicts

#### Testing Considerations
- **Unity-Specific:** Requires GameObject creation with component attached
- **Unity-Specific:** Config must be a ScriptableObject instance
- **CRITICAL - Log Assertion Requirements:** This method logs debug and error messages that require LogAssert.Expect
- **CRITICAL - VContainer Exception Types:** VContainer may throw VContainerException on invalid registrations
- **CRITICAL - Null Parameter Handling:** Config null is handled, but builder null is not explicitly checked (VContainer may handle)
- **CRITICAL - Framework-Specific Behavior:** Relies on VContainer registration patterns and Unity MonoBehaviour lifecycle

#### Implementation Bug Detection
- **CRITICAL - Null Safety Analysis:** 
  - ✅ Config null is validated with early return
  - ⚠️ Builder null is not explicitly validated (relies on VContainer)
  - ⚠️ gameObject could be null if component is not attached (Unity scenario)
- **CRITICAL - Validation Analysis:** 
  - ✅ Config validation happens before registration
  - ✅ Error message is clear and actionable

---

### 4. AreDependenciesReady Method

**Type:** Method (override)  
**Location:** Lines 64-74  
**Access:** Public  
**Complexity:** Simple

#### Signature
```csharp
public override bool AreDependenciesReady(IObjectResolver resolver)
```

#### Parameters
- `resolver: IObjectResolver` - VContainer resolver for dependency checking. Must not be null (VContainer will handle null checks).

#### Returns
- **Type:** `bool`
- **Description:** Returns true if service can be resolved and config is not null. Logs dependency check results.

#### Dependencies
- **External:** `VContainer`, `Serilog`, `UnityEngine`
- **Internal:** `log` (static logger), `config` (protected field), `gameObject` (Unity MonoBehaviour)
- **Side Effects:** 
  - Attempts to resolve service from container
  - Logs debug messages

#### Complexity Indicators
- **Cyclomatic Complexity:** 1 (simple boolean logic)
- **Branching Logic:** None (single return with boolean expression)
- **Loop Constructs:** None
- **Error Handling:** None (relies on VContainer for resolver null handling)

#### Testing Scenarios
1. **Happy Path:** 
   - Service is registered and config is assigned
   - Verify returns true
   - Verify debug log is written

2. **Edge Cases:**
   - Service is registered but config is null (should return false)
   - Config is assigned but service is not registered (should return false)
   - Both service and config are available (should return true)
   - Neither service nor config are available (should return false)

3. **Error Cases:**
   - Null resolver parameter (VContainer may throw, but not explicitly handled here)
   - Resolver throws exception during TryResolve (should propagate)

4. **Unity GameObject Lifecycle Cases:**
   - Check when GameObject is destroyed (config may be null)
   - Check when component is disabled (should still work)

5. **Log Assertion Cases:**
   - Expected debug log: "Dependencies ready check: CanResolveService={0}, HasConfig={1}"
   - **CRITICAL**: Use `LogAssert.Expect()` for expected log messages

6. **VContainer Integration Cases:**
   - Test with resolver that has service registered
   - Test with resolver that doesn't have service registered
   - Verify TryResolve is called correctly

#### Mock Requirements
- **IObjectResolver** - Mock or substitute to control service resolution
- **TConfig** - Create test ScriptableObject instance for config
- **GameObject** - Create test GameObject with component attached
- **Serilog ILogger** - Mock logger or use LogAssert for log verification
- **CRITICAL**: Create test implementations that can control resolver behavior

#### Testing Considerations
- **Unity-Specific:** Requires GameObject creation with component attached
- **Unity-Specific:** Config must be a ScriptableObject instance
- **CRITICAL - Log Assertion Requirements:** This method logs debug messages that require LogAssert.Expect
- **CRITICAL - VContainer Exception Types:** VContainer may throw exceptions on invalid resolver usage
- **CRITICAL - Null Parameter Handling:** Resolver null is not explicitly checked (VContainer may handle)
- **CRITICAL - Framework-Specific Behavior:** Relies on VContainer resolver patterns

#### Implementation Bug Detection
- **CRITICAL - Null Safety Analysis:** 
  - ✅ Config null is checked
  - ⚠️ Resolver null is not explicitly validated (relies on VContainer)
  - ⚠️ gameObject could be null if component is not attached (Unity scenario)
- **CRITICAL - Validation Analysis:** 
  - ✅ Both service resolution and config presence are validated
  - ✅ Clear logging of dependency check results

---

### 5. CreateService Method (Abstract)

**Type:** Method (abstract override)  
**Location:** Lines 82  
**Access:** Protected  
**Complexity:** N/A (implementation-dependent)

#### Signature
```csharp
protected abstract override TService CreateService(IObjectResolver resolver);
```

#### Parameters
- `resolver: IObjectResolver` - The object resolver for dependency injection. Must not be null.

#### Returns
- **Type:** `TService`
- **Description:** Creates and returns a service instance. Implementation is provided by derived classes. The config is available as a protected field.

#### Dependencies
- **External:** `VContainer`
- **Internal:** `config` (protected field, available to derived implementations)
- **Side Effects:** Implementation-dependent (creates service instance)

#### Testing Scenarios
1. **Abstract Method Testing:**
   - Cannot test abstract method directly
   - Test through concrete derived class implementations
   - Verify derived classes properly implement the method
   - Verify config field is accessible in derived implementations

2. **Integration Testing:**
   - Test that Register() calls CreateService() through base class
   - Verify service creation uses config field
   - Test service creation with various resolver configurations

#### Mock Requirements
- **IObjectResolver** - Mock or substitute for dependency injection
- **TConfig** - Test ScriptableObject instance
- **Concrete Derived Class** - Create test implementation of ConfigPlugin

#### Testing Considerations
- **Unity-Specific:** Abstract method cannot be tested directly
- **CRITICAL - Reflection vs Direct Calls:** Cannot use reflection to test abstract method - must test through derived classes
- **CRITICAL - Class Visibility:** Method is protected, only accessible to derived classes
- **CRITICAL - Framework-Specific Behavior:** Method is called by base.Register() through VContainer factory pattern

---

## Testing Strategy Recommendations

### High Priority Functions

1. **Register Method** - Critical for plugin initialization and config registration
   - Test config validation (null check)
   - Test VContainer registration calls
   - Test base class integration
   - Test logging behavior

2. **AreDependenciesReady Method** - Critical for dependency validation
   - Test all combination scenarios (service + config states)
   - Test resolver integration
   - Test logging behavior

### Mock Strategy

**Recommended approach for mocking dependencies:**

- **CRITICAL**: Create TestData factory methods for consistent test object creation
  ```csharp
  public static class ConfigPluginTestData
  {
      public static GameObject CreateTestGameObject<T>() where T : MonoBehaviour
      {
          GameObject go = new GameObject();
          return go.AddComponent<T>();
      }
      
      public static TConfig CreateTestConfig<TConfig>() where TConfig : ScriptableObject
      {
          return ScriptableObject.CreateInstance<TConfig>();
      }
  }
  ```

- **CRITICAL**: Use ReflectionHelper with cached FieldInfo for accessing protected `config` field
  ```csharp
  private static readonly FieldInfo ConfigField = typeof(ConfigPlugin<,,>)
      .GetField("config", BindingFlags.NonPublic | BindingFlags.Instance);
  ```

- **CRITICAL**: Create test implementations of ConfigPlugin for testing abstract methods
  ```csharp
  public class TestConfigPlugin : ConfigPlugin<TestService, ITestService, TestConfig>
  {
      protected override TestService CreateService(IObjectResolver resolver)
      {
          return new TestService(config);
      }
  }
  ```

- **CRITICAL**: Use parameterized tests with [TestCase] for similar scenarios
  ```csharp
  [TestCase(true, true, true)]  // service registered, config assigned
  [TestCase(false, true, false)] // service not registered, config assigned
  [TestCase(true, false, false)] // service registered, config null
  [TestCase(false, false, false)] // neither available
  public void AreDependenciesReady_WhenVariousStates_ShouldReturnExpected(
      bool serviceRegistered, bool configAssigned, bool expected)
  ```

- **CRITICAL**: Use try/finally blocks for proper test isolation and cleanup
  ```csharp
  [TearDown]
  public void TearDown()
  {
      if (_testGameObject != null)
      {
          Object.DestroyImmediate(_testGameObject);
      }
      _resolver?.Dispose();
  }
  ```

- **CRITICAL**: Create unique class names to avoid namespace conflicts

### Test File Organization

**Suggested structure:**
```
Assets/TemplateGame/Source/Tests/Runtime/Core/Abstractions/
├── ConfigPluginTests.cs
├── ConfigPluginTestData.cs
└── TestImplementations/
    └── TestConfigPlugin.cs
```

## Special Considerations

### Unity MonoBehaviour Components

- **AVOID direct instance creation** - Use GameObject.AddComponent<T>() pattern
- **Lifecycle methods**: Register() can be called during plugin initialization
- **VContainer dependencies**: Register() uses VContainer builder, requires proper container setup
- **Static logger**: Uses static lazy logger pattern, safe for testing
- **CRITICAL**: Use `ContainerBuilder` (concrete class) not `IContainerBuilder` (interface) for VContainer tests
- **CRITICAL**: Use `using ILogger = Serilog.ILogger;` to avoid ambiguity with UnityEngine.ILogger

### Unity Plugin Systems

- **IGamePlugin and IRuntimePlugin**: ConfigPlugin extends AbstractRuntimePlugin which implements these interfaces
- **Plugin registration**: Register() method handles plugin registration with VContainer
- **Config validation**: Config must be assigned in prefab/Inspector
- **CRITICAL**: Identify lifecycle guard implementations - ConfigPlugin doesn't add guards, inherits from base classes
- **CRITICAL**: Note GameObject/Component lifecycle dependencies - requires GameObject with component attached
- **CRITICAL**: Identify VContainer dependency injection patterns - uses IContainerBuilder and IObjectResolver
- **CRITICAL**: Note classes that need explicit registration in test containers - test implementations need registration
- **CRITICAL**: Identify helper method patterns for creating isolated test containers

### Generic Type Constraints

- **TService**: Must be class and implement TInterface
- **TInterface**: Must be class (interface type)
- **TConfig**: Must be ScriptableObject
- **Testing**: Requires concrete type implementations for testing

### Protected Field Access

- **config field**: Protected field accessible to derived classes
- **Testing**: Use reflection to set/verify config field in tests
- **CRITICAL**: Use cached FieldInfo for performance optimization

### Logging Patterns

- **Static lazy logger**: Safe for testing, no instance required
- **Structured logging**: Uses ForGameObject().ForMethod() pattern
- **Log levels**: Debug and Error levels used
- **CRITICAL**: All log messages require LogAssert.Expect() in tests
- **CRITICAL**: Count log messages to match expected calls

### VContainer Integration

- **Registration pattern**: Registers config instance, then calls base.Register()
- **Service creation**: Base class uses CreateService as factory method
- **Dependency checking**: Uses IObjectResolver.TryResolve() for validation
- **CRITICAL**: Test with real VContainer ContainerBuilder for integration testing
- **CRITICAL**: Verify registration order (config before service)

## Quality Checks

✅ All public methods are identified  
✅ Complex functions have adequate scenario coverage  
✅ Mock requirements are clearly identified  
✅ Test priorities are established  
✅ Unity-Specific: Invalid GameObject/Component lifecycle scenarios are identified  
✅ Unity-Specific: Static methods and reflection-accessible methods are prioritized  
✅ CRITICAL: VContainer dependency injection patterns are identified and documented  
✅ CRITICAL: Required class registrations for testing are clearly specified  
✅ CRITICAL: Helper method patterns for isolated test containers are identified  
✅ CRITICAL: Parameterized test opportunities are identified  
✅ CRITICAL: Log assertion requirements are documented  
✅ CRITICAL: Protected field access patterns are documented

