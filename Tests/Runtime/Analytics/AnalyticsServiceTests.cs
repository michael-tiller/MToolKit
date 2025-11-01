using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Analytics;
using MToolKit.Runtime.Analytics.Interfaces;
using NSubstitute;
using NUnit.Framework;

namespace MToolKit.Tests.Runtime.Analytics
{

public sealed class AnalyticsServiceTests
{
    [Test]
    public void Initialize_CallsBackend()
    {
        // Arrange
        var backend = Substitute.For<IAnalyticsBackend>();
        backend.Started.Returns(false);
        var svc = new AnalyticsService(backend);
        var ct = CancellationToken.None;
        
        // Act
        svc.InitializeAsync(ct).Forget();
        
        // Assert
        backend.Received(1).InitializeAsync(ct);
    }

    [Test]
    public void StartSessionAsync_CallsBackend_WhenNotStarted()
    {
        // Arrange
        var backend = Substitute.For<IAnalyticsBackend>();
        backend.Started.Returns(false);
        var svc = new AnalyticsService(backend);
        var ct = CancellationToken.None;
        
        // Act
        svc.StartSessionAsync(ct).Forget();
        
        // Assert
        backend.Received(1).StartSessionAsync(ct);
    }

    [Test]
    public void EndSessionAsync_CallsBackend_WhenStarted()
    {
        // Arrange
        var backend = Substitute.For<IAnalyticsBackend>();
        backend.Started.Returns(true);
        var svc = new AnalyticsService(backend);
        var ct = CancellationToken.None;
        
        // Act
        svc.EndSessionAsync(ct).Forget();
        
        // Assert
        backend.Received(1).EndSessionAsync(ct);
    }

    [Test]
    public void TrackEvent_Forwards()
    {
        // Arrange
        var backend = Substitute.For<IAnalyticsBackend>();
        var svc = new AnalyticsService(backend);
        var p = new Dictionary<string, object> { ["k"] = 1 };
        
        // Act
        svc.TrackEvent("evt", p);
        
        // Assert
        backend.Received(1).TrackEvent("evt", p);
    }

    [Test]
    public void TrackEvent_ForwardsWithNullParameters()
    {
        // Arrange
        var backend = Substitute.For<IAnalyticsBackend>();
        var svc = new AnalyticsService(backend);
        
        // Act
        svc.TrackEvent("evt", null);
        
        // Assert
        backend.Received(1).TrackEvent("evt", null);
    }

    [Test]
    public void SetUserId_Forwards()
    {
        // Arrange
        var backend = Substitute.For<IAnalyticsBackend>();
        var svc = new AnalyticsService(backend);
        
        // Act
        svc.SetUserId("test-user");
        
        // Assert
        backend.Received(1).SetUserId("test-user");
    }

    [Test]
    public void SetConsent_Forwards()
    {
        // Arrange
        var backend = Substitute.For<IAnalyticsBackend>();
        var svc = new AnalyticsService(backend);
        
        // Act
        svc.SetConsent(true, false);
        
        // Assert
        backend.Received(1).SetConsent(true, false);
    }

    [Test]
    public void FlushAsync_Forwards()
    {
        // Arrange
        var backend = Substitute.For<IAnalyticsBackend>();
        var svc = new AnalyticsService(backend);
        var ct = CancellationToken.None;
        
        // Act
        svc.FlushAsync(ct).Forget();
        
        // Assert
        backend.Received(1).FlushAsync(ct);
    }
}

}
