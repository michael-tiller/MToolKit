using System;
using System.Collections.Generic;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Helpers for handling polymorphic references that may name a type the current build does
  ///   not contain (e.g., a save written by a newer build referencing a type that was renamed
  ///   or removed in the current schema).
  ///
  ///   <para>
  ///     SCOPE: This helper targets DTOs that store polymorphic type identity as a STRING
  ///     (assembly-qualified type name) and resolve it via <see cref="Type.GetType(string)"/>
  ///     at materialization time. It does NOT cover first-class <c>[SerializeReference]</c>
  ///     missing-type failures, which throw at ES3 deserialization before any migrator runs.
  ///     Phase E does not intercept those — see Phase E plan boundary B1.
  ///   </para>
  ///
  ///   <para>
  ///     The exact ES3 behavior on a missing <c>[SerializeReference]</c> target type
  ///     (silently nulls the field / throws / silently skips the entry) is not empirically
  ///     pinned — confirming it requires loading a fixture serialized by an assembly that
  ///     contains the type from a test assembly that does NOT. A documentation-only probe that
  ///     sketched this (<c>Es3PolymorphicMissingTypeProbeTests</c>) was removed as perpetually
  ///     skipped; wiring a real one needs a separate stripped-type test assembly. Until then,
  ///     treat a missing-type ref as throwing at the save-controller boundary (before the
  ///     migrator runs) and keep this helper scoped to string-name DTOs.
  ///   </para>
  /// </summary>
  public static class PolymorphicResolve
  {
    /// <summary>
    ///   Resolves an assembly-qualified type name without throwing. Returns <c>null</c> when
    ///   the name is null/empty, malformed, or names a type that is not loaded.
    /// </summary>
    public static Type TryResolve(string assemblyQualifiedTypeName)
    {
      if (string.IsNullOrEmpty(assemblyQualifiedTypeName))
        return null;
      return Type.GetType(assemblyQualifiedTypeName, throwOnError: false);
    }

    /// <summary>
    ///   Removes entries from <paramref name="items"/> whose type-name (as returned by
    ///   <paramref name="typeNameSelector"/>) does not resolve via <see cref="TryResolve"/>.
    ///   Returns the number of removed entries (callers feed this into
    ///   <see cref="TruncationEntry.DroppedItemCount"/>).
    ///
    ///   <para>
    ///     Requires a mutable, variable-size <see cref="IList{T}"/> (e.g. <see cref="List{T}"/>).
    ///     Fixed-size lists (e.g. arrays surfaced via <see cref="IList{T}"/>) throw
    ///     <see cref="NotSupportedException"/> from <c>RemoveAt</c>; this method catches that
    ///     and rewraps as <see cref="InvalidOperationException"/> with a message naming the
    ///     caller-provided list's runtime type.
    ///   </para>
    /// </summary>
    public static int FilterMissingTypes<T>(IList<T> items, Func<T, string> typeNameSelector) where T : class
    {
      if (items == null) throw new ArgumentNullException(nameof(items));
      if (typeNameSelector == null) throw new ArgumentNullException(nameof(typeNameSelector));

      int removed = 0;
      try
      {
        for (int i = items.Count - 1; i >= 0; i--)
        {
          T item = items[i];
          if (item == null) continue;

          string typeName = typeNameSelector(item);
          if (TryResolve(typeName) == null)
          {
            items.RemoveAt(i);
            removed++;
          }
        }
      }
      catch (NotSupportedException ex)
      {
        throw new InvalidOperationException(
          $"PolymorphicResolve.FilterMissingTypes requires a mutable, variable-size IList<T>; "
          + $"received {items.GetType().FullName} which threw NotSupportedException on RemoveAt. "
          + "Wrap fixed-size collections in a List<T> before calling.", ex);
      }

      return removed;
    }
  }
}
