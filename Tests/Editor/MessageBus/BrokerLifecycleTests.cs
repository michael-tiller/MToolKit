using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MToolKit.Runtime.MessageBus;
using MToolKit.Tests.Editor.MessageBus.Support;
using NUnit.Framework;

namespace MToolKit.Tests.Editor.MessageBus
{
  [TestFixture]
  public sealed class BrokerLifecycleTests
  {
    private sealed class Reporter : IBrokerLifecycleReporter
    {
      public BrokerAccessMode AccessMode { get; set; }
      public List<BrokerLifecycleAccess> Accesses { get; } = new();

      public void Report(BrokerLifecycleAccess access) => Accesses.Add(access);
    }

    [SetUp]
    public void SetUp()
    {
      GameMessageBroker.ResetLifecycleForTests();
      GlobalAsyncMessageBroker.ResetLifecycleForTests();
    }

    [TearDown]
    public void TearDown()
    {
      GameMessageBroker.ResetLifecycleForTests();
      GlobalAsyncMessageBroker.ResetLifecycleForTests();
    }

    [Test]
    public void PreInitAccess_IsReported_AndRetainsGracefulReturn()
    {
      var reporter = new Reporter { AccessMode = BrokerAccessMode.Production };
      GameMessageBroker.SetLifecycleReporter(reporter);

      Assert.That(GameMessageBroker.GetSubscriber<BrokerPing>(), Is.Null);
      Assert.DoesNotThrow(() => GameMessageBroker.Publish(new BrokerPing(1)));

      Assert.AreEqual(2, reporter.Accesses.Count);
      Assert.That(reporter.Accesses[0].Phase, Is.EqualTo(BrokerLifecyclePhase.PreInit));
      Assert.That(reporter.Accesses[0].Operation, Is.EqualTo("GetSubscriber"));
      Assert.That(reporter.Accesses[1].Phase, Is.EqualTo(BrokerLifecyclePhase.PreInit));
      Assert.That(reporter.Accesses[1].Operation, Is.EqualTo("Publish"));
    }

    [Test]
    public async Task PreInitRequest_IsReported_AndReturnsDefault()
    {
      var reporter = new Reporter { AccessMode = BrokerAccessMode.ReducedTest };
      GameMessageBroker.SetLifecycleReporter(reporter);

      BrokerResponse response = await GameMessageBroker.RequestAsync<BrokerRequest, BrokerResponse>(new BrokerRequest("preinit"));

      Assert.That(response.Id, Is.Null);
      Assert.AreEqual(1, reporter.Accesses.Count);
      Assert.That(reporter.Accesses[0].Phase, Is.EqualTo(BrokerLifecyclePhase.PreInit));
      Assert.That(reporter.Accesses[0].Operation, Is.EqualTo("RequestAsync"));
    }

    [Test]
    public void PostShutdownAccess_IsReported_AndRetainsGracefulReturn()
    {
      var reporter = new Reporter { AccessMode = BrokerAccessMode.Production };
      GameMessageBroker.SetLifecycleReporter(reporter);
      GameMessageBroker.Reset();

      Assert.That(GameMessageBroker.GetPublisher<BrokerPing>(), Is.Null);
      Assert.DoesNotThrow(() => GameMessageBroker.Publish(new BrokerPing(1)));

      Assert.AreEqual(2, reporter.Accesses.Count);
      Assert.That(reporter.Accesses[0].Phase, Is.EqualTo(BrokerLifecyclePhase.Shutdown));
      Assert.That(reporter.Accesses[1].Phase, Is.EqualTo(BrokerLifecyclePhase.Shutdown));
    }

    [Test]
    public void GlobalAndGameReporters_AreIndependent()
    {
      var gameReporter = new Reporter();
      var globalReporter = new Reporter();
      GameMessageBroker.SetLifecycleReporter(gameReporter);
      GlobalAsyncMessageBroker.SetLifecycleReporter(globalReporter);

      GameMessageBroker.Publish(new BrokerPing(1));
      GlobalAsyncMessageBroker.Publish(new BrokerPing(1));

      Assert.AreEqual(1, gameReporter.Accesses.Count);
      Assert.AreEqual(1, globalReporter.Accesses.Count);
      Assert.That(gameReporter.Accesses[0].Scope, Is.EqualTo("GameMessageBroker"));
      Assert.That(globalReporter.Accesses[0].Scope, Is.EqualTo("GlobalAsyncMessageBroker"));
    }
  }
}
