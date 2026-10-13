namespace MToolKit.Runtime.VisualGraphs.Rules
{
  /// <summary>Position of a GameRule in a message's graph-dispatch sequence.</summary>
  public enum ERuleTiming
  {
    /// <summary>Execute before all normal domain graphs.</summary>
    BeforeDomainGraphs = 0,
    /// <summary>Execute after all normal domain graphs.</summary>
    AfterDomainGraphs = 1
  }
}
