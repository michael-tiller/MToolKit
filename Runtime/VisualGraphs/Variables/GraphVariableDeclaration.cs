using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Variables
{
  /// <summary>
  ///   The closed set of variable types supported by graph variable declarations.
  ///   Adding a member requires the full support checklist in VISUAL_GRAPHS_ROADMAP.md
  ///   (typed default, ES3 round-trip, text literal syntax, comparison semantics).
  /// </summary>
  public enum EGraphVariableType
  {
    String = 0,
    Int = 1,
    Float = 2,
    Bool = 3,
    Vector3 = 4,
    Vector2 = 5,
    Color = 6
  }

  /// <summary>
  ///   Canonical declaration of a single graph variable: key, type, and typed default.
  ///   Declarations are plain serializable data — they describe state keys, they do not store runtime values.
  /// </summary>
  [Serializable]
  public sealed class GraphVariableDeclaration
  {
    [HorizontalGroup("Entry", Width = 200)]
    [LabelWidth(30)]
    public string key = "variableName";

    [HorizontalGroup("Entry", Width = 100)]
    [LabelWidth(30)]
    public EGraphVariableType type = EGraphVariableType.Int;

    [HorizontalGroup("Entry")]
    [ShowIf(nameof(type), EGraphVariableType.String)]
    [LabelText("Value")]
    public string stringValue = "";

    [HorizontalGroup("Entry")]
    [ShowIf(nameof(type), EGraphVariableType.Int)]
    [LabelText("Value")]
    public int intValue;

    [HorizontalGroup("Entry")]
    [ShowIf(nameof(type), EGraphVariableType.Float)]
    [LabelText("Value")]
    public float floatValue;

    [HorizontalGroup("Entry")]
    [ShowIf(nameof(type), EGraphVariableType.Bool)]
    [LabelText("Value")]
    public bool boolValue;

    [HorizontalGroup("Entry")]
    [ShowIf(nameof(type), EGraphVariableType.Vector3)]
    [LabelText("Value")]
    public UnityEngine.Vector3 vector3Value;

    [HorizontalGroup("Entry")]
    [ShowIf(nameof(type), EGraphVariableType.Vector2)]
    [LabelText("Value")]
    public UnityEngine.Vector2 vector2Value;

    [HorizontalGroup("Entry")]
    [ShowIf(nameof(type), EGraphVariableType.Color)]
    [LabelText("Value")]
    public UnityEngine.Color colorValue = UnityEngine.Color.white;

    [Tooltip("Optional author note; surfaced by the variable picker and the text authoring importer.")]
    public string description = "";

    /// <summary>
    ///   The declared default as a boxed value of the declared type.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The serialized type value is outside the supported set.</exception>
    public object GetDefaultValue()
    {
      return type switch
      {
        EGraphVariableType.String => stringValue,
        EGraphVariableType.Int => intValue,
        EGraphVariableType.Float => floatValue,
        EGraphVariableType.Bool => boolValue,
        EGraphVariableType.Vector3 => vector3Value,
        EGraphVariableType.Vector2 => vector2Value,
        EGraphVariableType.Color => colorValue,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported graph variable type for key '{key}'.")
      };
    }

    /// <summary>
    ///   The runtime <see cref="System.Type" /> corresponding to the declared type.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The serialized type value is outside the supported set.</exception>
    public Type GetValueType()
    {
      return type switch
      {
        EGraphVariableType.String => typeof(string),
        EGraphVariableType.Int => typeof(int),
        EGraphVariableType.Float => typeof(float),
        EGraphVariableType.Bool => typeof(bool),
        EGraphVariableType.Vector3 => typeof(UnityEngine.Vector3),
        EGraphVariableType.Vector2 => typeof(UnityEngine.Vector2),
        EGraphVariableType.Color => typeof(UnityEngine.Color),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"Unsupported graph variable type for key '{key}'.")
      };
    }
  }
}
