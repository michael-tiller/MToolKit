using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.MessageBus.Interfaces;
using Sirenix.OdinInspector;

namespace MToolKit.Runtime.Core.Types
{
  /// <summary>
  ///   Attribute that provides a dropdown of all IGameMessage types.
  ///   Use with SerializableType fields.
  /// </summary>
  public sealed class MessageTypeAttribute : ValueDropdownAttribute
  {
    public MessageTypeAttribute() : base(nameof(GetMessageTypes))
    {
      DoubleClickToConfirm = false;
      DrawDropdownForListElements = true;
      ExpandAllMenuItems = false;
      NumberOfItemsBeforeEnablingSearch = 10;
    }

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

      return messageTypes
        .OrderBy(t => t.Namespace)
        .ThenBy(t => t.Name)
        .Select(t => new ValueDropdownItem<Type>(
          $"{t.Namespace}/{t.Name}",
          t
        ));
    }
#endif
  }
}

