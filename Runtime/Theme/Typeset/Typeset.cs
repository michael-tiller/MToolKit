using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Theme.Typeset
{
  [CreateAssetMenu(menuName = "MToolKit/Theme/Typeset/Typeset", fileName = "NewTypeset")]
  [InlineEditor]
  public class Typeset : ScriptableObject
  {
    public string Id => name;

    [field: SerializeField]
    public TMP_FontAsset Font { get; private set; }

    [field: SerializeField]
    public bool AutoSizeFont { get; private set; }

    [field: SerializeField]
    [ShowIf(nameof(AutoSizeFont))]
    public float FontSizeMin { get; private set; }

    [field: SerializeField]
    public float FontSize { get; private set; }

    [field: SerializeField]
    public FontStyles FontStyle { get; private set; }

    [field: SerializeField]
    public float LineSpacing { get; private set; }

    [field: SerializeField]
    public float CharacterSpacing { get; private set; }

    [field: SerializeField]
    public float WordSpacing { get; private set; }

    [field: SerializeField]
    public float ParagraphSpacing { get; private set; }
  }
}