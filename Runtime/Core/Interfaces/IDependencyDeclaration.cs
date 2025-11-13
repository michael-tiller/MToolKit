using System;
using System.Collections.Generic;

namespace MToolKit.Runtime.Core.Interfaces
{
  /// <summary>
  ///   Interface for explicit dependency declaration to eliminate hidden dependencies.
  ///   Plugins implementing this interface must declare all their required and optional dependencies.
  /// </summary>
  public interface IDependencyDeclaration
  {
    /// <summary>
    ///   Gets the collection of service types that are required for this plugin to function.
    ///   These services must be available during runtime initialization.
    /// </summary>
    IEnumerable<Type> RequiredServices { get; }

    /// <summary>
    ///   Gets the collection of service types that are optional for this plugin.
    ///   These services may or may not be available during runtime initialization.
    /// </summary>
    IEnumerable<Type> OptionalServices { get; }
  }
}