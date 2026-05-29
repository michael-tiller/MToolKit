using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Default <see cref="IMemberEnumerator"/> that mirrors what ES3 (and Unity's serializer) treat as
  ///   persisted state: all instance fields (public + non-public, including <c>[field: SerializeField]</c>
  ///   auto-property backing fields), excluding <see cref="System.NonSerializedAttribute"/>-tagged fields.
  ///   Public (not internal) per design: framework tests reference it directly without InternalsVisibleTo.
  /// </summary>
  public sealed class ES3MemberEnumerator : IMemberEnumerator
  {
    public static readonly ES3MemberEnumerator Instance = new ES3MemberEnumerator();

    private const BindingFlags InstanceFlags =
      BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public IEnumerable<MemberInfo> GetSerializedMembers(Type type)
    {
      if (type == null) throw new ArgumentNullException(nameof(type));

      return type.GetFields(InstanceFlags)
        .Where(f => !f.IsNotSerialized)
        .OrderBy(f => LogicalNameOf(f), StringComparer.Ordinal)
        .Cast<MemberInfo>();
    }

    public string LogicalNameOf(MemberInfo member)
    {
      if (member == null) throw new ArgumentNullException(nameof(member));
      var raw = member.Name;
      // Auto-property backing field: "<PropertyName>k__BackingField"
      if (raw.Length > 16 && raw[0] == '<' && raw.EndsWith("k__BackingField", StringComparison.Ordinal))
      {
        int close = raw.IndexOf('>');
        if (close > 1)
          return raw.Substring(1, close - 1);
      }
      return raw;
    }

    public string DescribeContainer(Type containerType)
    {
      if (containerType == null) return null;
      if (containerType.IsArray) return "T[]";
      if (!containerType.IsGenericType) return null;
      var generic = containerType.GetGenericTypeDefinition();
      if (generic == typeof(List<>)) return "List<>";
      if (generic == typeof(HashSet<>)) return "HashSet<>";
      if (generic == typeof(Dictionary<,>)) return "Dictionary<,>";
      if (generic == typeof(IList<>)) return "IList<>";
      if (generic == typeof(ICollection<>)) return "ICollection<>";
      if (generic == typeof(IEnumerable<>)) return "IEnumerable<>";
      if (generic == typeof(IDictionary<,>)) return "IDictionary<,>";
      return null;
    }
  }
}
