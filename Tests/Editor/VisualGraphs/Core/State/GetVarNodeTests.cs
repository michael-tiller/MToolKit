using System;
using System.Linq;
using MToolKit.Runtime.VisualGraphs.Contexts;
using MToolKit.Runtime.VisualGraphs.Executors.State;
using MToolKit.Runtime.VisualGraphs.Runtime.DTOs;
using MToolKit.Runtime.VisualGraphs.Runtime.Messages;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Core.State
{
  /// <summary>
  ///   Pins <see cref="GetVarNodeExecutor" />: bare-key reads go straight through <see cref="IGraphState" />
  ///   (the resolver is never called), scoped keys route through a real <see cref="ScopedKeyResolver" />,
  ///   and a missing/unresolvable key writes <c>Fallback.GetDefaultValue()</c> verbatim.
  /// </summary>
  [TestFixture]
  public sealed class GetVarNodeTests
  {
    private GraphContextRegistry registry;
    private ScopedKeyResolver resolver;

    [SetUp]
    public void SetUp()
    {
      registry = new GraphContextRegistry(new RecordingEmitter());
      resolver = new ScopedKeyResolver(registry);
    }

    private GraphRunnerHarness HarnessWithGetVar(string key, GraphVariableDeclaration fallback = null)
    {
      var h = new GraphRunnerHarness(GraphDefBuilder.New()
        .EntryNode("e")
        .Node("get", "GetVarNode", new NodeParametersDictionary
        {
          { "Key", key },
          { "ResultKey", "result" },
          { "Fallback", fallback ?? new GraphVariableDeclaration() }
        })
        .Connect("e", "get")
        .Build());
      h.Executors.Register(new GetVarNodeExecutor(resolver));
      return h;
    }

    [Test]
    public void Execute_BareKeyHit_ReadsDirectlyViaState_NoResolverInvolved()
    {
      var h = HarnessWithGetVar("local_key");
      h.State.Set("local_key", 7f);

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<object>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(7f));
    }

    [Test]
    public void Execute_WorldScopedKeyHit_ResolvesThroughRegistry()
    {
      registry.GetOrCreate(EGraphContextScope.World, null).Variables.Set("gold", 42);
      var h = HarnessWithGetVar("world.gold");

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<object>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(42));
    }

    [Test]
    public void Execute_MissingKey_WritesFallbackDefaultValue_IntCase()
    {
      LogAssert.ignoreFailingMessages = true;
      var fallback = new GraphVariableDeclaration { type = EGraphVariableType.Int, intValue = 99 };
      var h = HarnessWithGetVar("world.never_set", fallback);

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<object>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(99));
    }

    [Test]
    public void Execute_MissingKey_WritesFallbackDefaultValue_Vector3Case()
    {
      LogAssert.ignoreFailingMessages = true;
      var fallback = new GraphVariableDeclaration { type = EGraphVariableType.Vector3, vector3Value = new Vector3(1, 2, 3) };
      var h = HarnessWithGetVar("world.never_set", fallback);

      h.Run(new TestMessageA());

      Assert.That(h.State.TryGet<object>("result", out var result), Is.True);
      Assert.That(result, Is.EqualTo(new Vector3(1, 2, 3)));
    }

    [Test]
    public void Execute_ResultWrite_EmitsStateChanged()
    {
      var h = HarnessWithGetVar("local_key");
      h.State.Set("local_key", 7f);

      h.Run(new TestMessageA());

      var emitted = h.Emitter.Emitted.Select(e => e.message).OfType<GraphStateChangedMessage>().ToList();
      Assert.That(emitted, Has.Count.EqualTo(1));
      Assert.That(emitted[0].StateKey, Is.EqualTo("result"));
      Assert.That(emitted[0].NewValue, Is.EqualTo(7f));
    }

    [Test]
    public void Execute_MalformedQuestScopeSyntax_Throws()
    {
      var h = HarnessWithGetVar("quest:no-dot-after-id");
      Assert.Throws<ArgumentException>(() => h.Run(new TestMessageA()));
    }
  }
}
