using System.Collections.Generic;
using MToolKit.Runtime.MessageBus.Interfaces;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Recording <see cref="IEventEmitter" />: captures every emitted (message, domain) pair so tests
  ///   can assert what GraphRunner emitted (e.g. the dialogue-close DialogueProgressMessage).
  /// </summary>
  public sealed class RecordingEmitter : IEventEmitter
  {
    public List<(IGameMessage message, string domain)> Emitted { get; } = new();
    public int EmitCallCount => Emitted.Count;

    public void Emit(IGameMessage message, string domain = null)
    {
      Emitted.Add((message, domain));
    }
  }
}
