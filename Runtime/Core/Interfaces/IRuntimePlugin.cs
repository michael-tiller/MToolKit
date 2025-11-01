using System.Threading;
using Cysharp.Threading.Tasks;
using VContainer;

namespace MToolKit.Runtime.Core.Interfaces
{
    /// <summary>
    /// Interface for a runtime plugin with explicit three-phase initialization lifecycle.
    /// This interface reduces mental load by clearly separating setup, configuration, and runtime phases.
    /// </summary>
    public interface IRuntimePlugin
    {
        /// <summary>
        /// Phase 1: Register dependencies. Called during container building.
        /// Safe to call resolver.Resolve() for already-registered services.
        /// This phase is for registering services and dependencies with the DI container.
        /// </summary>
        /// <param name="builder">The container builder for registering dependencies.</param>
        void Register(IContainerBuilder builder);

        /// <summary>
        /// Phase 2: Setup subscriptions and early initialization.
        /// Dependencies may not be ready yet - use TryResolve().
        /// This phase is for setting up event subscriptions, configuring UI bindings,
        /// and performing any initialization that doesn't require all dependencies.
        /// </summary>
        /// <param name="resolver">The object resolver.</param>
        void PerformSetup(IObjectResolver resolver);

        /// <summary>
        /// Phase 3: Full initialization when all dependencies are ready.
        /// All services guaranteed to be available.
        /// This phase is for starting services, initializing runtime state,
        /// and performing any operations that require all dependencies to be ready.
        /// </summary>
        /// <param name="resolver">The object resolver.</param>
        void PerformRuntimeInitialization(IObjectResolver resolver);

        /// <summary>
        /// Check if dependencies are ready for runtime initialization.
        /// This method determines whether Phase 3 can proceed.
        /// If the plugin implements IDependencyDeclaration, this method will validate
        /// that all required services are available.
        /// </summary>
        /// <param name="resolver">The object resolver.</param>
        /// <returns>True if dependencies are ready for Phase 3.</returns>
        bool AreDependenciesReady(IObjectResolver resolver);

        /// <summary>
        /// Validates that all declared dependencies are available.
        /// This method is automatically called for plugins implementing IDependencyDeclaration.
        /// </summary>
        /// <param name="resolver">The object resolver.</param>
        /// <returns>True if all dependencies are available.</returns>
        bool ValidateDependencies(IObjectResolver resolver)
        {
            if (this is IDependencyDeclaration dependencyDeclaration)
            {
                foreach (var serviceType in dependencyDeclaration.RequiredServices)
                {
                    if (!resolver.TryResolve(serviceType, out _))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Async initialization with proper ordering support.
        /// Executes all three phases in sequence with dependency checking.
        /// </summary>
        /// <param name="resolver">The object resolver.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>UniTask for async completion.</returns>
        UniTask InitializeAsync(IObjectResolver resolver, CancellationToken ct = default)
        {
            PerformSetup(resolver);
            if (AreDependenciesReady(resolver))
            {
                PerformRuntimeInitialization(resolver);
            }
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Legacy single-phase initialization for backward compatibility.
        /// Default implementation delegates to two-phase methods.
        /// </summary>
        /// <param name="resolver">The object resolver.</param>
        void Initialize(IObjectResolver resolver)
        {
            PerformSetup(resolver);
            if (AreDependenciesReady(resolver))
            {
                PerformRuntimeInitialization(resolver);
            }
        }
    }
}
