using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Core;
using MToolKit.Runtime.Core.Interfaces;
using NUnit.Framework;
using VContainer;

namespace MToolKit.Tests.Editor.Core
{
  [TestFixture]
  public sealed class PluginRegistryRuntimeInitializationTests
  {
    [Test]
    public void AwaitedRuntimeInitialization_PropagatesTheFailingPlugin()
    {
      var registry = new PluginRegistry();
      registry.Register(new ThrowingRuntimePlugin());

      var exception = Assert.Throws<PluginRuntimeInitializationException>(() =>
        registry.PerformPluginRuntimeInitializationAsync(null).GetAwaiter().GetResult());

      Assert.That(exception.PluginName, Is.EqualTo(nameof(ThrowingRuntimePlugin)));
      Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
    }

    private sealed class ThrowingRuntimePlugin : IGamePlugin, IRuntimePlugin
    {
      public void Register(IContainerBuilder builder) { }
      public void PerformSetup(IObjectResolver resolver) { }
      public void PerformRuntimeInitialization(IObjectResolver resolver) => throw new InvalidOperationException("fixture");
      public bool AreDependenciesReady(IObjectResolver resolver) => true;
      public UniTask PerformRuntimeInitializationAsync(IObjectResolver resolver, CancellationToken ct = default)
      {
        PerformRuntimeInitialization(resolver);
        return UniTask.CompletedTask;
      }
    }
  }
}
