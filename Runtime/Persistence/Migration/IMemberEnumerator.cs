using System.Collections.Generic;
using System.Reflection;
using System;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Parameterizes <see cref="SchemaHashWalker"/>'s view of "what counts as a serialized member".
  ///   The default <see cref="ES3MemberEnumerator"/> honors Unity + ES3 conventions
  ///   (instance fields including <c>[field: SerializeField]</c> auto-property backing fields, skipping
  ///   <see cref="System.NonSerializedAttribute"/>); alternative implementations can adapt for other serializers.
  /// </summary>
  public interface IMemberEnumerator
  {
    /// <summary>Returns the serialized members of <paramref name="type"/> in a deterministic order (by logical name, Ordinal).</summary>
    IEnumerable<MemberInfo> GetSerializedMembers(Type type);

    /// <summary>
    ///   Returns the logical name of <paramref name="member"/> — collapses auto-property backing
    ///   fields (<c>&lt;X&gt;k__BackingField</c>) to their property name (<c>X</c>) so the walker hashes
    ///   stable identifiers.
    /// </summary>
    string LogicalNameOf(MemberInfo member);

    /// <summary>
    ///   Returns a stable label for a container shape (e.g. <c>"List&lt;&gt;"</c>, <c>"T[]"</c>,
    ///   <c>"HashSet&lt;&gt;"</c>, <c>"Dictionary&lt;,&gt;"</c>), or null if <paramref name="containerType"/>
    ///   is not a recognized container.
    /// </summary>
    string DescribeContainer(Type containerType);
  }
}
