using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using MToolKit.Runtime.UI.Scaling;

namespace MToolKit.Theme.Typeset
{
  [RequireComponent(typeof(TMP_Text))]
  public class TypesetPreset : ThemePreset<Typeset>
  {
    private UserUiScaleCanvas scaleCanvas;

    private void OnEnable()
    {
      UserUiScaleCanvas.Attached += OnCanvasAttached;
      BindScaleCanvas();
      if (Current != null) Apply(Current);
    }

    private void OnDisable()
    {
      UserUiScaleCanvas.Attached -= OnCanvasAttached;
      if (scaleCanvas != null) scaleCanvas.ScaleChanged -= OnScaleChanged;
      scaleCanvas = null;
    }

    private void OnCanvasAttached(UserUiScaleCanvas canvas)
    {
      if (transform.IsChildOf(canvas.transform))
      {
        BindScaleCanvas();
        if (Current != null) Apply(Current);
      }
    }

    private void OnTransformParentChanged()
    {
      if (!isActiveAndEnabled) return;
      BindScaleCanvas();
      if (Current != null) Apply(Current);
    }

    private void BindScaleCanvas()
    {
      var next = GetComponentInParent<UserUiScaleCanvas>(true);
      if (next == scaleCanvas) return;
      if (scaleCanvas != null) scaleCanvas.ScaleChanged -= OnScaleChanged;
      scaleCanvas = next;
      if (scaleCanvas != null) scaleCanvas.ScaleChanged += OnScaleChanged;
    }

    private void OnScaleChanged(float _) { if (Current != null) Apply(Current); }
    [field: SerializeField]
    [field: Required]
    public TMP_Text Text { get; private set; }


    [SerializeField]
    private bool useOverrideMat;

    [SerializeField]
    [ShowIf(nameof(useOverrideMat))]
    private Material overrideMaterial;


    protected override void AutoWire()
    {
      Text = GetComponent<TMP_Text>();
    }

    protected override Typeset Resolve(Theme theme, string id)
    {
      return theme.TypesetStyleRegistry.GetTypeset(id);
    }

    protected override void Apply(Typeset typeset)
    {
      if (Text == null) AutoWire();
      if (Text == null || typeset == null) return;

      if (isActiveAndEnabled) BindScaleCanvas();
      float userScale = scaleCanvas != null ? scaleCanvas.EffectiveUserScale : UiScalePolicy.Default;
      float minimum = AccessibleTypography.ResolveMinimum(
        typeset.AutoSizeFont ? typeset.FontSizeMin : typeset.FontSize,
        typeset.FontSize, userScale, typeset.AccessibleFontSizeMin);
      float maximum = Mathf.Max(typeset.FontSize, minimum);

      Text.font = typeset.Font; // assigning font resets to the font's default material...
      Text.enableAutoSizing = typeset.AutoSizeFont;
      if (typeset.AutoSizeFont)
      {
        Text.fontSizeMin = minimum;
        Text.fontSizeMax = maximum;
      }
      Text.fontSize = maximum;
      Text.fontStyle = typeset.FontStyle;
      Text.lineSpacing = typeset.LineSpacing;
      Text.characterSpacing = typeset.CharacterSpacing;
      Text.wordSpacing = typeset.WordSpacing;
      Text.paragraphSpacing = typeset.ParagraphSpacing;
      if (useOverrideMat) Text.fontSharedMaterial = overrideMaterial; // ...so the override wins
    }
  }
}
