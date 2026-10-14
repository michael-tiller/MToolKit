using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Dialogue.Messages;
using MToolKit.Runtime.VisualGraphs.Runtime;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace MToolKit.Tests.Editor.VisualGraphs.Runtime
{
  /// <summary>
  ///   Exercises real message routing and node execution through a stable Serilog sink. Run this
  ///   fixture in a fresh domain: both runtime classes cache their first contextual logger.
  /// </summary>
  [TestFixture]
  public sealed class GraphExecutionLoggingTests
  {
    private sealed class CaptureSink : ILogEventSink
    {
      private readonly ILogger previous;

      public CaptureSink(ILogger previous)
      {
        this.previous = previous;
      }

      public bool Capturing { get; set; } = true;
      public List<LogEvent> Events { get; } = new();

      public void Emit(LogEvent logEvent)
      {
        if (Capturing)
          Events.Add(logEvent);
        else
          previous.Write(logEvent);
      }
    }

    private readonly LoggingLevelSwitch level = new(LogEventLevel.Information);
    private ILogger previous;
    private CaptureSink sink;

    [OneTimeSetUp]
    public void ConfigureStableCapture()
    {
      previous = Log.Logger;
      sink = new CaptureSink(previous);
      Log.Logger = new LoggerConfiguration()
        .MinimumLevel.ControlledBy(level)
        .WriteTo.Sink(sink)
        .CreateLogger();

      // Initialize both cached loggers once and distinguish a stale logger from a severity failure.
      var router = new GraphEventRouter();
      router.RouteAsync(null).GetAwaiter().GetResult();
      var runner = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("entry").Node("missing", "Unregistered").Connect("entry", "missing").Build());
      runner.Run(new TestMessageA());

      Assert.That(GraphEvents().Any(e => e.Level == LogEventLevel.Warning &&
                                        e.MessageTemplate.Text == "Attempted to route null message"), Is.True,
        "The router must capture this fixture's stable sink; use a fresh test domain.");
      Assert.That(GraphEvents().Any(e => e.Level == LogEventLevel.Error &&
                                        e.MessageTemplate.Text.Contains("no executor found")), Is.True,
        "The runner must capture this fixture's stable sink; use a fresh test domain.");
    }

    [SetUp]
    public void ResetCapture()
    {
      level.MinimumLevel = LogEventLevel.Information;
      sink.Events.Clear();
    }

    [OneTimeTearDown]
    public void RestoreLogging()
    {
      Log.Logger = previous;
      // Cached contextual loggers still reference the sink. Keep them usable after this fixture
      // and let the original logger apply its own minimum level to forwarded events.
      level.MinimumLevel = LogEventLevel.Verbose;
      sink.Capturing = false;
      sink.Events.Clear();
    }

    [Test]
    public void Information_RoutedQuest_ExecutesWithoutRoutineTrace()
    {
      var runner = QuestRunner();
      var action = runner.RegisterExecutor("Action");

      Route(runner);

      Assert.That(action.ExecutedNodeIds, Is.EqualTo(new[] { "action" }));
      Assert.That(GraphEvents(), Is.Empty,
        "Normal graph routing and node execution must stay quiet at Information.");
    }

    [Test]
    public void Debug_RoutedQuest_EmitsRoutineTracesAtDebug()
    {
      level.MinimumLevel = LogEventLevel.Debug;
      var runner = QuestRunner();
      var action = runner.RegisterExecutor("Action");

      Route(runner);

      Assert.That(action.ExecutedNodeIds, Is.EqualTo(new[] { "action" }));
      var events = GraphEvents().ToList();
      Assert.That(events.Select(e => e.MessageTemplate.Text), Is.EquivalentTo(new[]
      {
        "Routing message '{MessageType}' (domain: '{Domain}') to {GraphCount} graph(s) via {Strategy}: {GraphIds}",
        "Quest: Graph '{GraphId}' received message '{MessageType}' (domain: {Domain})",
        "Quest: Found {EntryNodeCount} entry nodes in graph '{GraphId}'",
        "Quest: Processing entry node {NodeId} (type: {NodeType})",
        "Quest: Enqueued node {ToNodeId} from entry node {FromNodeId}",
        "Quest: Entry node {NodeId} enqueued {ConnectionCount} connected node(s)",
        "Quest: Executing node {NodeId} (type: {NodeType}) in graph '{GraphId}'",
        "Quest: Completed execution of node {NodeType} ({NodeId})"
      }));
      Assert.That(events.Select(e => e.Level), Is.All.EqualTo(LogEventLevel.Debug),
        "The routing and node traces remain available when Debug is enabled.");
      Assert.That(events.Single(e => e.MessageTemplate.Text.StartsWith("Routing message"))
        .Properties["GraphIds"].ToString(), Does.Contain("logging-quest"));
    }

    [Test]
    public void Information_NullRoute_PreservesWarning()
    {
      var router = new GraphEventRouter();

      router.RouteAsync(null).GetAwaiter().GetResult();

      var warning = GraphEvents().Single();
      Assert.That(warning.Level, Is.EqualTo(LogEventLevel.Warning));
      Assert.That(warning.MessageTemplate.Text, Is.EqualTo("Attempted to route null message"));
    }

    [Test]
    public void Information_MaxSteps_PreservesWarning()
    {
      var runner = new GraphRunnerHarness(GraphDefBuilder.New().MaxSteps(1)
        .EntryNode("entry").Node("action", "Action").Connect("entry", "action").Build());
      var action = runner.RegisterExecutor("Action");

      runner.Run(new TestMessageA());

      Assert.That(action.ExecuteCallCount, Is.Zero);
      var warning = GraphEvents().Single(e => e.Level == LogEventLevel.Warning);
      Assert.That(warning.MessageTemplate.Text, Does.Contain("execution halted - max steps"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Information_ExecutorFailure_PreservesError(bool throws)
    {
      var runner = QuestRunner();
      if (throws)
        runner.RegisterExecutor("Action").ShouldThrow = true;

      runner.Run(new TestMessageA());

      var error = GraphEvents().Single(e => e.Level == LogEventLevel.Error);
      Assert.That(error.MessageTemplate.Text,
        Does.Contain(throws ? "error executing node" : "no executor found"));
      Assert.That(error.Exception, Is.Not.Null);
    }

    [Test]
    public void Information_DialogueContinue_PreservesLineAndPauseMessages()
    {
      var parameters = new NodeParametersDictionary();
      parameters["Text"] = "A deliberate dialogue line";
      var runner = new GraphRunnerHarness(GraphDefBuilder.New().Id("logging-dialogue").Domain("Dialogue")
        .Node("line", "DialogueLineNode", parameters).Build());
      runner.State.Set("Dialogue.NextNodeIds", new List<string> { "line" });
      var line = runner.RegisterExecutor("DialogueLineNode");
      line.OnExecute = (_, _) => runner.State.Set("Dialogue.NextNodeIds", new List<string> { "later" });

      runner.Run(new DialogueContinueMessage("logging-dialogue"));

      Assert.That(line.ExecutedNodeIds, Is.EqualTo(new[] { "line" }));
      var events = GraphEvents().Where(e => e.Level == LogEventLevel.Information).ToList();
      Assert.That(events.Select(e => e.MessageTemplate.Text), Is.EquivalentTo(new[]
      {
        "Dialogue: Graph '{GraphId}' received DialogueContinueMessage with GraphId={MessageGraphId}",
        "Dialogue: Continuing execution for graph '{GraphId}' with {Count} next node ID(s): {NodeIds}",
        "Quest: Executing DialogueLineNode {NodeId} in graph '{GraphId}' with text '{Text}'",
        "Dialogue: Pausing execution after {NodeType} ({NodeId}). Waiting for user input to continue."
      }));
      Assert.That(events.Single(e => e.Properties.ContainsKey("Text")).Properties["Text"].ToString(),
        Does.Contain("A deliberate dialogue line"));
      Assert.That(runner.State.TryGet<List<string>>("Dialogue.NextNodeIds", out var next), Is.True);
      Assert.That(next, Is.EqualTo(new[] { "later" }));
    }

    [Test]
    public void Information_DialogueEnd_PreservesCloseMessage()
    {
      var runner = new GraphRunnerHarness(GraphDefBuilder.New().Id("logging-dialogue").Domain("Dialogue").Build());

      runner.Run(new DialogueContinueMessage("logging-dialogue"));

      Assert.That(runner.Emitter.Emitted.Single().message, Is.TypeOf<DialogueProgressMessage>());
      Assert.That(GraphEvents().Any(e => e.Level == LogEventLevel.Information &&
                                        e.MessageTemplate.Text.Contains("Dialogue has ended - closing dialogue view")),
        Is.True);
    }

    private IEnumerable<LogEvent> GraphEvents()
    {
      return sink.Events.Where(e => e.Properties.TryGetValue("SourceContext", out var context) &&
                                   context is ScalarValue scalar &&
                                   (Equals(scalar.Value, typeof(GraphEventRouter).FullName) ||
                                    Equals(scalar.Value, typeof(GraphRunner).FullName)));
    }

    private static GraphRunnerHarness QuestRunner()
    {
      return new GraphRunnerHarness(GraphDefBuilder.New().Id("logging-quest")
        .Subscribe(typeof(TestMessageA), "Quest")
        .EntryNode("entry").Node("action", "Action").Connect("entry", "action").Build());
    }

    private static void Route(GraphRunnerHarness runner)
    {
      var router = new GraphEventRouter { TimeProvider = () => 0f };
      router.RegisterRunner(runner.Runner);
      router.RouteAsync(new TestMessageA(), "Quest").GetAwaiter().GetResult();
    }
  }
}
