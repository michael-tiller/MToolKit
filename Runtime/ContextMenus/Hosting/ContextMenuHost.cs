using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace MToolKit.ContextMenus.Hosting
{
  /// <summary>Optional scene/prefab host. Works standalone or with the normal MTK plugin installer.</summary>
  [AddComponentMenu("MToolKit/UI/Context Menu Host")]
  public sealed class ContextMenuHost : MonoBehaviour
  {
    [SerializeField] private MenuHostConfig config;
    private IContextMenuHostService service;
    private bool ownsService;
    public IContextMenuHostService Service
    {
      get
      {
        if (service == null)
        {
          if (config == null) throw new InvalidOperationException("ContextMenuHost requires a host config or an injected IContextMenuHostService.");
          service = new ContextMenuHostService(config); ownsService = true;
        }
        return service;
      }
    }
    public void Configure(MenuHostConfig value)
    {
      if (service != null && !ownsService) throw new InvalidOperationException("Configure the DI-owned menu host through its plugin config.");
      if (ownsService) service?.Dispose();
      service = null; ownsService = false; config = value;
    }
    [Inject] public void Construct(IContextMenuHostService value)
    {
      if (ownsService) service?.Dispose();
      service = value ?? throw new ArgumentNullException(nameof(value)); ownsService = false;
    }
    public async UniTask OpenAsync(MenuOpenRequest request, CancellationToken cancellationToken = default)
    {
      using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.GetCancellationTokenOnDestroy());
      await Service.OpenAsync(request, lifetime.Token);
    }
    public void Close() => service?.Close();
    private void OnDisable() => service?.Close();
    private void OnDestroy()
    {
      if (ownsService) service?.Dispose();
      service = null;
    }
  }
}
