using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Support
{
  /// <summary>
  ///   Base fixture for tests that create Unity objects (ScriptableObject graphs, xNode nodes,
  ///   QuestDefinitions, GraphVariableSets). Track each created object; it is DestroyImmediate'd in
  ///   TearDown so state does not leak across the editor session. Tolerates Unity fake-null /
  ///   already-destroyed objects.
  /// </summary>
  public abstract class UnityObjectCleanup
  {
    private readonly List<Object> tracked = new();

    protected T Track<T>(T obj) where T : Object
    {
      if (obj != null)
        tracked.Add(obj);
      return obj;
    }

    [TearDown]
    public void CleanupTrackedObjects()
    {
      foreach (var obj in tracked)
        if (obj != null)
          Object.DestroyImmediate(obj);

      tracked.Clear();
    }
  }
}
