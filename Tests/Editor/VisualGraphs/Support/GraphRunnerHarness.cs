using System.Threading;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.Execution;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.State;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Bundles a real <see cref="GraphRunner" /> with an in-memory state, a fresh executor registry, a
  ///   recording emitter and a null service provider. Because every fake completes synchronously
  ///   (UniTask.CompletedTask), <see cref="Run" /> drives the runner to completion via
  ///   GetAwaiter().GetResult() — letting characterization tests assert REAL execution, not just that a
  ///   task was returned.
  /// </summary>
  public sealed class GraphRunnerHarness
  {
    public GraphRunnerHarness(IRuntimeGraphDefinition definition, MToolKit.Runtime.VisualGraphs.Variables.GraphVariableSet declarations = null)
    {
      Definition = definition;
      Runner = new GraphRunner(definition, State, Executors, new NullServiceProvider(), Emitter, declarations);
    }

    public IRuntimeGraphDefinition Definition { get; }
    public InMemoryGraphState State { get; } = new();
    public NodeExecutorRegistry Executors { get; } = new();
    public RecordingEmitter Emitter { get; } = new();
    public GraphRunner Runner { get; }

    /// <summary>Register and return a recording executor for the given node type.</summary>
    public RecordingExecutor RegisterExecutor(string nodeType)
    {
      var executor = new RecordingExecutor(nodeType);
      Executors.Register(executor);
      return executor;
    }

    /// <summary>Dispatch a message and block until the (synchronous) runner completes.</summary>
    public void Run(IGameMessage message, string domain = null, CancellationToken ct = default)
    {
      Runner.HandleMessageAsync(message, domain, ct).GetAwaiter().GetResult();
    }
  }
}
