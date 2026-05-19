using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Computes a deterministic, schema-shape-sensitive hash of one or more DTO root types.
  ///   Hash changes when fields are added/removed/renamed/retyped or when a container kind flips
  ///   (List vs array vs HashSet vs Dictionary). Stable across field reorder.
  ///   Returns <see cref="WalkerResult"/> with diagnostics; no static logger.
  /// </summary>
  public sealed class SchemaHashWalker
  {
    /// <summary>SHA-256 truncated to first 12 hex chars. Collision risk acceptable for a tripwire (~2^48 distinct shapes before birthday-paradox concern).</summary>
    public const int HashPrefixLength = 12;

    /// <summary>
    ///   Logical property names automatically excluded from the hash when a root implements <see cref="ISchemaStampedSaveData"/>.
    ///   Matching is by logical property name, NOT raw backing-field name — <c>[field: SerializeField] public int SaveVersion { get; set; }</c>
    ///   generates a <c>&lt;SaveVersion&gt;k__BackingField</c>, and <see cref="IMemberEnumerator.LogicalNameOf"/> collapses
    ///   that to <c>"SaveVersion"</c> before this check runs.
    ///   Rationale: scaffold metadata is not domain shape. Including it would make every domain hash depend on framework metadata,
    ///   and future framework-side metadata additions would force every domain to re-pin its drift constant.
    /// </summary>
    internal static readonly HashSet<string> ExcludedSchemaStampMembers = new HashSet<string>(StringComparer.Ordinal)
    {
      nameof(ISchemaStampedSaveData.SaveVersion),
      nameof(ISchemaStampedSaveData.SaveSchemaHash)
    };

    public static Builder For(params Type[] roots)
    {
      if (roots == null || roots.Length == 0) throw new ArgumentException("At least one root type is required.", nameof(roots));
      return new Builder(roots);
    }

    public sealed class Builder
    {
      private readonly Type[] _roots;
      private IReadOnlyCollection<string> _namespacePrefixes = Array.Empty<string>();
      private IMemberEnumerator _enumerator = ES3MemberEnumerator.Instance;

      internal Builder(Type[] roots) { _roots = roots; }

      public Builder WithNamespacePrefixes(IEnumerable<string> prefixes)
      {
        if (prefixes == null) throw new ArgumentNullException(nameof(prefixes));
        _namespacePrefixes = prefixes.ToArray();
        return this;
      }

      public Builder WithMemberEnumerator(IMemberEnumerator enumerator)
      {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        return this;
      }

      public WalkerResult Compute()
      {
        var perType = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var diagnostics = new List<WalkerDiagnostic>();
        var visited = new HashSet<Type>();

        foreach (var root in _roots)
          Visit(root, perType, diagnostics, visited);

        var sb = new StringBuilder();
        foreach (var kv in perType)
          sb.Append(kv.Key).Append('|').Append(kv.Value).Append('\n');

        return new WalkerResult(ComputeHash(sb.ToString()), diagnostics);
      }

      private void Visit(Type type, SortedDictionary<string, string> perType, List<WalkerDiagnostic> diagnostics, HashSet<Type> visited)
      {
        if (type == null) return;
        if (!visited.Add(type)) return;

        // Unwrap Nullable<T>: hash the wrapped type, not the Nullable wrapper.
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
        {
          Visit(underlying, perType, diagnostics, visited);
          return;
        }

        if (!ShouldRecord(type)) return;

        if (type.IsEnum)
        {
          var underlyingEnum = Enum.GetUnderlyingType(type);
          var names = Enum.GetNames(type);
          Array.Sort(names, StringComparer.Ordinal);
          perType[type.FullName ?? type.Name] = "enum:" + underlyingEnum.FullName + "|" + string.Join(";", names);
          return;
        }

        if (type.IsAbstract || type.IsInterface)
        {
          diagnostics.Add(new WalkerDiagnostic(WalkerDiagnosticKind.AbstractTypeSkipped, type.FullName, "", "abstract/interface type cannot be hashed"));
          return;
        }

        var members = _enumerator.GetSerializedMembers(type).ToList();
        var stampedExcluded = typeof(ISchemaStampedSaveData).IsAssignableFrom(type);

        var emitted = new List<string>(members.Count);
        foreach (var member in members)
        {
          var logicalName = _enumerator.LogicalNameOf(member);
          if (stampedExcluded && ExcludedSchemaStampMembers.Contains(logicalName)) continue;

          var memberType = GetMemberType(member);

          var containerKind = _enumerator.DescribeContainer(memberType);
          if (containerKind != null)
          {
            var elementType = ResolveElementType(memberType);
            emitted.Add(containerKind + ":" + logicalName + ":" + (elementType?.FullName ?? "?") + ";");
            if (elementType != null) Visit(elementType, perType, diagnostics, visited);
            // Dictionary<K,V> also recurses into key type.
            if (memberType.IsGenericType)
            {
              var genericArgs = memberType.GetGenericArguments();
              if (genericArgs.Length == 2)
              {
                Visit(genericArgs[0], perType, diagnostics, visited);
              }
            }
            continue;
          }

          if (memberType == typeof(object) || memberType.IsAbstract || memberType.IsInterface)
          {
            diagnostics.Add(new WalkerDiagnostic(
              WalkerDiagnosticKind.PolymorphicFieldSkipped, type.FullName, logicalName,
              "field declared as object/abstract/interface; cannot hash polymorphic surface"));
            emitted.Add("polymorphic:" + logicalName + ";");
            continue;
          }

          emitted.Add((memberType.FullName ?? memberType.Name) + ":" + logicalName + ";");
          Visit(memberType, perType, diagnostics, visited);
        }

        if (emitted.Count == 0)
          diagnostics.Add(new WalkerDiagnostic(WalkerDiagnosticKind.NoSerializedMembers, type.FullName, "", "no serialized members visible to enumerator"));

        perType[type.FullName ?? type.Name] = string.Concat(emitted);
      }

      private bool ShouldRecord(Type type)
      {
        // Leaves: primitives, string, decimal are always recorded by FullName in the parent's signature
        // but never recursed into — they have no schema-relevant inner shape.
        if (type.IsPrimitive) return false;
        if (type == typeof(string) || type == typeof(decimal)) return false;
        // Everything else is recorded ONLY if it's in a project namespace surface.
        // Framework types (UnityEngine.Vector3, System.DateTime, etc.) appear by FullName in parent
        // signatures but never recursed — their internal shape is owned by the framework, not us.
        return InNamespace(type);
      }

      private bool InNamespace(Type type)
      {
        var ns = type.Namespace;
        if (string.IsNullOrEmpty(ns)) return false;
        foreach (var prefix in _namespacePrefixes)
          if (ns.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
      }

      private static Type GetMemberType(MemberInfo m)
      {
        if (m is FieldInfo f) return f.FieldType;
        if (m is PropertyInfo p) return p.PropertyType;
        return typeof(object);
      }

      private static Type ResolveElementType(Type containerType)
      {
        if (containerType.IsArray) return containerType.GetElementType();
        if (containerType.IsGenericType)
        {
          var args = containerType.GetGenericArguments();
          // Dictionary<K,V> and IDictionary<K,V>: element type for hashing is V (value); key recursed separately by caller.
          if (args.Length == 2) return args[1];
          if (args.Length == 1) return args[0];
        }
        return null;
      }

      private static string ComputeHash(string input)
      {
        using (var sha = SHA256.Create())
        {
          var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
          var sb = new StringBuilder(HashPrefixLength);
          for (int i = 0; i < HashPrefixLength / 2; i++)
            sb.Append(bytes[i].ToString("X2"));
          return sb.ToString();
        }
      }
    }
  }
}
