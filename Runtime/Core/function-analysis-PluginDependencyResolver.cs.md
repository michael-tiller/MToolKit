# Function Analysis: PluginDependencyResolver.cs

**File:** `MToolKit/Runtime/Core/PluginDependencyResolver.cs`  
**Date:** 2025-11-18 
**Total Functions Found:** 10 (8 methods + 2 exception classes)

## Summary

The `PluginDependencyResolver` class is responsible for resolving plugin dependency order using topological sorting. It handles circular dependency detection and missing dependency validation. This is a critical infrastructure component that ensures plugins are registered in the correct order based on their service dependencies. The class uses a graph-based approach with topological sorting to determine registration order, while runtime dependencies are handled separately by `AreDependenciesReady` checks.

## Function Inventory

### Public Methods
- Total: 1
- Complexity: Complex

### Private Methods
- Total: 7
- Complexity: Moderate to Complex

### Exception Classes
- Total: 2
- Complexity: Simple

## Detailed Function Analysis

### 1. ResolveOrder

**Type:** Public instance method  
**Location:** Lines 29-49  
**Export:** Public API  
**Complexity:** Complex

#### Signature
```csharp
public List<AbstractGamePlugin> ResolveOrder(List<AbstractGamePlugin> plugins)
```

#### Parameters
- `plugins: List<AbstractGamePlugin>` - The list of plugins to order. Can be null or empty.

#### Returns
- **Type:** `List<AbstractGamePlugin>`
- **Description:** Returns plugins in dependency-resolved order. Returns empty list if input is null or empty.

#### Dependencies
- **External:** `Serilog.ILogger` (static logger), `System.Linq`
- **Internal:** `BuildDependencyGraph()`, `PerformTopologicalSort()`
- **Side Effects:** Logs debug/warning messages, may throw `CircularDependencyException`

#### Testing Scenarios
1. **Happy Path:** 
   - Valid list of plugins with clear dependency chain
   - Plugins with no dependencies (independent plugins)
   - Plugins with multiple dependencies
   - Plugins with transitive dependencies (A depends on B, B depends on C)

2. **Edge Cases:**
   - Null input list
   - Empty input list
   - Single plugin (no dependencies to resolve)
   - Plugins with no dependencies (all independent)

3. **Error Cases:**
   - Circular dependencies (should throw `CircularDependencyException`)
   - Plugins with missing dependencies (handled at runtime, not here)
   - Plugins with external service dependencies (should be handled gracefully)

4. **Log Assertion Cases:**
   - Warning log when plugins is null or empty
   - Debug log with plugin count at start
   - Debug log with resolved order at end

#### Mock Requirements
- No mocks needed - pure algorithm with logging
- Can test with concrete plugin instances or test doubles
- **CRITICAL**: Create test plugins that implement `IDependencyDeclaration` interface
- **CRITICAL**: Test plugins should have `RequiredServices` property returning `IEnumerable<Type>`

---

### 2. BuildDependencyGraph

**Type:** Private instance method  
**Location:** Lines 51-126  
**Export:** Internal  
**Complexity:** Complex

#### Signature
```csharp
private (Dictionary<Type, List<Type>> graph, Dictionary<Type, int> inDegree, Dictionary<Type, AbstractGamePlugin> pluginTypeMap)
  BuildDependencyGraph(List<AbstractGamePlugin> plugins)
```

#### Parameters
- `plugins: List<AbstractGamePlugin>` - The list of plugins to build graph from

#### Returns
- **Type:** `(Dictionary<Type, List<Type>> graph, Dictionary<Type, int> inDegree, Dictionary<Type, AbstractGamePlugin> pluginTypeMap)`
- **Description:** Returns tuple containing:
  - `graph`: Adjacency list representation of dependency graph (provider -> dependents)
  - `inDegree`: In-degree count for each plugin type
  - `pluginTypeMap`: Mapping from plugin type to plugin instance

#### Dependencies
- **External:** `System.Collections.Generic`, `System.Linq`, `Serilog.ILogger`
- **Internal:** `IsExternallyRegisteredService()`, `FindServiceProvider()`, `WouldCreateCircularDependency()`
- **Side Effects:** Logs debug/verbose messages about dependency building

