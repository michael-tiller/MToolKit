namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>
  ///   One observation produced by <see cref="SchemaHashWalker"/> during a hash computation.
  ///   Returned in <see cref="WalkerResult.Diagnostics"/> so callers can log without the walker
  ///   needing a static logger.
  /// </summary>
  public sealed class WalkerDiagnostic
  {
    public WalkerDiagnosticKind Kind { get; }
    public string TypeFullName { get; }
    public string FieldName { get; }
    public string Message { get; }

    public WalkerDiagnostic(WalkerDiagnosticKind kind, string typeFullName, string fieldName, string message)
    {
      Kind = kind;
      TypeFullName = typeFullName;
      FieldName = fieldName;
      Message = message;
    }
  }
}
