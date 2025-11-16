using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.MessageBus.Interfaces;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.Core.Types
{
  /// <summary>
  ///   Serializable reference to a MessagePipe message type (IGameMessage).
  ///   Provides Odin-powered dropdown in the inspector.
  /// </summary>
  [Serializable]
  [InlineProperty]
  public sealed class MessageTypeReference : IEquatable<MessageTypeReference>
  {
    [SerializeField]
    [HideInInspector]
    private string assemblyQualifiedName;

    private Type cachedType;

    public MessageTypeReference() { }

    public MessageTypeReference(Type type)
    {
      Type = type;
    }

    /// <summary>
    ///   The message type (must implement IGameMessage).
    /// </summary>
    [HideLabel]
    [ValueDropdown(nameof(GetMessageTypes), ExpandAllMenuItems = false,
                    DropdownTitle = "Select Message Type",
                    NumberOfItemsBeforeEnablingSearch = 10)]
    [ShowInInspector]
    public Type Type
    {
      get
      {
        if (cachedType == null && !string.IsNullOrEmpty(assemblyQualifiedName))
        {
          // Try Type.GetType first (fastest if assembly is already loaded)
          cachedType = Type.GetType(assemblyQualifiedName);

          // Fallback: search through all loaded assemblies if Type.GetType fails
          // This handles cases where the assembly name in the qualified name doesn't match
          // or the assembly isn't loaded in the current context
          if (cachedType == null)
          {
            var fullTypeName = assemblyQualifiedName.Split(',')[0].Trim(); // Get full type name (namespace + type)
            var typeNameOnly = fullTypeName.Contains('.')
              ? fullTypeName.Substring(fullTypeName.LastIndexOf('.') + 1)
              : fullTypeName;

            // First try exact match with full type name
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
              try
              {
                cachedType = assembly.GetType(fullTypeName);
                if (cachedType != null)
                  break;
              }
              catch
              {
                // Skip assemblies that can't be loaded or don't have the type
                continue;
              }
            }

            // If still not found, try searching all types in all assemblies
            // This is slower but handles cases where assembly name doesn't match
            if (cachedType == null)
            {
              foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
              {
                try
                {
                  foreach (var type in assembly.GetTypes())
                  {
                    if (type.FullName == fullTypeName && typeof(IGameMessage).IsAssignableFrom(type))
                    {
                      cachedType = type;
                      break;
                    }
                  }
                  if (cachedType != null)
                    break;
                }
                catch
                {
                  // Skip assemblies that can't be loaded
                  continue;
                }
              }
            }
          }

          if (cachedType != null && !typeof(IGameMessage).IsAssignableFrom(cachedType))
          {
            Debug.LogError($"Type {cachedType.Name} does not implement IGameMessage");
            cachedType = null;
          }
        }
        return cachedType;
      }
      set
      {
        if (value != null && !typeof(IGameMessage).IsAssignableFrom(value))
          throw new ArgumentException($"Type {value.Name} must implement IGameMessage");

        cachedType = value;
        assemblyQualifiedName = value?.AssemblyQualifiedName;
      }
    }

    public bool IsValid => Type != null;

    public string Name => Type?.Name;

    public string FullName => Type?.FullName;

    public override string ToString() => Type?.Name ?? "(None)";

    public override bool Equals(object obj) => obj is MessageTypeReference other && Equals(other);

    public bool Equals(MessageTypeReference other)
    {
      if (other == null) return false;
      return assemblyQualifiedName == other.assemblyQualifiedName;
    }

    public override int GetHashCode() => assemblyQualifiedName?.GetHashCode() ?? 0;

    public static implicit operator Type(MessageTypeReference reference) => reference?.Type;

    public static implicit operator MessageTypeReference(Type type) => new MessageTypeReference(type);

#if UNITY_EDITOR
    private static IEnumerable<ValueDropdownItem<Type>> GetMessageTypes()
    {
      var messageTypes = new List<Type>();

      foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
      {
        try
        {
          foreach (var type in assembly.GetTypes())
          {
            if (typeof(IGameMessage).IsAssignableFrom(type) &&
                !type.IsInterface &&
                !type.IsAbstract)
            {
              messageTypes.Add(type);
            }
          }
        }
        catch
        {
          // Skip assemblies that can't be loaded
        }
      }

      // Group by namespace for better organization
      return messageTypes
        .OrderBy(t => t.Namespace)
        .ThenBy(t => t.Name)
        .Select(t => new ValueDropdownItem<Type>(
          $"{t.Namespace?.Split('.').Last() ?? "Global"}/{t.Name}",
          t
        ));
    }
#endif
  }
}

