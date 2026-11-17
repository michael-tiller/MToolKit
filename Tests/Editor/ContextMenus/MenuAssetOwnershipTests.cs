using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.AssetLoader;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.ResourceManagement;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace MToolKit.Tests.Editor.ContextMenus
{
  /// <summary>Real ResourceManager operations verify the loader's acquired-handle ownership boundary.</summary>
  public sealed class MenuAssetOwnershipTests
  {
    [Test]
    public void Failed_asset_operation_releases_acquired_handle_without_registration()
    {
      var previousHandler = ResourceManager.ExceptionHandler;
      using var resources = new ResourceManager();
      ResourceManager.ExceptionHandler = (_, __) => { };
      var handle = resources.CreateCompletedOperation<GameObject>(null, "Expected menu load failure");
      var expected = handle.OperationException;
      int before = ReferenceCount(handle);
      bool registered = false;
      try
      {
        var error = Assert.Catch<Exception>(() => AwaitOwned(handle, default, _ => registered = true).GetAwaiter().GetResult());
        Assert.That(error, Is.SameAs(expected));
        Assert.That(registered, Is.False);
        int afterLoader = ReferenceCount(handle);
        TestContext.WriteLine("Failed asset operation references before loader: " + before + "; after loader: " + afterLoader);
        Assert.That(afterLoader, Is.EqualTo(before - 1), "The loader must release exactly its acquired reference.");
        // Failed Complete queues the operation's Start reference for deferred callbacks; successful
        // Complete releases that reference immediately. Flush only this fixture's owned manager.
        FlushOwnedCallbacks(resources);
        TestContext.WriteLine("Failed asset operation valid after owned callbacks: " + handle.IsValid());
        Assert.That(handle.IsValid(), Is.False, "After deferred completion, no loader or manager reference may remain.");
      }
      finally
      {
        try
        {
          FlushOwnedCallbacks(resources);
          if (handle.IsValid()) resources.Release(handle);
        }
        finally { ResourceManager.ExceptionHandler = previousHandler; }
      }
    }

    [Test]
    public void Pre_canceled_asset_operation_releases_acquired_handle_without_registration()
    {
      using var resources = new ResourceManager();
      var handle = resources.CreateCompletedOperation<GameObject>(null, null);
      using var cancellation = new CancellationTokenSource();
      cancellation.Cancel();
      bool registered = false;
      try
      {
        Assert.Throws<OperationCanceledException>(() => AwaitOwned(handle, cancellation.Token, _ => registered = true).GetAwaiter().GetResult());
        Assert.That(registered, Is.False);
        Assert.That(handle.IsValid(), Is.False);
      }
      finally { if (handle.IsValid()) resources.Release(handle); }
    }

    [Test]
    public void Registration_failure_releases_the_handle_and_preserves_original_exception()
    {
      using var resources = new ResourceManager();
      var handle = resources.CreateCompletedOperation<GameObject>(null, null);
      var expected = new InvalidOperationException("Expected menu registration failure");
      try
      {
        var error = Assert.Throws<InvalidOperationException>(() => AwaitOwned(handle, default, _ => throw expected).GetAwaiter().GetResult());
        Assert.That(error, Is.SameAs(expected));
        Assert.That(handle.IsValid(), Is.False);
      }
      finally { if (handle.IsValid()) resources.Release(handle); }
    }

    [Test]
    public void Same_asset_acquisitions_release_exactly_once_per_owner()
    {
      using var resources = new ResourceManager();
      var prefab = new GameObject("Counted menu prefab");
      var handle = resources.CreateCompletedOperation(prefab, null);
      var second = resources.Acquire(handle);
      var loader = new AddressablesAssetLoader();
      try
      {
        RegisterSingle(loader, handle).GetAwaiter().GetResult();
        RegisterSingle(loader, second).GetAwaiter().GetResult();
        loader.Release(prefab);
        Assert.That(ReferenceCount(handle), Is.EqualTo(1), "Closing one host must retain the other host's acquired reference.");
        loader.Release(prefab);
        Assert.That(handle.IsValid(), Is.False, "The second host's release must retire its own acquired reference too.");
      }
      finally { loader.Dispose(); ReleaseRemaining(resources, handle); UnityEngine.Object.DestroyImmediate(prefab); }
    }

    [Test]
    public void Dispose_releases_all_acquired_references_even_when_the_asset_is_shared()
    {
      using var resources = new ResourceManager();
      var prefab = new GameObject("Shared menu prefab at shutdown");
      var handle = resources.CreateCompletedOperation(prefab, null);
      var second = resources.Acquire(handle);
      var loader = new AddressablesAssetLoader();
      try
      {
        RegisterSingle(loader, handle).GetAwaiter().GetResult();
        RegisterSingle(loader, second).GetAwaiter().GetResult();
        loader.Dispose(); loader.Dispose();
        Assert.That(handle.IsValid(), Is.False, "Dispose releases every acquisition, rather than one unique operation handle.");
      }
      finally { loader.Dispose(); ReleaseRemaining(resources, handle); UnityEngine.Object.DestroyImmediate(prefab); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Group_and_single_acquisitions_of_the_same_asset_release_in_either_order(bool releaseOtherAssetFirst)
    {
      using var resources = new ResourceManager();
      var first = new GameObject("Shared group menu prefab");
      var other = new GameObject("Other group menu prefab");
      var group = resources.CreateCompletedOperation<IList<GameObject>>(new List<GameObject> { first, other }, null);
      var single = resources.CreateCompletedOperation(first, null);
      var loader = new AddressablesAssetLoader();
      try
      {
        RegisterGroup(loader, group).GetAwaiter().GetResult();
        RegisterSingle(loader, single).GetAwaiter().GetResult();
        loader.Release(first);
        loader.Release(releaseOtherAssetFirst ? other : first);
        Assert.That(group.IsValid(), Is.True, "A grouped acquisition retains its handle until its last asset share is returned.");
        loader.Release(releaseOtherAssetFirst ? first : other);
        Assert.That(single.IsValid(), Is.False);
        Assert.That(group.IsValid(), Is.False, "Overlapping single loads must not erase the group's ownership record.");
      }
      finally
      {
        loader.Dispose(); ReleaseRemaining(resources, single); ReleaseRemaining(resources, group);
        UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(other);
      }
    }

    [Test]
    public void Empty_group_releases_its_handle_without_waiting_for_a_nonexistent_asset_release()
    {
      using var resources = new ResourceManager();
      var group = resources.CreateCompletedOperation<IList<GameObject>>(Array.Empty<GameObject>(), null);
      var loader = new AddressablesAssetLoader();
      try
      {
        var result = RegisterGroup(loader, group).GetAwaiter().GetResult();
        Assert.That(result, Is.Empty);
        Assert.That(group.IsValid(), Is.False, "An empty successful group has no asset through which a caller could return its reference.");
      }
      finally { loader.Dispose(); ReleaseRemaining(resources, group); }
    }

    [Test]
    public void Repeated_group_acquisitions_each_retain_and_return_their_own_asset_shares()
    {
      using var resources = new ResourceManager();
      var first = new GameObject("Repeated group first prefab");
      var second = new GameObject("Repeated group second prefab");
      var group = resources.CreateCompletedOperation<IList<GameObject>>(new List<GameObject> { first, second }, null);
      var acquired = resources.Acquire(group);
      var loader = new AddressablesAssetLoader();
      try
      {
        RegisterGroup(loader, group).GetAwaiter().GetResult();
        RegisterGroup(loader, acquired).GetAwaiter().GetResult();
        loader.Release(first); loader.Release(second);
        Assert.That(ReferenceCount(group), Is.EqualTo(1));
        loader.Release(first); loader.Release(second);
        Assert.That(group.IsValid(), Is.False);
      }
      finally
      {
        loader.Dispose(); ReleaseRemaining(resources, group);
        UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
      }
    }

    private static UniTask<GameObject> RegisterSingle(AddressablesAssetLoader loader, AsyncOperationHandle<GameObject> handle)
      => AwaitOwned(handle, default, operation =>
        typeof(AddressablesAssetLoader).GetMethod("RegisterLoadedAsset", BindingFlags.Instance | BindingFlags.NonPublic)
          .MakeGenericMethod(typeof(GameObject)).Invoke(loader, new object[] { operation }));

    private static UniTask<IList<GameObject>> RegisterGroup(AddressablesAssetLoader loader, AsyncOperationHandle<IList<GameObject>> handle)
    {
      Action<AsyncOperationHandle<IList<GameObject>>> register = operation =>
        typeof(AddressablesAssetLoader).GetMethod("RegisterLoadedAssets", BindingFlags.Instance | BindingFlags.NonPublic)
          .MakeGenericMethod(typeof(GameObject)).Invoke(loader, new object[] { operation });
      var method = typeof(AddressablesAssetLoader).GetMethod("AwaitAndRegisterAsync", BindingFlags.Static | BindingFlags.NonPublic);
      return (UniTask<IList<GameObject>>)method.MakeGenericMethod(typeof(IList<GameObject>))
        .Invoke(null, new object[] { handle, CancellationToken.None, register });
    }

    private static void ReleaseRemaining<T>(ResourceManager resources, AsyncOperationHandle<T> handle)
    {
      // Fixtures acquire at most two references to any completed operation; no external owner exists.
      for (int i = 0; i < 2 && handle.IsValid(); i++) resources.Release(handle);
    }

    private static UniTask<GameObject> AwaitOwned(AsyncOperationHandle<GameObject> handle, CancellationToken cancellationToken,
      Action<AsyncOperationHandle<GameObject>> register)
    {
      var method = typeof(AddressablesAssetLoader).GetMethod("AwaitAndRegisterAsync", BindingFlags.Static | BindingFlags.NonPublic);
      Assert.That(method, Is.Not.Null, "The loader must retain acquired-handle ownership through awaiting and successful registry transfer.");
      return (UniTask<GameObject>)method.MakeGenericMethod(typeof(GameObject)).Invoke(null, new object[] { handle, cancellationToken, register });
    }

    private static int ReferenceCount<T>(AsyncOperationHandle<T> handle)
    {
      if (!handle.IsValid()) return 0;
      var operationField = typeof(AsyncOperationHandle<T>).GetField("m_InternalOp", BindingFlags.Instance | BindingFlags.NonPublic);
      var countProperty = typeof(AsyncOperationBase<T>).GetProperty("ReferenceCount", BindingFlags.Instance | BindingFlags.NonPublic);
      Assert.That(operationField, Is.Not.Null);
      Assert.That(countProperty, Is.Not.Null);
      return (int)countProperty.GetValue(operationField.GetValue(handle));
    }

    private static void FlushOwnedCallbacks(ResourceManager resources)
    {
      var update = typeof(ResourceManager).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
      Assert.That(update, Is.Not.Null);
      update.Invoke(resources, new object[] { 0f });
    }
  }
}
