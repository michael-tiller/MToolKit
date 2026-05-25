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

    /// <summary>
    ///   Storage-typed wrapper for fields stored as <see cref="int"/> that map to an
    ///   integer-backed enum (which may be <c>int</c>- or <c>byte</c>-backed under the hood).
    ///   Bridges the storage type to the enum's underlying type internally so callers don't need
    ///   per-field casts.
    ///   <para>
    ///     Delegates to <see cref="Defined{TEnum}"/> for both the loaded value AND the fallback,
    ///     so an out-of-range fallback (programmer error) does not silently stamp bad data into
    ///     the field. In that pathological case the returned value is the underlying-zero member
    ///     of the enum, surfaced as an <see cref="int"/>.
    ///   </para>
    /// </summary>
    public static int ClampIntField<TEnum>(int storedValue, TEnum fallback) where TEnum : struct, Enum
    {
      TEnum loaded = (TEnum)Enum.ToObject(typeof(TEnum), storedValue);
      TEnum validatedFallback = Defined(fallback, default(TEnum));
      TEnum clamped = Defined(loaded, validatedFallback);
      return Convert.ToInt32(clamped);
    }

    /// <summary>
    ///   Storage-typed wrapper for fields stored as <see cref="byte"/> that map to an
    ///   integer-backed enum. See <see cref="ClampIntField{TEnum}"/> for the fallback-validation
    ///   contract.
    /// </summary>
    public static byte ClampByteField<TEnum>(byte storedValue, TEnum fallback) where TEnum : struct, Enum
    {
      TEnum loaded = (TEnum)Enum.ToObject(typeof(TEnum), storedValue);
      TEnum validatedFallback = Defined(fallback, default(TEnum));
      TEnum clamped = Defined(loaded, validatedFallback);
      return Convert.ToByte(clamped);
    }
  }
}
