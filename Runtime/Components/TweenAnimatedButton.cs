using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MToolKit.Runtime.Components
{
  public class TweenAnimatedButton : Button
  {
    [Header("Button Settings")]
    [SerializeField]
    private Color normalButtonColor = Color.white;

    [SerializeField]
    private Color highlightedButtonColor = Color.gray;

    [SerializeField]
    private Color pressedButtonColor = Color.black;

    [SerializeField]
    private Color selectedButtonColor = Color.black;

    [SerializeField]
    private Color disabledButtonColor = Color.grey;

    [SerializeField]
    private float tweenDuration = 0.2f;

    [Header("Border Settings")]
    [SerializeField]
    private Graphic border; // Image

    [SerializeField]
    private Color normalBorderColor = Color.white;

    [SerializeField]
    private Color highlightedBorderColor = Color.gray;

    [SerializeField]
    private Color pressedBorderColor = Color.black;

    [SerializeField]
    private Color selectedBorderColor = Color.black;

    [SerializeField]
    private Color disabledBorderColor = Color.grey;

    [Header("Label Settings")]
    [SerializeField]
    private Graphic label; // TMP_Text or Text

    [SerializeField]
    private Color normalLabelColor = Color.black;

    [SerializeField]
    private Color highlightedLabelColor = Color.black;

    [SerializeField]
    private Color pressedLabelColor = Color.white;

    [SerializeField]
    private Color selectedLabelColor = Color.white;

    [SerializeField]
    private Color disabledLabelColor = Color.gray;

    private Tweener borderColorTween;

    private Tweener buttonColorTween;
    private Tweener labelColorTween;

    protected override void Awake()
    {
      base.Awake();
      if (label == null)
        label = GetComponentInChildren<TMP_Text>() as Graphic ?? GetComponentInChildren<Text>();
    }

    protected override void OnDestroy()
    {
      buttonColorTween?.Kill();
      borderColorTween?.Kill();
      labelColorTween?.Kill();
      base.OnDestroy();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
      Color targetButtonColor;
      Color targetBorderColor;
      Color targetLabelColor;

      switch (state)
      {
        case SelectionState.Normal:
          targetButtonColor = normalButtonColor;
          targetBorderColor = normalBorderColor;
          targetLabelColor = normalLabelColor;
          break;
        case SelectionState.Highlighted:
          targetButtonColor = highlightedButtonColor;
          targetBorderColor = highlightedBorderColor;
          targetLabelColor = highlightedLabelColor;
          break;
        case SelectionState.Selected:
          targetButtonColor = selectedButtonColor;
          targetBorderColor = selectedBorderColor;
          targetLabelColor = selectedLabelColor;
          break;
        case SelectionState.Pressed:
          targetButtonColor = pressedButtonColor;
          targetBorderColor = pressedBorderColor;
          targetLabelColor = pressedLabelColor;
          break;
        case SelectionState.Disabled:
          targetButtonColor = disabledButtonColor;
          targetBorderColor = disabledBorderColor;
          targetLabelColor = disabledLabelColor;
          break;
        default:
          targetButtonColor = normalButtonColor;
          targetBorderColor = normalBorderColor;
          targetLabelColor = normalLabelColor;
          break;
      }

      if (targetGraphic is not null)
      {
        buttonColorTween?.Kill();
        if (instant)
          targetGraphic.color = targetButtonColor;
        else
          buttonColorTween = DOVirtual.Color(
            targetGraphic.color,
            targetButtonColor,
            tweenDuration,
            c =>
            {
              if (targetGraphic is not null)
                targetGraphic.color = c;
            }
            ).SetUpdate(true);
      }

      if (border is not null)
      {
        borderColorTween?.Kill();
        if (instant)
          border.color = targetBorderColor;
        else
          borderColorTween = DOVirtual.Color(
            border.color,
            targetBorderColor,
            tweenDuration,
            c =>
            {
              if (border is not null)
                border.color = c;
            }
            ).SetUpdate(true);
      }

      if (label is not null)
      {
        labelColorTween?.Kill();

        if (label is TMP_Text tmpText)
        {
          if (instant)
            tmpText.color = targetLabelColor;
          else
            labelColorTween = DOVirtual.Color(
              tmpText.color,
              targetLabelColor,
              tweenDuration,
              c => { tmpText.color = c; }
              ).SetUpdate(true);
        }
        else if (label is { } graphic)
        {
          if (instant)
            graphic.color = targetLabelColor;
          else
            labelColorTween = DOVirtual.Color(
              graphic.color,
              targetLabelColor,
              tweenDuration,
              c =>
              {
                if (graphic != null)
                  graphic.color = c;
              }
              ).SetUpdate(true);
        }
      }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
      base.OnValidate();
      if (label == null)
        label = GetComponentInChildren<TMP_Text>() as Graphic ?? GetComponentInChildren<Text>();

      DoStateTransition(interactable ? SelectionState.Normal : SelectionState.Disabled, true);
    }

    public void ForceValidate()
    {
      OnValidate();
    }
#endif
  }
}