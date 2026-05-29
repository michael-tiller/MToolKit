namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>Categorizes a schema-walker diagnostic so callers can route or filter.</summary>
  public enum WalkerDiagnosticKind
  {
    /// <summary>Field's declared type is <see cref="object"/>, an interface, or an abstract base; cannot hash a polymorphic surface.</summary>
    PolymorphicFieldSkipped,

    /// <summary>Type has no serialized members visible to the active <see cref="IMemberEnumerator"/>.</summary>
    NoSerializedMembers,

    /// <summary>Type is abstract or interface (e.g., as an element type); excluded from the hash.</summary>
    AbstractTypeSkipped
  }
}
