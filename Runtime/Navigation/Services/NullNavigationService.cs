using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.Views;

namespace MToolKit.Runtime.Navigation.Services
{
  public class NullNavigationService : INavigationService
  {
    public void NavigateTo(ECanvasType canvas, object data = null) { }

    public void NavigateBack() { }

    public void NavigateClear() { }

    public void NavigateQuit() { }

    public void Cleanup(ECanvasType canvas, IView view) { }

    public UniTask<T> PushAsync<T>(ECanvasType canvasType, T prefab, CancellationToken token = default) where T : View
    {
      return UniTask.FromResult(default(T));
    }

    public UniTask PopAsync(ECanvasType canvasType, CancellationToken token = default)
    {
      return UniTask.CompletedTask;
    }

    public UniTask ClearStackAsync(ECanvasType canvasType, CancellationToken token = default)
    {
      return UniTask.CompletedTask;
    }

    public bool TryPeek(ECanvasType canvasType, out IView view)
    {
      view = null;
      return false;
    }

    public void Cleanup(ECanvasType canvas, View view) { }
  }
}