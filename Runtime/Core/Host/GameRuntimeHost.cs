using MToolKit.Runtime.Core.Interfaces;
using UnityEngine;
using VContainer;

namespace MToolKit.Runtime.Core.Host
{
  /// <summary>
  ///   Host for the game runtime.
  /// </summary>
  public class GameRuntimeHost : MonoBehaviour
  {
    [Inject]
    private IGameRuntime runtime;


    private void Start()
    {
      runtime.Start();
    }

    private void OnDestroy()
    {
      runtime.Shutdown();
      // Only destroy the GameObject if it still exists (prevents errors when OnDestroy is called multiple times in tests)
      try
      {
        // Check if GameObject is still valid by accessing it
        if (gameObject != null)
        {
          // Use DestroyImmediate in EditMode, Destroy in PlayMode
          if (Application.isPlaying)
            Destroy(gameObject);
          else
            DestroyImmediate(gameObject);
        }
      }
      catch (MissingReferenceException)
      {
        // GameObject was already destroyed, ignore
      }
    }

    private void Update()
    {
      runtime.Tick(Time.deltaTime);
    }

    private void LateUpdate()
    {
      runtime.LateTick(Time.deltaTime);
    }

    private void FixedUpdate()
    {
      runtime.FixedTick(Time.fixedDeltaTime);
    }
  }
}