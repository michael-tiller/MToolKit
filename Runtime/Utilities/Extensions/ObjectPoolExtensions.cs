using System.Collections.Generic;
using UnityEngine.Pool;

namespace MToolKit.Runtime.Utilities.Extensions
{
  public static class ObjectPoolExtensions
  {
    /// <summary>
    /// Gets all objects in the pool, both active and inactive.
    /// Note: This is an expensive operation as it requires creating new objects if the pool is empty.
    /// </summary>
    public static IEnumerable<T> GetAll<T>(this ObjectPool<T> pool) where T : class
    {
      // Get all inactive objects
      var inactiveCount = pool.CountInactive;
      var activeCount = pool.CountActive;
      var totalCount = inactiveCount + activeCount;

      // Create a list to store all objects
      var allObjects = new List<T>(totalCount);

      // Get all inactive objects (they're already in the pool's internal list)
      for (int i = 0; i < inactiveCount; i++)
      {
        var obj = pool.Get();
        allObjects.Add(obj);
      }

      // Release all objects back to the pool
      foreach (var obj in allObjects)
      {
        pool.Release(obj);
      }

      return allObjects;
    }
  }
}