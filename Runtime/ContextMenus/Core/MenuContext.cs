using System;
using System.Threading;

namespace MToolKit.ContextMenus
{
  /// <summary>Caller-owned target validity and invalidation, with no gameplay service dependency.</summary>
  public sealed class MenuContext
  {
    private readonly Func<bool> isValid;
    private bool invalidated;
    public MenuContext(object target = null, Func<bool> isValid = null, long generation = 0,
      CancellationToken cancellationToken = default)
    { Target = target; this.isValid = isValid; Generation = generation; CancellationToken = cancellationToken; }
    public object Target { get; }
    public long Generation { get; }
    public CancellationToken CancellationToken { get; }
    public bool IsValid
    {
      get
      {
        if (invalidated || CancellationToken.IsCancellationRequested) return false;
        // A destroyed Unity target still exists as a CLR object. Use Unity's native lifetime check.
        if (Target is UnityEngine.Object unityTarget && unityTarget == null) return false;
        return isValid == null || isValid();
      }
    }
    public event Action Invalidated;
    public void Invalidate()
    {
      if (invalidated) return;
      invalidated = true;
      Invalidated?.Invoke();
    }
  }
}
