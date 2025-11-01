@page utilities_system Utilities

@brief Extension Methods, Helpers, and Data Structures

# Utilities - Extension Methods, Helpers, and Data Structures

Utility classes, extensions, and helper functions for MToolKit.

## Purpose

The Utilities module provides:
- **Extension Methods** - Extension methods for common types
- **Helper Classes** - Reusable utility classes
- **Data Structures** - Custom data structures
- **Converters** - Type converters and parsers
- **Math Utilities** - Mathematical helper functions

## Structure

```
Utilities/
├── Extensions/                   # Extension methods
│   ├── IEnumerableExt.cs        # LINQ extensions
│   ├── ObjectPoolExtensions.cs  # Object pooling extensions
│   ├── StringExt.cs             # String extensions
│   └── VectorExtensions.cs      # Vector math extensions
├── DataStructures/              # Custom data structures
│   ├── SerializableDictionary.cs
│   └── GuidScriptableObject.cs
├── Converters/                   # Type converters
│   └── StringToNumberConverter.cs
└── Others/                       # Other utilities
```

## Key Files

### Extensions

- **`IEnumerableExt.cs`** - LINQ-style extensions for IEnumerable
- **`ObjectPoolExtensions.cs`** - Object pooling utility methods
- **`StringExt.cs`** - String manipulation and parsing
- **`VectorExtensions.cs`** - Vector math utilities

### Data Structures

- **`SerializableDictionary.cs`** - Dictionary that serializes in Unity
- **`GuidScriptableObject.cs`** - ScriptableObject with GUID

### Converters

- **`StringToNumberConverter.cs`** - String to number parsing

## Usage Examples

### Extension Methods

```csharp
using MToolKit.Runtime.Utilities.Extensions;

// IEnumerable extensions
var filtered = someEnumerable.WhereNotNull().DistinctBy(x => x.Id);

// String extensions
var number = "123".ToIntOrDefault(defaultValue: 0);
var formatted = value.ToStringInvariant();

// Vector extensions
var distance = positionA.DistanceTo(positionB);
var lerped = Vector3.Lerp(start, end, t);
```

### Serializable Dictionary

```csharp
using MToolKit.Runtime.Utilities.DataStructures;

[Serializable]
public class MyDictionary : SerializableDictionary<string, int>
{
    // Use like regular dictionary, but serializes in Unity
}

// Usage
var dict = new MyDictionary();
dict["key"] = 42;  // Serializes in Unity Inspector
```

### Type Converters

```csharp
using MToolKit.Runtime.Utilities.Converters;

// Convert string to number
var number = StringToNumberConverter.ToInt("123");
var floatValue = StringToNumberConverter.ToFloat("3.14");
```

### Object Pooling

```csharp
using MToolKit.Runtime.Utilities.Extensions;

// Pool objects for reuse
var pooledObject = ObjectPoolExtensions.GetPooledObject<GameObject>();
ObjectPoolExtensions.ReturnToPool(pooledObject);
```

## Dependencies

- **Unity** - Core Unity types
- **System** - .NET types and LINQ

## Integration Points

- **All Modules** - Utilities used across all modules
- **Components** - UI components use extensions
- **Core** - Core systems use utilities

## Design Patterns

- **Extension Pattern** - Extension methods for clean API
- **Utility Pattern** - Static utility methods
- **Pool Pattern** - Object pooling utilities

## Test Coverage

**Status**: ⚠️ **ZERO TESTS** (See TESTS_GOALS.md)

All utility classes should have tests:
- Extension methods
- Data structures
- Converters
- Helpers

## Known Issues

- Consider adding more extension methods
- Performance optimizations for some utilities
- Missing documentation for some methods

## Future Enhancements

- Performance profilers
- Memory allocation helpers
- Async utility extensions
- More data structures