#### Testing Scenarios
1. **Happy Path:**
   - Plugins with clear dependency chains
   - Plugins with external service dependencies (should be skipped in graph)
   - Plugins with config dependencies (should be skipped)
   - Plugins with circular dependencies (should be detected and handled)

2. **Edge Cases:**
   - Plugins that don't implement `IDependencyDeclaration` (should be skipped)
   - Plugins with empty `RequiredServices`
   - Plugins with config types in `RequiredServices` (should be skipped)
   - Plugins with `ScriptableObject` dependencies (should be skipped)

3. **Error Cases:**
   - Null plugins in list (should handle gracefully)
   - Plugins with null `RequiredServices` (should handle gracefully)

4. **Log Assertion Cases:**
   - Debug logs for each plugin's dependencies
   - Verbose logs for skipped config dependencies
   - Debug logs for externally registered services
   - Debug logs for circular dependencies (handled at runtime)

#### Mock Requirements
- Test plugins implementing `IDependencyDeclaration`
- Test plugins with various `RequiredServices` configurations
- **CRITICAL**: Test with plugins that provide services (DomainPlugin, ConfigPlugin patterns)
- **CRITICAL**: Test with plugins that have externally registered service dependencies

---

### 3. FindServiceProvider

**Type:** Private instance method  
**Location:** Lines 128-135  
**Export:** Internal  
**Complexity:** Simple

#### Signature
```csharp
private AbstractGamePlugin FindServiceProvider(List<AbstractGamePlugin> plugins, Type serviceType)
```

#### Parameters
- `plugins: List<AbstractGamePlugin>` - List of plugins to search
- `serviceType: Type` - The service type to find provider for

#### Returns
- **Type:** `AbstractGamePlugin`
- **Description:** Returns the plugin that provides the service, or null if not found

#### Dependencies
- **External:** None
- **Internal:** `IsServiceProvider()`
- **Side Effects:** None

#### Testing Scenarios
1. **Happy Path:**
   - Service provider found in list
   - Multiple plugins, correct one returned
   - Service provider not found (returns null)

2. **Edge Cases:**
   - Empty plugin list
   - Null serviceType (should handle gracefully or throw)
   - Service provided by multiple plugins (first match returned)

3. **Error Cases:**
   - Null plugins list (should throw `ArgumentNullException` or `NullReferenceException`)

#### Mock Requirements
- Test plugins with various service provider patterns
- **CRITICAL**: Test with DomainPlugin<TService, TInterface> pattern
- **CRITICAL**: Test with ConfigPlugin<TService, TInterface, TConfig> pattern
- **CRITICAL**: Test with hardcoded plugin names (PlayerPlugin, InventoryPlugin, etc.)

---

### 4. IsExternallyRegisteredService

**Type:** Private instance method  
**Location:** Lines 137-149  
**Export:** Internal  
**Complexity:** Simple

#### Signature
```csharp
private bool IsExternallyRegisteredService(Type serviceType)
```

#### Parameters
- `serviceType: Type` - The service type to check

#### Returns
- **Type:** `bool`
- **Description:** Returns true if the service is registered externally in GameInstaller

#### Dependencies
- **External:** None
- **Internal:** None
- **Side Effects:** None

#### Testing Scenarios
1. **Happy Path:**
   - Known externally registered services (IInventoryService, ITilemapService, etc.)
   - Services not externally registered (returns false)

2. **Edge Cases:**
   - Null serviceType (should handle gracefully or throw)
   - Service with same name but different namespace
   - Service types that match by name but not by type

3. **Error Cases:**
   - None (pure function)

#### Mock Requirements
- Test with various service types
- **CRITICAL**: Test all hardcoded service names:
  - IInventoryService
  - ITilemapService
  - IMouseOverService
  - INavigationService
  - IOneShotAudioService
  - IInventoryPanelCoordinator

---

### 5. WouldCreateCircularDependency

**Type:** Private instance method  
**Location:** Lines 151-178  
**Export:** Internal  
**Complexity:** Moderate

#### Signature
```csharp
private bool WouldCreateCircularDependency(Type providerType, Type dependentType, Dictionary<Type, List<Type>> graph)
```

