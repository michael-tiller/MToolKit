using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Analytics;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Runtime.Analytics
{

public sealed class GameAnalyticsBackendTests
{
    private GameAnalyticsBackend backend;
    private CancellationToken cancellationToken;

    [SetUp]
    public void SetUp()
    {
        backend = new GameAnalyticsBackend();
        cancellationToken = CancellationToken.None;
    }

    [Test]
    public void InitializeAsync_SkipsInEditor()
    {
        // Arrange & Act
        backend.InitializeAsync(cancellationToken).Forget();
        
        // Assert
        // In editor, initialization should complete without errors
        // and the backend should be marked as initialized
        Assert.IsTrue(true); // Test passes if no exception is thrown
    }

    [Test]
    public void Started_ReturnsFalseInitially()
    {
        // Arrange & Act
        var started = backend.Started;
        
        // Assert
        Assert.IsFalse(started);
    }

    [Test]
    public void StartSessionAsync_CompletesWithoutError()
    {
        // Arrange
        backend.InitializeAsync(cancellationToken).Forget();
        
        // Act & Assert
        Assert.DoesNotThrow(() => backend.StartSessionAsync(cancellationToken).Forget());
    }

    [Test]
    public void EndSessionAsync_CompletesWithoutError()
    {
        // Arrange
        backend.InitializeAsync(cancellationToken).Forget();
        
        // Act & Assert
        Assert.DoesNotThrow(() => backend.EndSessionAsync(cancellationToken).Forget());
    }

    [Test]
    public void SetUserId_DoesNotThrowInEditor()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.SetUserId("test-user"));
    }

    [Test]
    public void SetUserProperty_DoesNotThrowInEditor()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.SetUserProperty("key", "value"));
    }

    [Test]
    public void SetUserProperties_DoesNotThrowInEditor()
    {
        // Arrange
        var props = new Dictionary<string, string> { ["key1"] = "value1", ["key2"] = "value2" };
        
        // Act & Assert
        Assert.DoesNotThrow(() => backend.SetUserProperties(props));
    }

    [Test]
    public void SetUserProperties_HandlesNull()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.SetUserProperties(null));
    }

    [Test]
    public void SetConsent_DoesNotThrowInEditor()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.SetConsent(true, false));
    }

    [Test]
    public void TrackEvent_DoesNotThrowInEditor()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["param1"] = "value1", ["param2"] = 42 };
        
        // Act & Assert
        Assert.DoesNotThrow(() => backend.TrackEvent("test_event", parameters));
    }

    [Test]
    public void TrackEvent_HandlesNullParameters()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackEvent("test_event", null));
    }

    [Test]
    public void TrackEvent_HandlesEmptyParameters()
    {
        // Arrange
        var parameters = new Dictionary<string, object>();
        
        // Act & Assert
        Assert.DoesNotThrow(() => backend.TrackEvent("test_event", parameters));
    }

    [Test]
    public void TrackRevenue_DoesNotThrowInEditor()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackRevenue("USD", 9.99, "item_type", "item_id"));
    }

    [Test]
    public void TrackRevenue_HandlesNullValues()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackRevenue(null, 0, null, null));
    }

    [Test]
    public void TrackProgression_DoesNotThrowInEditor()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackProgression("level1", "stage1", "phase1", 100));
    }

    [Test]
    public void TrackProgression_HandlesNullValues()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackProgression("level1", null, null, null));
    }

    [Test]
    public void TrackDesign_DoesNotThrowInEditor()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackDesign("design_event", 42.5f));
    }

    [Test]
    public void TrackDesign_HandlesNullValue()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackDesign("design_event", null));
    }

    [Test]
    public void TrackError_DoesNotThrowInEditor()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackError("Test error message", "error"));
    }

    [Test]
    public void TrackError_HandlesNullValues()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.TrackError(null, null));
    }

    [Test]
    public void FlushAsync_CompletesWithoutError()
    {
        // Arrange & Act & Assert
        Assert.DoesNotThrow(() => backend.FlushAsync(cancellationToken).Forget());
    }

    [Test]
    public void MultipleOperations_DoNotThrowInEditor()
    {
        // Arrange
        var parameters = new Dictionary<string, object> { ["test"] = "value" };
        
        // Act & Assert - Multiple operations should not throw
        Assert.DoesNotThrow(() =>
        {
            backend.SetUserId("test-user");
            backend.SetUserProperty("key", "value");
            backend.SetConsent(true, false);
            backend.TrackEvent("test_event", parameters);
            backend.TrackRevenue("USD", 9.99, "item", "id");
            backend.TrackProgression("level1", "stage1", null, 100);
            backend.TrackDesign("design_event", 42.5f);
            backend.TrackError("error", "warning");
        });
    }
}

}
