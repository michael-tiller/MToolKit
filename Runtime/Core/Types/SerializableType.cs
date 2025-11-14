using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Core.Types
{
  /// <summary>
  ///   Serializable wrapper for System.Type that works with Unity serialization.
  ///   Stores AssemblyQualifiedName internally but exposes the actual Type.
  /// </summary>
  [Serializable]
  public sealed class SerializableType : IEquatable<SerializableType>
  {
    [SerializeField]
    [HideInInspector]
    private string assemblyQualifiedName;

    private Type cachedType;

    public SerializableType() { }

    public SerializableType(Type type)
    {
      Type = type;
    }

    /// <summary>
    ///   The actual System.Type this represents.
    /// </summary>
    public Type Type
    {
      get
      {
        if (cachedType == null && !string.IsNullOrEmpty(assemblyQualifiedName))
          cachedType = Type.GetType(assemblyQualifiedName);
        return cachedType;
      }
      set
      {
        cachedType = value;
        assemblyQualifiedName = value?.AssemblyQualifiedName;
      }
    }

    /// <summary>
    ///   Check if the type is valid and loaded.
    /// </summary>
    public bool IsValid => Type != null;

    /// <summary>
    ///   Simple type name (without namespace).
    /// </summary>
    public string Name => Type?.Name;

    /// <summary>
    ///   Full type name (with namespace).
    /// </summary>
    public string FullName => Type?.FullName;

    public override string ToString() => Type?.Name ?? "(null)";

    public override bool Equals(object obj) => obj is SerializableType other && Equals(other);

    public bool Equals(SerializableType other)
    {
      if (other == null) return false;
      return assemblyQualifiedName == other.assemblyQualifiedName;
    }

    public override int GetHashCode() => assemblyQualifiedName?.GetHashCode() ?? 0;

    public static implicit operator Type(SerializableType serializableType) => serializableType?.Type;

    public static implicit operator SerializableType(Type type) => new SerializableType(type);
  }

  /// <summary>
  ///   Generic serializable type reference with constraint validation.
  /// </summary>
  [Serializable]
  public sealed class SerializableType<TBase> where TBase : class
  {
    [SerializeField]
    [HideInInspector]
    private string assemblyQualifiedName;

    private Type cachedType;

    public SerializableType() { }

    public SerializableType(Type type)
    {
      Type = type;
    }

    public Type Type
    {
      get
      {
        if (cachedType == null && !string.IsNullOrEmpty(assemblyQualifiedName))
        {
          cachedType = Type.GetType(assemblyQualifiedName);
          // Validate constraint
          if (cachedType != null && !typeof(TBase).IsAssignableFrom(cachedType))
          {
            Debug.LogError($"Type {cachedType} does not inherit from {typeof(TBase)}");
            cachedType = null;
          }
        }
        return cachedType;
      }
      set
      {
        if (value != null && !typeof(TBase).IsAssignableFrom(value))
          throw new ArgumentException($"Type {value} must inherit from {typeof(TBase)}");

        cachedType = value;
        assemblyQualifiedName = value?.AssemblyQualifiedName;
      }
    }

    public bool IsValid => Type != null;

    public string Name => Type?.Name;

    public string FullName => Type?.FullName;

    public override string ToString() => Type?.Name ?? "(null)";

    public static implicit operator Type(SerializableType<TBase> serializableType) => serializableType?.Type;
  }

#if UNITY_EDITOR
  /// <summary>
  ///   Helper class for editor dropdowns that filter types by base class/interface.
  /// </summary>
  public static class TypeUtility
  {
    public static IEnumerable<Type> GetTypesAssignableFrom<T>() where T : class
    {
      return GetTypesAssignableFrom(typeof(T));
    }

    public static IEnumerable<Type> GetTypesAssignableFrom(Type baseType)
    {
      var types = new List<Type>();

      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        try
        {
          foreach (var type in assembly.GetTypes())
          {
            if (baseType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
              types.Add(type);
          }
        }
        catch
        {
          // Skip assemblies that can't be loaded
        }
      }

      return types.OrderBy(t => t.FullName);
    }

    public static IEnumerable<string> GetTypeFullNames<T>() where T : class
    {
      return GetTypesAssignableFrom<T>().Select(t => t.FullName);
    }

    public static IEnumerable<ValueDropdownItem<Type>> GetTypeDropdown<T>() where T : class
    {
      return GetTypesAssignableFrom<T>().Select(t => new ValueDropdownItem<Type>(
        $"{t.Namespace}.{t.Name}",
        t
      ));
    }
  }
#endif
}