#### Parameters
- `providerType: Type` - The provider plugin type
- `dependentType: Type` - The dependent plugin type
- `graph: Dictionary<Type, List<Type>>` - Current dependency graph

#### Returns
- **Type:** `bool`
- **Description:** Returns true if adding providerType -> dependentType edge would create a cycle

#### Dependencies
- **External:** `System.Collections.Generic` (HashSet, Stack)
- **Internal:** None
- **Side Effects:** None

#### Testing Scenarios
1. **Happy Path:**
   - No circular dependency (returns false)
   - Direct circular dependency (A -> B, checking B -> A)
   - Indirect circular dependency (A -> B -> C, checking C -> A)
   - Complex circular dependency chains

2. **Edge Cases:**
   - Empty graph
   - Single node graph
   - Disconnected graph components
   - Self-loop (providerType == dependentType)

3. **Error Cases:**
   - Null graph (should throw `ArgumentNullException` or `NullReferenceException`)
   - Null providerType or dependentType

#### Mock Requirements
- Build test graphs with various structures
- **CRITICAL**: Test DFS traversal logic with stack-based implementation
- **CRITICAL**: Test visited set to prevent infinite loops

---

### 6. IsServiceProvider

**Type:** Private instance method  
**Location:** Lines 180-228  
**Export:** Internal  
**Complexity:** Complex

#### Signature
```csharp
private bool IsServiceProvider(AbstractGamePlugin plugin, Type serviceType)
```

#### Parameters
- `plugin: AbstractGamePlugin` - The plugin to check
- `serviceType: Type` - The service type to check for

#### Returns
- **Type:** `bool`
- **Description:** Returns true if the plugin provides the specified service

#### Dependencies
- **External:** `System.Reflection` (implicit via GetType, BaseType, GetGenericTypeDefinition, GetGenericArguments)
- **Internal:** None
- **Side Effects:** None

#### Testing Scenarios
1. **Happy Path:**
   - DomainPlugin<TService, TInterface> pattern (matches service type)
   - ConfigPlugin<TService, TInterface, TConfig> pattern (matches service type)
   - Hardcoded plugin names (PlayerPlugin, InventoryPlugin, etc.)
   - Naming convention matching

2. **Edge Cases:**
   - Plugin with generic base type but wrong generic arguments
   - Plugin with non-generic base type
   - Plugin with null base type
   - Service type matching interface but not concrete type
   - Service type matching concrete type but not interface
   - Naming convention partial matches

3. **Error Cases:**
   - Null plugin (should throw `ArgumentNullException` or `NullReferenceException`)
   - Null serviceType (should handle gracefully or throw)
   - Plugin type that causes TypeLoadException (handled by name checking)

4. **Reflection Cases:**
   - Generic type definition checking
   - Generic argument extraction
   - Type name string matching as fallback

#### Mock Requirements
- Test plugins with various inheritance patterns
- **CRITICAL**: Test DomainPlugin<TService, TInterface> inheritance
- **CRITICAL**: Test ConfigPlugin<TService, TInterface, TConfig> inheritance
- **CRITICAL**: Test hardcoded plugin name checks:
  - PlayerPlugin -> IPlayerService, IRangeCalculationService
  - InventoryPlugin -> IInventoryService
  - TooltipPlugin -> ITooltipSystem
  - NavigationPlugin -> INavigationService
- **CRITICAL**: Test naming convention fallback logic

---

### 7. PerformTopologicalSort

**Type:** Private instance method  
**Location:** Lines 231-284  
**Export:** Internal  
**Complexity:** Complex

#### Signature
```csharp
private List<AbstractGamePlugin> PerformTopologicalSort(
  List<AbstractGamePlugin> plugins,
  Dictionary<Type, List<Type>> graph,
  Dictionary<Type, int> inDegree,
  Dictionary<Type, AbstractGamePlugin> pluginTypeMap)
```

#### Parameters
- `plugins: List<AbstractGamePlugin>` - Original plugin list
- `graph: Dictionary<Type, List<Type>>` - Dependency graph adjacency list
- `inDegree: Dictionary<Type, int>` - In-degree count for each plugin type
- `pluginTypeMap: Dictionary<Type, AbstractGamePlugin>` - Type to plugin instance mapping

