using System;
using MToolKit.Runtime.Core.Interfaces;
using Serilog;
using UnityEngine;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Core.Abstractions
{
    public abstract class AbstractGamePlugin : MonoBehaviour, IGamePlugin
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<AbstractGamePlugin>().ForFeature("Core.Abstractions"));
        protected static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

        
        protected bool isStarted = false;
        protected bool isShutdown = false;

        public bool IsStarted => isStarted;
        public bool IsShutdown => isShutdown;

        public virtual void Register(IContainerBuilder builder) { }

        /// <summary>
        /// Virtual Start method with lifecycle guard. Override in derived classes if needed.
        /// </summary>
        public virtual void Start()
        {
            if (isStarted)
            {
                log.ForGameObject(gameObject).ForMethod(nameof(Start)).Verbose("{0} already started, skipping.", GetType().Name);
                return;
            }

            // Check if the object is still valid before accessing gameObject
            if (this == null || gameObject == null)
            {
                throw new MissingReferenceException($"GameObject for {GetType().Name} has been destroyed");
            }

            log.ForGameObject(gameObject).ForMethod(nameof(Start)).Verbose("{0} started", GetType().Name);
            isStarted = true;
        }

        /// <summary>
        /// Virtual Shutdown method with lifecycle guard. Override in derived classes if needed.
        /// </summary>
        public virtual void Shutdown()
        {
            // Always set isStarted to false, even if already shutdown
            isStarted = false;

            if (isShutdown)
            {
                log.ForMethod(nameof(Shutdown)).Verbose("{0} already shut down, skipping.", GetType().Name);
                return;
            }

            // Check if the object is still valid before accessing gameObject
            if (this == null || gameObject == null)
            {
                log.ForMethod(nameof(Shutdown)).Warning("{0} is null or destroyed during shutdown, skipping.", GetType().Name);
                return;
            }

            log.ForGameObject(gameObject).ForMethod(nameof(Shutdown)).Verbose("Shutting down {0}", GetType().Name);
            isShutdown = true;
            log.ForGameObject(gameObject).ForMethod(nameof(Shutdown)).Debug("{0} shutdown completed", GetType().Name);
        }
    }
}