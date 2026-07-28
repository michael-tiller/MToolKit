using System;
using System.Collections.Generic;
using System.Threading;
using MToolKit.Runtime.ErrorSystem.Messages;
using MToolKit.Runtime.MessageBus.Interfaces;

namespace MToolKit.Runtime.MessageBus
{
  /// <summary>
  ///   Ordered pre/post interceptor storage and dispatch for one broker scope. One instance per closed
  ///   <see cref="MessageBrokerCore{TScope}" />, which is what keeps game and global registrations from
  ///   firing on each other.
  /// </summary>
  internal sealed class InterceptorRegistry
  {
    /// <summary>Shared by both dictionaries and by the sequence counter — one lock, one ordering.</summary>
    private readonly object gate = new();

    private readonly Dictionary<Type, List<Registration>> preByType = new();
    private readonly Dictionary<Type, List<Registration>> postByType = new();

    /// <summary>
    ///   Monotonic for the process lifetime; deliberately NOT rewound by <see cref="Reset" />, since tests
    ///   read it and a rewind would make sequence values ambiguous across fixtures.
    /// </summary>
    private long nextSeq;

    internal sealed class Registration
    {
      internal int Band;
      internal long Seq;
      internal Delegate Op;
      internal string Owner;

      /// <summary>
      ///   0 = live. An int rather than a bool so disposal can use <see cref="Interlocked" /> and dispatch
      ///   can use <see cref="Volatile" />: disposal writes under the lock but dispatch reads OUTSIDE it, and
      ///   a lock taken by the writer establishes nothing for an unlocked reader.
      /// </summary>
      internal int Disposed;
    }

    internal IDisposable AddPre<T>(Action<T> op, HandlerPriority priority, string owner) =>
      Add(preByType, op, priority, owner);

    internal IDisposable AddPost<T>(Action<T> op, HandlerPriority priority, string owner) =>
      Add(postByType, op, priority, owner);

    private IDisposable Add<T>(Dictionary<Type, List<Registration>> store, Action<T> op, HandlerPriority priority, string owner)
    {
      // Validation order is op-first-then-T, so InterceptPre<SomeCancellableStruct>(null) reports the null.
      if (op == null) throw new ArgumentNullException(nameof(op));
      if (typeof(T).IsValueType && typeof(ICancellableMessage).IsAssignableFrom(typeof(T)))
        throw new ArgumentException(
          $"{typeof(T).Name} is a value type implementing {nameof(ICancellableMessage)}; cancellation cannot " +
          "survive boxing. Make it a class.", nameof(T));

      var registration = new Registration { Band = (int)priority, Op = op, Owner = owner };

      lock (gate)
      {
        // Seq is allocated inside the SAME lock as the insertion: a counter incremented outside would hand
        // out sequence numbers in an order that need not match insertion order.
        registration.Seq = nextSeq++;

        if (!store.TryGetValue(typeof(T), out List<Registration> list))
        {
          list = new List<Registration>();
          store[typeof(T)] = list;
        }

        // Keep (Band, Seq) ascending. Since Seq only grows, appending after the last entry of an equal-or-
        // lower band is the sorted position — registration order IS execution order within a band.
        int index = list.Count;
        while (index > 0 && list[index - 1].Band > registration.Band) index--;
        list.Insert(index, registration);
      }

      return new Subscription(this, store, typeof(T), registration);
    }

    /// <summary>Marks first (so an in-flight snapshot skips it immediately), then removes and prunes.</summary>
    private void Remove(Dictionary<Type, List<Registration>> store, Type key, Registration registration)
    {
      if (Interlocked.Exchange(ref registration.Disposed, 1) != 0) return;

      lock (gate)
      {
        if (!store.TryGetValue(key, out List<Registration> list)) return;
        list.Remove(registration);
        if (list.Count == 0) store.Remove(key);
      }
    }