#### Returns
- **Type:** `List<AbstractGamePlugin>`
- **Description:** Returns plugins in topological order. Throws `CircularDependencyException` if cycle detected.

#### Dependencies
- **External:** `System.Collections.Generic` (Queue), `System.Linq`, `Serilog.ILogger`
- **Internal:** `LogDependencyGraph()`
- **Side Effects:** Logs debug/verbose/error messages, throws `CircularDependencyException`

#### Testing Scenarios
1. **Happy Path:**
   - Simple linear dependency chain (A -> B -> C)
   - Multiple independent chains
   - Complex DAG with multiple paths
   - Plugins with no dependencies (should be processed first)

2. **Edge Cases:**
   - Empty plugin list
   - Single plugin
   - All plugins independent (no edges in graph)
   - Plugins not in pluginTypeMap (should handle gracefully)

3. **Error Cases:**
   - Circular dependency detected (should throw `CircularDependencyException`)
   - Mismatch between processed count and plugin count
   - Null parameters (should throw appropriate exceptions)

4. **Log Assertion Cases:**
   - Debug log with plugin count at start
   - Debug log with initial queue (no dependencies)
   - Verbose logs for each plugin added to result
   - Verbose logs for dependents added to queue
   - Error log when circular dependency detected
   - Debug log with dependency graph when error occurs

5. **Topological Sort Algorithm Cases:**
   - Queue initialization with zero in-degree nodes
   - In-degree decrementing logic
   - Queue processing order
   - Result ordering verification

#### Mock Requirements
- Build test graphs with known topological orders
- **CRITICAL**: Test Kahn's algorithm implementation
- **CRITICAL**: Test circular dependency detection
- **CRITICAL**: Verify exception message includes unprocessed plugin names

---

### 8. LogDependencyGraph

**Type:** Private instance method  
**Location:** Lines 286-295  
**Export:** Internal  
**Complexity:** Simple

#### Signature
```csharp
private void LogDependencyGraph(Dictionary<Type, List<Type>> graph, Dictionary<Type, AbstractGamePlugin> pluginTypeMap)
```

#### Parameters
- `graph: Dictionary<Type, List<Type>>` - Dependency graph to log
- `pluginTypeMap: Dictionary<Type, AbstractGamePlugin>` - Type to plugin mapping

#### Returns
- **Type:** `void`
- **Description:** Logs the dependency graph structure for debugging

#### Dependencies
- **External:** `System.Linq`, `Serilog.ILogger`
- **Internal:** None
- **Side Effects:** Logs debug messages

#### Testing Scenarios
1. **Happy Path:**
   - Graph with multiple edges
   - Graph with single edge
   - Empty graph

2. **Edge Cases:**
   - Graph with plugins not in pluginTypeMap (should use type name)
   - Null graph (should handle gracefully or throw)
   - Null pluginTypeMap (should handle gracefully or throw)

3. **Log Assertion Cases:**
   - Debug log with "Dependency graph:" header
   - Debug logs for each graph edge in format "fromPlugin -> toPlugin1, toPlugin2"

#### Mock Requirements
- Build test graphs with various structures
- **CRITICAL**: Verify log format matches expected structure

---

### 9. CircularDependencyException

**Type:** Exception class  
**Location:** Lines 301-305  
**Export:** Public  
**Complexity:** Simple

#### Signature
```csharp
public class CircularDependencyException : Exception
{
  public CircularDependencyException(string message) : base(message) { }
  public CircularDependencyException(string message, Exception innerException) : base(message, innerException) { }
}
```

#### Parameters
- Constructor 1: `message: string` - Exception message
- Constructor 2: `message: string`, `innerException: Exception` - Exception message and inner exception

#### Returns
- **Type:** Exception instance
- **Description:** Exception thrown when circular dependencies are detected

#### Dependencies
- **External:** `System.Exception`
- **Internal:** None
- **Side Effects:** None

#### Testing Scenarios
1. **Happy Path:**
   - Exception creation with message
   - Exception creation with message and inner exception
   - Exception message retrieval
   - Exception inner exception retrieval

2. **Edge Cases:**
   - Null message (should be handled by base Exception class)
   - Null inner exception (should be handled by base Exception class)

