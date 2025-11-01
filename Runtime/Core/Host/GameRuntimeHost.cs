using MToolKit.Runtime.Core.Interfaces;
using UnityEngine;
using VContainer;

namespace MToolKit.Runtime.Core.Host
{
    /// <summary>
    /// Host for the game runtime.
    /// </summary>
    public class GameRuntimeHost : MonoBehaviour
    {
        [Inject] private IGameRuntime runtime;


        private void Start()
        {
            runtime.Start();
        }

        private void OnDestroy()
        {
            runtime.Shutdown();
            Destroy(this.gameObject);
        }

        private void Update() => runtime.Tick(Time.deltaTime);
        private void LateUpdate() => runtime.LateTick(Time.deltaTime);
        private void FixedUpdate() => runtime.FixedTick(Time.fixedDeltaTime);
    }
}
