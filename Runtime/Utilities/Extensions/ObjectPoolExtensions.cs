using System.Collections.Generic;
using UnityEngine.Pool;

namespace MToolKit.Runtime.Utilities.Extensions
{
  public static class ObjectPoolExtensions
  {
    /// <summary>
    ///   Gets all objects in the pool, both active and inactive.
    ///   Note: This is an expensive operation as it requires creating new objects if the pool is empty.
    /// </summary>
    public static IEnumerable<T> GetAll<T>(this ObjectPool<T> pool) where T : class
    {
      // Get all inactive objects
      int inactiveCount = pool.CountInactive;
      int activeCount = pool.CountActive;
      int totalCount = inactiveCount + activeCount;

      // Create a list to store all objects
      List<T> allObjects = new(totalCount);

      // Get all inactive objects (they're already in the pool's internal list)
      for (int i = 0; i < inactiveCount; i++)
      {
        T obj = pool.Get();
        allObjects.Add(obj);
      }

      // Release all objects back to the pool
      foreach (T obj in allObjects)
        pool.Release(obj);

      return allObjects;
    }
  }
}