#### Mock Requirements
- No mocks needed - simple exception class
- Test exception throwing and catching
- **CRITICAL**: Verify exception is thrown in `PerformTopologicalSort` when circular dependency detected

---

### 10. MissingDependencyException

**Type:** Exception class  
**Location:** Lines 310-319  
**Export:** Public  
**Complexity:** Simple

#### Signature
```csharp
public class MissingDependencyException : Exception
{
  public List<(AbstractGamePlugin plugin, Type missingType)> MissingDependencies { get; }
  
  public MissingDependencyException(string message, List<(AbstractGamePlugin plugin, Type missingType)> missingDependencies)
    : base(message)
  {
    MissingDependencies = missingDependencies;
  }
}
```

#### Parameters
- `message: string` - Exception message
- `missingDependencies: List<(AbstractGamePlugin plugin, Type missingType)>` - List of missing dependencies

#### Returns
- **Type:** Exception instance
- **Description:** Exception thrown when required dependencies are missing

#### Dependencies
- **External:** `System.Exception`, `System.Collections.Generic`
- **Internal:** None
- **Side Effects:** None

#### Testing Scenarios
1. **Happy Path:**
   - Exception creation with message and missing dependencies
   - MissingDependencies property retrieval
   - Exception message retrieval

2. **Edge Cases:**
   - Null message (should be handled by base Exception class)
   - Null missingDependencies list
   - Empty missingDependencies list
   - Multiple missing dependencies

#### Mock Requirements
- No mocks needed - simple exception class
- Test exception throwing and catching
- **CRITICAL**: Note: This exception is defined but not currently thrown in the code - may be for future use

---

## Testing Strategy Recommendations

### High Priority Functions

1. **ResolveOrder** - Main public API, must be thoroughly tested
2. **PerformTopologicalSort** - Core algorithm, critical for correctness
3. **BuildDependencyGraph** - Complex logic, many edge cases
4. **IsServiceProvider** - Complex reflection logic, multiple patterns
5. **WouldCreateCircularDependency** - Critical for cycle detection

### Test Data Factory Strategy

**CRITICAL**: Create test plugin classes that implement `IDependencyDeclaration`:

```csharp
public class TestGamePlugin : AbstractGamePlugin, IDependencyDeclaration
{
  public IEnumerable<Type> RequiredServices { get; set; }
  public IEnumerable<Type> OptionalServices { get; set; }
  
  // For testing service provider patterns
  public Type ServiceType { get; set; }
  public Type InterfaceType { get; set; }
}
```

**CRITICAL**: Create helper methods for building test dependency graphs:
- `CreateLinearDependencyChain(int count)` - Creates A -> B -> C -> ...
- `CreateCircularDependency(int count)` - Creates circular chain
- `CreateIndependentPlugins(int count)` - Creates plugins with no dependencies
- `CreateComplexDAG()` - Creates complex directed acyclic graph

### Mock Strategy

- **No external dependencies to mock** - Pure algorithm with logging
- **CRITICAL**: Use concrete test plugin instances instead of mocks
- **CRITICAL**: Test with real `Type` objects, not mocked types
- **CRITICAL**: Test with real `AbstractGamePlugin` instances or test doubles

### Test File Organization

Suggested structure:
- `PluginDependencyResolverTests.cs` - Main test class
- `PluginDependencyResolverTestData.cs` - Test data factory methods
- Test methods organized by function being tested

### Parameterized Testing Opportunities

- **ResolveOrder**: Test with various plugin counts and dependency structures
- **BuildDependencyGraph**: Test with various dependency patterns
- **IsServiceProvider**: Test with various plugin inheritance patterns
- **WouldCreateCircularDependency**: Test with various graph structures

---

## Special Considerations

### Static Logger Pattern
- **CRITICAL**: Uses static lazy logger initialization - safe for testing
- **CRITICAL**: Log messages should be verified with `LogAssert.Expect()` where appropriate
- **CRITICAL**: Multiple log messages per method call - count them correctly

### Pure Algorithm Class
- **No Unity-specific dependencies** - Can be tested in EditMode
- **No MonoBehaviour dependencies** - No GameObject/Component lifecycle concerns
- **No VContainer dependencies** - No DI container setup needed
- **No async operations** - Synchronous testing only

