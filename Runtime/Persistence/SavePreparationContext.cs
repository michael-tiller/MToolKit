using System;
using System.Collections.Generic;

namespace MToolKit.Runtime.Persistence
{
  /// <summary>Owns participant admission leases for one coordinator preparation.</summary>
  public sealed class SavePreparationContext
  {
    private readonly object gate;
    private List<IDisposable> leases = new();

    internal SavePreparationContext(object gate) => this.gate = gate;

    /// <summary>Transfers a lease to the preparation; a lease arriving after cleanup is disposed immediately.</summary>
    public void HoldAdmission(IDisposable lease)
    {
      if (lease == null) throw new ArgumentNullException(nameof(lease));
      lock (gate)
      {
        if (leases != null)
        {
          leases.Add(lease);
          return;
        }
      }
      lease.Dispose();
    }

    internal void Close()
    {
      List<IDisposable> entered;
      lock (gate)
      {
        entered = leases;
        leases = null;
      }
      if (entered == null) return;
      List<Exception> failures = null;
      for (int i = entered.Count - 1; i >= 0; i--)
      {
        try { entered[i].Dispose(); }
        catch (Exception failure) { (failures ??= new()).Add(failure); }
      }
      if (failures != null) throw new AggregateException("Save preparation admission cleanup failed.", failures);
    }
  }
}
