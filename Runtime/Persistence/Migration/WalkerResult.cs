using System.Collections.Generic;

namespace MToolKit.Runtime.Persistence.Migration
{
  /// <summary>Bundle returned from <see cref="SchemaHashWalker.Builder.Compute"/>: hash + observation list.</summary>
  public sealed class WalkerResult
  {
    public string Hash { get; }
    public IReadOnlyList<WalkerDiagnostic> Diagnostics { get; }

    public WalkerResult(string hash, IReadOnlyList<WalkerDiagnostic> diagnostics)
    {
      Hash = hash;
      Diagnostics = diagnostics;
    }
  }
}
