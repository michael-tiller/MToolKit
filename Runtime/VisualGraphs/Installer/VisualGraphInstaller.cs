using System.Collections.Generic;
using MToolKit.Runtime.VisualGraphs.Executors;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MToolKit.Runtime.VisualGraphs.Installer
{
    /// <summary>
    ///   VContainer installer for the visual graph subsystem.
    ///   Registers core services and all node executors.
    /// </summary>
    public sealed class VisualGraphInstaller : IInstaller
  {
    public void Install(IContainerBuilder builder)
    {
      // Core services
      builder.Register<GraphEventRouter>(Lifetime.Singleton);
      builder.Register<NodeExecutorRegistry>(Lifetime.Singleton);

      // Event emitter adapter (simple implementation)
      builder.Register<IEventEmitter, SimpleEventEmitter>(Lifetime.Singleton);

      // Node executors - register all IGraphNodeExecutor implementations
      // Add your custom executors here or use RegisterComponentInHierarchy for MonoBehaviour-based executors
      builder.Register<QuestSetStageNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<DialogueLineNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();
      builder.Register<DialogueChoiceNodeExecutor>(Lifetime.Singleton)
        .As<IGraphNodeExecutor>();

      // Save provider (adapt to your save system interface)
      // builder.Register<Persistence.GraphStateSaveProvider>(Lifetime.Singleton)
      //     .As<ICustomSaveProvider>();

      // Build callback to register all executors
      builder.RegisterBuildCallback(container =>
      {
        var registry = container.Resolve<NodeExecutorRegistry>();
        var executors = container.Resolve<IEnumerable<IGraphNodeExecutor>>();

        foreach (var executor in executors)
          registry.Register(executor);
      });
    }
  }

    /// <summary>
    ///   Simple event emitter implementation that does nothing.
    ///   Replace with your actual event bus integration.
    /// </summary>
    internal sealed class SimpleEventEmitter : IEventEmitter
  {
    public void Emit(IEventMessage message)
    {
      // TODO: Emit to your R3 event bus or message pipe
      // For now, just log
      Debug.Log($"[GraphEvent] {message.Type} in {message.Domain}");
    }
  }
}