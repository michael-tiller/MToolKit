using System;
using UnityEngine;

namespace MToolKit.Runtime.Persistence.ES3Integration
{
    /// <summary>
    ///   Player data structure for storing player position and orientation
    ///   Simple data container without metadata (version info is in ProfileMetaData)
    /// </summary>
    [Serializable]
  [ES3Serializable]
  public class ExamplePlayerData
  {
    [field: SerializeField]
    public Vector3 Position { get; private set; }

    [field: SerializeField]
    public Quaternion Rotation { get; private set; }

    [field: SerializeField]
    public int SceneNumber { get; private set; }

        /// <summary>
        ///   Creates a new ExamplePlayerData instance from a Transform
        /// </summary>
        public static ExamplePlayerData FromTransform(Transform transform, int sceneNumber = 0)
    {
      if (transform == null)
        throw new ArgumentNullException(nameof(transform));

      return new ExamplePlayerData(transform.position, transform.rotation, sceneNumber);
    }

    public ExamplePlayerData()
    {
      Position = Vector3.zero;
      Rotation = Quaternion.identity;
      SceneNumber = 0;
    }

    // Parameterized constructor for easier initialization
    public ExamplePlayerData(Vector3 position, Quaternion rotation, int sceneNumber = 0)
    {
      Position = position;
      Rotation = rotation;
      SceneNumber = sceneNumber;
    }

        /// <summary>
        ///   Updates the data with new position and rotation
        /// </summary>
        public ExamplePlayerData UpdateFromTransform(Transform transform, int sceneNumber = 0)
    {
      if (transform == null)
        throw new ArgumentNullException(nameof(transform));

      return new ExamplePlayerData(transform.position, transform.rotation, sceneNumber);
    }

        /// <summary>
        ///   Applies the stored position and rotation to a Transform
        /// </summary>
        public void ApplyToTransform(Transform transform)
    {
      if (transform == null)
        throw new ArgumentNullException(nameof(transform));

      transform.position = Position;
      transform.rotation = Rotation;
    }
  }
}