### Graph Algorithm Testing
- **CRITICAL**: Test topological sort correctness with known orderings
- **CRITICAL**: Test cycle detection with various cycle structures
- **CRITICAL**: Test edge cases: empty graphs, single nodes, disconnected components
- **CRITICAL**: Verify algorithm handles all nodes (no nodes left unprocessed in valid cases)

### Reflection-Based Service Provider Detection
- **CRITICAL**: Test generic type checking logic
- **CRITICAL**: Test fallback to name-based matching
- **CRITICAL**: Test with various plugin inheritance patterns
- **CRITICAL**: Test hardcoded plugin name checks

### Exception Testing
- **CRITICAL**: Verify `CircularDependencyException` is thrown with correct message
- **CRITICAL**: Verify exception message includes unprocessed plugin names
- **CRITICAL**: Test exception in context of full resolution flow
- **CRITICAL**: Note that `MissingDependencyException` is defined but not currently used

### Null Safety Analysis

**POTENTIAL BUGS IDENTIFIED:**

1. **ResolveOrder** - Line 31: Checks for null plugins, but doesn't validate individual plugins in list
   - **Risk**: Null plugins in list could cause `NullReferenceException` in `BuildDependencyGraph`
   - **Recommendation**: Add null check in loop or validate input list

2. **BuildDependencyGraph** - Line 59: Iterates over plugins without null check
   - **Risk**: Null plugin in list causes `NullReferenceException` on `plugin.GetType()`
   - **Recommendation**: Add null check: `if (plugin == null) continue;`

3. **FindServiceProvider** - Line 130: No null check for plugins parameter
   - **Risk**: Null plugins list causes `NullReferenceException`
   - **Recommendation**: Add null check or document that caller must ensure non-null

4. **IsServiceProvider** - Line 182: No null check for plugin parameter
   - **Risk**: Null plugin causes `NullReferenceException` on `plugin.GetType()`
   - **Recommendation**: Add null check: `if (plugin == null) return false;`

5. **WouldCreateCircularDependency** - Line 156: No null check for graph parameter
   - **Risk**: Null graph causes `NullReferenceException` on `graph.ContainsKey()`
   - **Recommendation**: Add null check or document requirement

6. **PerformTopologicalSort** - Line 237: No null checks for parameters
   - **Risk**: Null parameters cause `NullReferenceException`
   - **Recommendation**: Add null checks for all parameters

### Thread Safety Analysis

- **Not thread-safe** - Uses mutable dictionaries and lists
- **Not designed for concurrent access** - Single-threaded algorithm
- **No synchronization** - Should only be called from single thread

### Collection Safety Analysis

- **Modifies inDegree dictionary** - Line 259: `inDegree[dependent]--`
- **Safe for single-threaded use** - No concurrent modifications expected
- **Graph structure not modified** - Only read during topological sort

### Validation Analysis

- **ResolveOrder**: Validates null/empty input, but not individual plugin validity
- **BuildDependencyGraph**: No validation of plugin list contents
- **FindServiceProvider**: No validation of parameters
- **IsServiceProvider**: No validation of plugin parameter
- **WouldCreateCircularDependency**: No validation of graph parameter
- **PerformTopologicalSort**: No validation of parameters

**Recommendation**: Add comprehensive parameter validation to all public and private methods to provide clear error messages instead of `NullReferenceException`.

---

## Quality Checks

✅ All public functions identified  
✅ Complex functions have detailed scenario coverage  
✅ Mock requirements clearly identified  
✅ Test priorities established  
✅ Unity-specific considerations noted (none needed - pure algorithm)  
✅ Static logger pattern identified  
✅ Reflection usage documented  
✅ Exception behavior documented  
✅ Null safety analysis completed  
✅ Thread safety analysis completed  
✅ Collection safety analysis completed  
✅ Validation analysis completed  

---

## Implementation Bug Recommendations

1. **Add null validation** to all methods that accept collections or objects
2. **Add null checks** in loops that iterate over collections
3. **Consider using ArgumentNullException** instead of allowing NullReferenceException
4. **Add validation** for plugin list contents (no null plugins)
5. **Document thread safety** requirements (single-threaded only)

