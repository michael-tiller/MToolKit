using System;
using MToolKit.Runtime.Core.Interfaces;
using VContainer;

namespace MToolKit.Runtime.Core.Abstractions
{
  /// <summary>
  ///   Base class for plugins that implement both IRuntimePlugin and IRuntimeSystem.
  ///   Provides lifecycle guards to prevent multiple Start/Shutdown calls.
  ///   Includes dependency validation support for plugins implementing IDependencyDeclaration.
  /// </summary>
  public abstract class AbstractRuntimePlugin : AbstractGamePlugin, IRuntimeSystem, IRuntimePlugin
  {
    private bool isRuntimeInitialized;

    /// <summary>
    ///   Phase 2: Setup subscriptions and early initialization.
    ///   Override in derived classes to implement setup logic.
    /// </summary>
    /// <param name="resolver">The object resolver.</param>
    public virtual void PerformSetup(IObjectResolver resolver) { }

    /// <summary>
    ///   Phase 3: Full initialization when all dependencies are ready.
    ///   Override in derived classes to implement runtime initialization.
    /// </summary>
    /// <param name="resolver">The object resolver.</param>
    public virtual void PerformRuntimeInitialization(IObjectResolver resolver)
    {
      if (isRuntimeInitialized)
        return;

      isRuntimeInitialized = true;
    }

    /// <summary>
    ///   Check if dependencies are ready for runtime initialization.
    ///   The default implementation returns true. Override in derived classes for custom logic.
    /// </summary>
    /// <param name="resolver">The object resolver.</param>
    /// <returns>True if dependencies are ready for Phase 3.</returns>
    public virtual bool AreDependenciesReady(IObjectResolver resolver)
    {
      return true;
    }

    /// <summary>
    ///   Validates dependencies for plugins implementing IDependencyDeclaration.
    ///   This method provides a default implementation that can be overridden.
    /// </summary>
    /// <param name="resolver">The object resolver.</param>
    /// <returns>True if all dependencies are available.</returns>
    public virtual bool ValidateDependencies(IObjectResolver resolver)
    {
      if (this is IDependencyDeclaration dependencyDeclaration)
        foreach (Type serviceType in dependencyDeclaration.RequiredServices)
          if (!resolver.TryResolve(serviceType, out _))
            return false;
      return true;
    }

    public virtual void Tick(float deltaTime) { }
    public virtual void LateTick(float deltaTime) { }
    public virtual void FixedTick(float deltaTime) { }
  }
}