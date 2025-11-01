// Navigation/Interfaces/INavigationService.cs

using System.Threading;
using Cysharp.Threading.Tasks;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Views;

namespace MToolKit.Runtime.Navigation.Interfaces
{
  public interface INavigationService
  {
    UniTask<T> PushAsync<T>(ECanvasType canvasType, T prefab, CancellationToken token = default) where T : View;
    UniTask PopAsync(ECanvasType canvasType, CancellationToken token = default);
    UniTask ClearStackAsync(ECanvasType canvasType, CancellationToken token = default);
    bool TryPeek(ECanvasType canvasType, out IView view);
    void Cleanup(ECanvasType canvas, View view);
  }
}