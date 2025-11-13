#if UNITY_EDITOR

using System;

namespace MToolKit.Runtime.Editor
{
  /// <summary>
  ///   Model representing a plugin dependency for diagnostic purposes.
  /// </summary>
  [Serializable]
  public class DependencyModel
  {
    public string ServiceName { get; set; }
    public Type ServiceType { get; set; }
    public bool IsResolved { get; set; }
    public object ServiceInstance { get; set; }
    public string ResolutionError { get; set; }
    public bool IsOptional { get; set; }
  }
}

#endif