using System;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   <see cref="IServiceProvider" /> that resolves nothing. GraphRunner passes this through to executor
  ///   contexts; the characterization graphs never resolve services, so null returns are correct.
  /// </summary>
  public sealed class NullServiceProvider : IServiceProvider
  {
    public object GetService(Type serviceType)
    {
      return null;
    }
  }
}
