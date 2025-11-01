using System;
using System.Collections.Generic;
using MToolKit.Runtime.Core.Interfaces;
using Serilog;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Core.Abstractions
{
    /// <summary>
    /// Base class for domain plugins that manage a single service with sensible defaults.
    /// Reduces boilerplate by providing convention-based implementations for common patterns.
    /// </summary>
    /// <typeparam name="TService">The concrete service implementation</typeparam>
    /// <typeparam name="TInterface">The service interface</typeparam>
    public abstract class DomainPlugin<TService, TInterface> : AbstractRuntimePlugin, IRuntimePlugin, IDependencyDeclaration
        where TService : class, TInterface
        where TInterface : class
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<DomainPlugin<TService, TInterface>>().ForFeature("Core.Abstractions"));
        protected static new ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

        protected TService service;
        protected bool isRuntimeInitialized;

        /// <summary>
        /// Required services for dependency validation. Override to specify custom dependencies.
        /// </summary>
        public virtual IEnumerable<Type> RequiredServices => new[]
        {
            typeof(TInterface)
        };

        /// <summary>
        /// Optional services for dependency validation. Override to specify custom dependencies.
        /// </summary>
        public virtual IEnumerable<Type> OptionalServices => new[]
        {
            typeof(ILogger)
        };

        /// <summary>
        /// Register the service and plugin with the container.
        /// Override CreateService to provide custom service creation logic.
        /// </summary>
        public override void Register(IContainerBuilder builder)
        {
            log.ForGameObject(gameObject).ForMethod(nameof(Register)).Debug("Registering {0}", GetType().Name);

            // Register the service as singleton
            builder.Register<TInterface>(resolver => CreateService(resolver), Lifetime.Singleton);
            
            // Register the plugin instance
            builder.RegisterInstance(this).AsSelf();

            log.ForGameObject(gameObject).ForMethod(nameof(Register)).Debug("{0} registration completed", GetType().Name);
        }

        /// <summary>
        /// Create the service instance. Override to provide custom creation logic.
        /// </summary>
        /// <param name="resolver">The object resolver for dependency injection</param>
        /// <returns>The created service instance</returns>
        protected abstract TService CreateService(IObjectResolver resolver);

        /// <summary>
        /// Default setup implementation. Override for custom setup logic.
        /// </summary>
        public override void PerformSetup(IObjectResolver resolver)
        {
            log.ForGameObject(gameObject).ForMethod(nameof(PerformSetup)).Verbose("{0} setup phase completed", GetType().Name);
        }

        /// <summary>
        /// Check if dependencies are ready. Default implementation checks if service can be resolved.
        /// Override for custom dependency validation.
        /// </summary>
        public override bool AreDependenciesReady(IObjectResolver resolver)
        {
            bool canResolveService = resolver.TryResolve(out TInterface _);
            
            log.ForGameObject(gameObject).ForMethod(nameof(AreDependenciesReady)).Debug(
                "Dependencies ready check: CanResolveService={0}", canResolveService);

            return canResolveService;
        }

        /// <summary>
        /// Default runtime initialization. Resolves service and calls Initialize() if available.
        /// Override for custom initialization logic.
        /// </summary>
        public override void PerformRuntimeInitialization(IObjectResolver resolver)
        {
            if (isRuntimeInitialized)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Verbose(
                    "{0} already runtime initialized, skipping", GetType().Name);
                return;
            }

            log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Debug(
                "Performing {0} runtime initialization...", GetType().Name);

            try
            {
                // Resolve the service
                service = resolver.Resolve<TInterface>() as TService;
                
                if (service == null)
                {
                    log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Error(
                        "Failed to resolve {0}", typeof(TService).Name);
                    return;
                }

                // Service is resolved and ready
                log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Debug(
                    "{0} resolved successfully", typeof(TService).Name);

                isRuntimeInitialized = true;
                log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Debug(
                    "{0} runtime initialization completed", GetType().Name);
            }
            catch (Exception ex)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(PerformRuntimeInitialization)).Error(
                    ex, "Error during {0} runtime initialization", GetType().Name);
            }
        }

        /// <summary>
        /// Default shutdown implementation. Calls Dispose() if service implements IDisposable.
        /// Override for custom cleanup logic.
        /// </summary>
        public override void Shutdown()
        {
            base.Shutdown();

            if (service is IDisposable disposable)
            {
                disposable.Dispose();
                log.ForGameObject(gameObject).ForMethod(nameof(Shutdown)).Debug(
                    "{0} disposed", typeof(TService).Name);
            }

            service = null;
            isRuntimeInitialized = false;
        }
    }
}
