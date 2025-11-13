using UnityEngine;

namespace MToolKit.Runtime.Utilities.Extensions
{
  /// <summary>
  ///   Provides extension methods for converting between Unity vector types.
  /// </summary>
  public static class VectorExtensions
  {
    /// <summary>
    ///   Converts a Vector2Int to a Vector3 with z = 0.
    /// </summary>
    public static Vector3 ToVector3(this Vector2Int v)
    {
      return new Vector3(v.x, v.y, 0f);
    }

    /// <summary>
    ///   Converts a Vector2Int to a Vector3Int with z = 0.
    /// </summary>
    public static Vector3Int ToVector3Int(this Vector2Int v)
    {
      return new Vector3Int(v.x, v.y, 0);
    }

    /// <summary>
    ///   Converts a Vector2 to a Vector3 with z = 0.
    /// </summary>
    public static Vector3 ToVector3(this Vector2 v)
    {
      return new Vector3(v.x, v.y, 0f);
    }

    /// <summary>
    ///   Converts a Vector2 to a Vector3Int with z = 0 (floored).
    /// </summary>
    public static Vector3Int ToVector3Int(this Vector2 v)
    {
      return new Vector3Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), 0);
    }

    /// <summary>
    ///   Converts a Vector3 to a Vector2 by dropping the z component.
    /// </summary>
    public static Vector2 ToVector2(this Vector3 v)
    {
      return new Vector2(v.x, v.y);
    }

    /// <summary>
    ///   Converts a Vector3 to a Vector2Int by flooring x and y.
    /// </summary>
    public static Vector2Int ToVector2Int(this Vector3 v)
    {
      return new Vector2Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
    }

    /// <summary>
    ///   Converts a Vector3 to a Vector3Int by flooring each component.
    /// </summary>
    public static Vector3Int ToVector3Int(this Vector3 v)
    {
      return new Vector3Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
    }

    /// <summary>
    ///   Converts a Vector3Int to a Vector3.
    /// </summary>
    public static Vector3 ToVector3(this Vector3Int v)
    {
      return new Vector3(v.x, v.y, v.z);
    }

    /// <summary>
    ///   Converts a Vector3Int to a Vector2Int by dropping the z component.
    /// </summary>
    public static Vector2Int ToVector2Int(this Vector3Int v)
    {
      return new Vector2Int(v.x, v.y);
    }

    /// <summary>
    ///   Converts a Vector3Int to a Vector2.
    /// </summary>
    public static Vector2 ToVector2(this Vector3Int v)
    {
      return new Vector2(v.x, v.y);
    }

    public static Vector3 WithZ(this Vector2 v, float z = 0f)
    {
      return new Vector3(v.x, v.y, z);
    }

    public static Vector3 WithZ(this Vector2Int v, float z = 0f)
    {
      return new Vector3(v.x, v.y, z);
    }

    public static Vector3Int WithZ(this Vector2Int v, int z = 0)
    {
      return new Vector3Int(v.x, v.y, z);
    }
  }
}