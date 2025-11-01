// Navigation/Views/SettingsView.cs

using System;
using System.Threading;
using MToolKit.Runtime.Navigation.Enums;
using MToolKit.Runtime.Navigation.Interfaces;
using MToolKit.Runtime.Navigation.Views;
using MToolKit.Runtime.Settings.Audio;
using MToolKit.Runtime.Settings.Enums;
using MToolKit.Runtime.Settings.Graphics;
using MToolKit.Runtime.Settings.Interfaces;
using Cysharp.Threading.Tasks;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using ILogger = Serilog.ILogger;
using MToolKit.Runtime.Localization;
using MToolKit.Runtime.Settings.Game;
using MToolKit.Runtime.Settings.Input;
using UnityEngine.EventSystems;

namespace MToolKit.Template.Navigation
{
  [RequireComponent(typeof(AudioSettingsInitializer), typeof(SubviewManager))]
  public class SettingsView : View
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<SettingsView>().ForFeature("MToolKit.Template.Navigation"));
    private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;

    [SerializeField][Required] private AudioSettingsInitializer audioSettingsInitializer;
    [SerializeField][Required] private GraphicsSettingsInitializer graphicsSettingsInitializer;
    [SerializeField][Required] private GameSettingsInitializer gameSettingsInitializer;
    [SerializeField][Required] private InputSettingsInitializer inputSettingsInitializer;

    [SerializeField][Required] private Button saveSettingsButton, defaultSettingsButton, cancelSettingsButton;

    [SerializeField][Required] private SubviewManager subviewManager;

    [SerializeField][Required] private TextMeshProUGUI subviewText;

    private IDisposable isDirtySub;

    [Inject] private IModalService ModalService { get; set; }
    [Inject] private IObjectResolver Container { get; set; }
    private ISettingsSystem SettingsController { get; set; }

    private void Reset()
    {
      audioSettingsInitializer = GetComponent<AudioSettingsInitializer>();
      graphicsSettingsInitializer = GetComponent<GraphicsSettingsInitializer>();
      gameSettingsInitializer = GetComponent<GameSettingsInitializer>();
      inputSettingsInitializer = GetComponent<InputSettingsInitializer>();

      subviewManager = GetComponent<SubviewManager>();
    }

    private void Start()
    {
      StartAsync().Forget();
    }

    private async UniTask StartAsync()
    {
      // Wait for SettingsController to become available
      int maxAttempts = 10;
      int attempts = 0;

      while (attempts < maxAttempts)
      {
        try
        {
          SettingsController = Container.Resolve<ISettingsSystem>();
          log.ForGameObject(gameObject).ForMethod().Debug("Successfully resolved ISettingsController from container on attempt {0}", attempts + 1);
          break;
        }
        catch (Exception ex)
        {
          attempts++;
          log.ForGameObject(gameObject).ForMethod().Warning("Failed to resolve ISettingsController from container (attempt {0}/{1}): {2}", attempts, maxAttempts, ex.Message);

          if (attempts >= maxAttempts)
          {
            log.ForGameObject(gameObject).ForMethod().Error("Failed to resolve ISettingsController after {0} attempts", maxAttempts);
            return;
          }

          // Wait a frame before trying again
          await UniTask.Yield();
        }
      }

      // Check if SettingsController is available
      if (SettingsController == null)
      {
        log.ForGameObject(gameObject).ForMethod().Error("SettingsController is null after resolution!");
        return;
      }

      await audioSettingsInitializer.ConfigureAsync();
      await graphicsSettingsInitializer.ConfigureAsync();
      await gameSettingsInitializer.ConfigureAsync();
      await inputSettingsInitializer.ConfigureAsync();
      
      SettingsController.Apply(true, false);

      saveSettingsButton.onClick.AddListener(ClickApply);
      defaultSettingsButton.onClick.AddListener(ClickRevertToDefaultSettings);
      cancelSettingsButton.onClick.AddListener(ClickCancel);

      isDirtySub = SettingsController.IsDirty.Subscribe(IsSettingsDirtyHandler);
      IsSettingsDirtyHandler(SettingsController.IsDirty.Value);

      subviewManager.OnSubviewChanged -= OnSubviewChangedHandler;
      subviewManager.OnSubviewChanged += OnSubviewChangedHandler;
    }

    public override void Show()
    {
      base.Show();
      EventSystem.current.SetSelectedGameObject(cancelSettingsButton.gameObject);
    }

    private void OnDestroy()
    {
      isDirtySub?.Dispose();
      subviewManager.OnSubviewChanged -= OnSubviewChangedHandler;
    }

    private void OnSubviewChangedHandler(Subview subview)
    {
      log.ForGameObject(gameObject).ForMethod().Information("Set subview {0}", subview.DisplayName);
    }

    private async UniTask CreateCancelModalView(CancellationToken token)
    {
      log.ForGameObject(gameObject).ForMethod().Information("Creating cancel modal view");

      // Local confirm and cancel actions wrapping async calls.
      void OnConfirm()
      {
        ConfirmAsync().Forget();
      }

      void OnCancel()
      {
        CancelAsync().Forget();
      }

      async UniTask ConfirmAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(ConfirmAsync)).Information("Confirming cancel");
        await UniTask.Yield();
        SettingsController.Cancel();
        await NavigationService.PopAsync(ECanvasType.Overlay, token);
      }

      async UniTask CancelAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(CancelAsync)).Information("Cancelling cancel");
        await NavigationService.PopAsync(ECanvasType.Overlay, token);
      }

      log.ForGameObject(gameObject).ForMethod().Information("About to call ModalService.CreateModalView");

      await ModalService.CreateModalView<ModalView>(
        token,
        nameof(CreateCancelModalView),
        LocalizationHelper.GetLocalizedString("Unsaved changes"),
        LocalizationHelper.GetLocalizedString("Are you sure you want to return to menu? All unsaved changes will be lost."),
        EModalButtonType.Negative,
        LocalizationHelper.GetLocalizedString("Yes"),
        OnConfirm,
        EModalButtonType.Primary,
        LocalizationHelper.GetLocalizedString("No"),
        OnCancel
      );

      log.ForGameObject(gameObject).ForMethod().Information("ModalService.CreateModalView completed");
    }

    private async UniTask CreateConfirmDefaultsModalView(CancellationToken token)
    {
      void OnConfirm()
      {
        ConfirmAsync().Forget();
      }

      void OnCancel()
      {
        CancelAsync().Forget();
      }

      async UniTask ConfirmAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(ConfirmAsync)).Information("Confirming defaults");
        await UniTask.Yield();
        SettingsController.DefaultSettings();
        await NavigationService.PopAsync(ECanvasType.Overlay, token);
      }

      async UniTask CancelAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(CancelAsync)).Information("Cancelling defaults");
        await NavigationService.PopAsync(ECanvasType.Overlay, token);
      }

      await ModalService.CreateModalView<ModalView>(
        token,
        nameof(CreateConfirmDefaultsModalView),
        LocalizationHelper.GetLocalizedString("Revert to default?"),
        LocalizationHelper.GetLocalizedString("This will revert your Config."),
        EModalButtonType.Negative,
        LocalizationHelper.GetLocalizedString("No"),
        OnCancel,
        EModalButtonType.Primary,
        LocalizationHelper.GetLocalizedString("Yes"),
        OnConfirm
      );
    }

    private async UniTask CreateConfirmModalView(CancellationToken token)
    {
      void OnConfirm()
      {
        ConfirmAsync().Forget();
      }

      void OnCancel()
      {
        CancelAsync().Forget();
      }

      async UniTask ConfirmAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(ConfirmAsync)).Information("Confirming apply");
        await UniTask.Yield();
        SettingsController.FinishApply();
        await NavigationService.PopAsync(ECanvasType.Overlay, token);
      }

      async UniTask CancelAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(CancelAsync)).Information("Cancelling apply");
        await NavigationService.PopAsync(ECanvasType.Overlay, token);
      }

      await ModalService.CreateModalView<ModalView>(
        token,
        nameof(CreateConfirmModalView),
        LocalizationHelper.GetLocalizedString("Apply changes?"),
        LocalizationHelper.GetLocalizedString("This will save your Config."),
        EModalButtonType.Negative,
        LocalizationHelper.GetLocalizedString("No"),
        OnCancel,
        EModalButtonType.Primary,
        LocalizationHelper.GetLocalizedString("Yes"),
        OnConfirm
      );
    }


    private async UniTask CreateConfirmResolutionTimedModalView(CancellationToken token)
    {
      void OnConfirm()
      {
        ConfirmAsync().Forget();
      }

      void OnCancel()
      {
        CancelAsync().Forget();
      }

      async UniTask ConfirmAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(ConfirmAsync)).Information("Confirming resolution timed");
        await UniTask.Yield();
        SettingsController.FinishApply();
        await NavigationService.PopAsync(ECanvasType.Overlay, token);
      }

      async UniTask CancelAsync()
      {
        log.ForGameObject(gameObject).ForMethod(nameof(CancelAsync)).Information("Cancelling resolution timed");
        await NavigationService.PopAsync(ECanvasType.Overlay, token);
      }

      await ModalService.CreateTimedModalView(
        token,
        nameof(CreateConfirmResolutionTimedModalView),
        LocalizationHelper.GetLocalizedString("Confirm Resolution?"),
        LocalizationHelper.GetLocalizedString("Are you sure you want to use these Config? The Config will revert in 30 seconds."),
        EModalButtonType.Negative,
        LocalizationHelper.GetLocalizedString("No"),
        OnCancel,
        30f,
        OnCancel,
        EModalButtonType.Primary,
        LocalizationHelper.GetLocalizedString("Yes"),
        OnConfirm
      );
    }

    private void IsSettingsDirtyHandler(bool isDirty)
    {
      saveSettingsButton.interactable = isDirty;
      defaultSettingsButton.interactable = isDirty;
    }

    private bool ShouldShowResolutionConfirmation()
    {
      return SettingsController.GraphicsSettings.Fullscreen.IsDirty ||
             SettingsController.GraphicsSettings.ResolutionIndex.IsDirty;
    }

    private void ClickApply()
    {
      if (ShouldShowResolutionConfirmation())
        CreateConfirmResolutionTimedModalView(new CancellationToken()).Forget();
      else
        CreateConfirmModalView(new CancellationToken()).Forget();
    }

    private void ClickRevertToDefaultSettings()
    {
      CreateConfirmDefaultsModalView(new CancellationToken()).Forget();
    }

    private void ClickCancel()
    {
      log.ForGameObject(gameObject).ForMethod().Debug("ClickCancel called, IsDirty: {IsDirty}", SettingsController.IsDirty.Value);

      if (SettingsController.IsDirty.Value)
      {
        log.ForGameObject(gameObject).ForMethod().Information("Settings are dirty, creating cancel modal");
        CreateCancelModalView(new CancellationToken()).Forget();
      }
      else
      {
        log.ForGameObject(gameObject).ForMethod().Verbose("Settings are not dirty, calling SettingsController.Cancel directly");
        SettingsController.Cancel();
      }
    }
  }
}