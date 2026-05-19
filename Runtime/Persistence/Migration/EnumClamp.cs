using System;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   Helpers for clamping deserialized enum values to a safe fallback when the loaded value
  ///   is outside the current enum's defined members (e.g. a save written by a newer build that
  ///   added an enum member which doesn't exist in the current schema).
  ///
  ///   Generalized from LAIRD's <c>PlayerProfileSystem.ReadConsentFromPrefs</c> pattern.
  /// </summary>
  public static class EnumClamp
  {
    /// <summary>
    ///   Returns <paramref name="value"/> if it is a defined member of <typeparamref name="TEnum"/>,
    ///   otherwise returns <paramref name="fallback"/>.
    ///
    ///   <para>
    ///     CAVEAT: Unsuitable for <c>[Flags]</c> enums. <see cref="Enum.IsDefined(Type, object)"/>
    ///     returns true only for exact named members, so bit combinations like <c>(A | B)</c>
    ///     clamp to <paramref name="fallback"/> even when both <c>A</c> and <c>B</c> are defined.
    ///     For <c>[Flags]</c>, mask the loaded value against a known-bits constant instead.
    ///   </para>
    /// </summary>
    public static TEnum Defined<TEnum>(TEnum value, TEnum fallback) where TEnum : struct, Enum
      => Enum.IsDefined(typeof(TEnum), value) ? value : fallback;
  }
}
