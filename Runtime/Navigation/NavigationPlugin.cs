using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core.Abstractions;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MToolKit.Runtime.Navigation
{
    public class NavigationPlugin : AbstractGamePlugin, IAsyncStartable
    {
        [SerializeField] private NavigationInstaller installer;

        public override void Register(IContainerBuilder builder)
        {
            // Register INavigationService in scene container where canvas transforms are available
            installer.Install(builder);

            // Register the plugin instance
            builder.RegisterInstance(this).AsSelf().AsImplementedInterfaces();
        }

        /// <summary>
        /// Async startup method for VContainer's UniTask integration.
        /// Replaces PerformRuntimeInitialization with proper async/await support.
        /// </summary>
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            // Runtime initialization - navigation service is created on first resolve
            // No additional async logic needed for this plugin, but we can add it here if needed
            await PerformAsyncInitialization(cancellation);
        }

        /// <summary>
        /// Performs any additional async initialization specific to NavigationPlugin.
        /// Override this method to add custom async initialization logic.
        /// </summary>
        protected virtual async UniTask PerformAsyncInitialization(CancellationToken cancellation)
        {
            // Default implementation - no additional async work needed
            // Navigation service is created on first resolve, no async initialization required
            await UniTask.CompletedTask;
        }
    }
}