    /// <summary>
    ///   Disposes every registration and clears both stores. Marking before clearing matters: a dispatch in
    ///   flight holds references to the registrations, so emptying the store alone would leave them live and
    ///   still running.
    /// </summary>
    internal void Reset()
    {
      lock (gate)
      {
        foreach (KeyValuePair<Type, List<Registration>> entry in preByType)
          foreach (Registration r in entry.Value)
            Interlocked.Exchange(ref r.Disposed, 1);
        foreach (KeyValuePair<Type, List<Registration>> entry in postByType)
          foreach (Registration r in entry.Value)
            Interlocked.Exchange(ref r.Disposed, 1);

        preByType.Clear();
        postByType.Clear();
      }
    }

    /// <summary>Runs pre-interceptors. Returns false if the message was cancelled — caller skips the publish.</summary>
    internal bool RunPre<T>(T message)
    {
      ICancellableMessage cancellable = AsCancellable(message);

      // BEFORE the registry-miss early-out: sticky cancellation must hold even with zero pre registrations.
      if (cancellable != null && cancellable.IsCancelled) return false;

      Registration[] snapshot = Snapshot<T>(preByType);
      if (snapshot == null) return true;

      foreach (Registration reg in snapshot)
      {
        if (Volatile.Read(ref reg.Disposed) != 0) continue;

        try
        {
          ((Action<T>)reg.Op)(message);
        }
        catch (Exception ex)
        {
          ReportFailure<T>(reg.Owner, ex);
        }

        // Checked AFTER the catch: an op may cancel and THEN throw, and the cancel still counts.
        if (cancellable != null && cancellable.IsCancelled) return false;
      }

      return true;
    }

    /// <summary>Runs post-interceptors. No cancellation checks, no early return — always runs to completion.</summary>
    internal void RunPost<T>(T message)
    {
      Registration[] snapshot = Snapshot<T>(postByType);
      if (snapshot == null) return;

      foreach (Registration reg in snapshot)
      {
        if (Volatile.Read(ref reg.Disposed) != 0) continue;

        try
        {
          ((Action<T>)reg.Op)(message);
        }
        catch (Exception ex)
        {
          ReportFailure<T>(reg.Owner, ex);
        }
      }
    }

    private Registration[] Snapshot<T>(Dictionary<Type, List<Registration>> store)
    {
      lock (gate)
      {
        return store.TryGetValue(typeof(T), out List<Registration> list) ? list.ToArray() : null;
      }
    }

    /// <summary>
    ///   Type suppression lives HERE, at the catch site, not inside the reporter — a check inside a
    ///   substitutable delegate is not a check. Compared as a Type, never a name string.
    /// </summary>
    private static void ReportFailure<T>(string owner, Exception ex)
    {
      if (typeof(T) == typeof(ErrorRequestMessage)) return;

      try
      {
        InterceptorFailureReporter.Report(owner, typeof(T), ex);
      }
      catch
      {
        // The whole Report call, not just its publish: the log call can throw, and a test override can throw.
      }
    }

    /// <summary>
    ///   Where the reference-type rule is actually enforced. The registration-time <c>typeof(T)</c> guard is
    ///   necessary but not sufficient — <c>InterceptPre&lt;object&gt;</c> passes it trivially, and
    ///   <c>Publish&lt;object&gt;(someStruct)</c> would then box and silently lose every Cancel().
    /// </summary>
    private static ICancellableMessage AsCancellable<T>(T message)
    {
      if (message is not ICancellableMessage cancellable) return null;
      if (message.GetType().IsValueType)
        throw new ArgumentException(
          $"{message.GetType().Name} is a value type implementing {nameof(ICancellableMessage)}; cancellation " +
          "cannot survive boxing. Make it a class.", nameof(message));

      return cancellable;
    }

    private sealed class Subscription : IDisposable
    {
      private readonly InterceptorRegistry registry;
      private readonly Dictionary<Type, List<Registration>> store;
      private readonly Type key;
      private readonly Registration registration;

      internal Subscription(InterceptorRegistry registry, Dictionary<Type, List<Registration>> store, Type key, Registration registration)
      {
        this.registry = registry;
        this.store = store;
        this.key = key;
        this.registration = registration;
      }

      /// <summary>Idempotent, and a harmless no-op for a registration already dropped by Reset.</summary>
      public void Dispose() => registry.Remove(store, key, registration);
    }
  }
}
