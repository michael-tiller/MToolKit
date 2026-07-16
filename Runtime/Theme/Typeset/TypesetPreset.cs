using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace MToolKit.Theme.Typeset
{
  [RequireComponent(typeof(TMP_Text))]
  public class TypesetPreset : ThemePreset<Typeset>
  {
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
      if (Text == null || typeset == null) return;

      Text.font = typeset.Font; // assigning font resets to the font's default material...
      Text.enableAutoSizing = typeset.AutoSizeFont;
      if (typeset.AutoSizeFont)
      {
        Text.fontSizeMin = typeset.FontSizeMin;
        Text.fontSizeMax = typeset.FontSize;
      }
      Text.fontSize = typeset.FontSize;
      Text.fontStyle = typeset.FontStyle;
      Text.lineSpacing = typeset.LineSpacing;
      Text.characterSpacing = typeset.CharacterSpacing;
      Text.wordSpacing = typeset.WordSpacing;
      Text.paragraphSpacing = typeset.ParagraphSpacing;
      if (useOverrideMat) Text.fontSharedMaterial = overrideMaterial; // ...so the override wins
    }
  }
}