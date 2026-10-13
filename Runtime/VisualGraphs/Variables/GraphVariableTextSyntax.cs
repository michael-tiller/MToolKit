using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace MToolKit.Runtime.VisualGraphs.Variables
{
  /// <summary>Shared, culture-independent literal syntax for the seven graph variable types.</summary>
  public static class GraphVariableTextSyntax
  {
    /// <summary>Parse a typed literal. Malformed literals return false and a null value.</summary>
    /// <param name="type">Declared variable type.</param>
    /// <param name="literal">A quoted string, scalar, parenthesized vector, or eight-digit RGBA color prefixed with a hash.</param>
    /// <param name="value">The typed value when parsing succeeds; otherwise null.</param>
    /// <returns>Whether the whole literal is valid for the requested type.</returns>
    public static bool TryParse(EGraphVariableType type, string literal, out object value)
    {
      value = null;
      if (literal == null) return false;
      literal = literal.Trim();
      switch (type)
      {
        case EGraphVariableType.String:
          if (!TryParseString(literal, out var text)) return false;
          value = text;
          return true;
        case EGraphVariableType.Int:
          if (!int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) return false;
          value = integer;
          return true;
        case EGraphVariableType.Float:
          if (!TryFloat(literal, out var number)) return false;
          value = number;
          return true;
        case EGraphVariableType.Bool:
          if (!bool.TryParse(literal, out var boolean)) return false;
          value = boolean;
          return true;
        case EGraphVariableType.Vector2:
        case EGraphVariableType.Vector3:
          var dimensions = type == EGraphVariableType.Vector2 ? 2 : 3;
          if (literal.Length < 2 || literal[0] != '(' || literal[literal.Length - 1] != ')') return false;
          var components = literal.Substring(1, literal.Length - 2).Split(',');
          if (components.Length != dimensions) return false;
          var numbers = new float[dimensions];
          for (var i = 0; i < dimensions; i++)
            if (!TryFloat(components[i].Trim(), out numbers[i])) return false;
          value = dimensions == 2 ? (object)new Vector2(numbers[0], numbers[1]) : new Vector3(numbers[0], numbers[1], numbers[2]);
          return true;
        case EGraphVariableType.Color:
          if (literal.Length != 9 || literal[0] != '#' ||
              !uint.TryParse(literal.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgba)) return false;
          value = (Color)new Color32((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
          return true;
        default:
          return false;
      }
    }

    /// <summary>Emit the canonical literal for a declaration's default value.</summary>
    /// <param name="declaration">Declaration carrying the typed default.</param>
    /// <returns>A literal accepted by TryParse under every culture.</returns>
    /// <exception cref="ArgumentNullException">The declaration is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type is unsupported or a numeric value is not finite.</exception>
    public static string Emit(GraphVariableDeclaration declaration)
    {
      if (declaration == null) throw new ArgumentNullException(nameof(declaration));
      switch (declaration.type)
      {
        case EGraphVariableType.String: return Quote(declaration.stringValue ?? "");
        case EGraphVariableType.Int: return declaration.intValue.ToString(CultureInfo.InvariantCulture);
        case EGraphVariableType.Float: return Float(declaration.floatValue);
        case EGraphVariableType.Bool: return declaration.boolValue ? "true" : "false";
        case EGraphVariableType.Vector2:
          return "(" + Float(declaration.vector2Value.x) + ", " + Float(declaration.vector2Value.y) + ")";
        case EGraphVariableType.Vector3:
          return "(" + Float(declaration.vector3Value.x) + ", " + Float(declaration.vector3Value.y) + ", " + Float(declaration.vector3Value.z) + ")";
        case EGraphVariableType.Color:
          var color = declaration.colorValue;
          Float(color.r); Float(color.g); Float(color.b); Float(color.a);
          return "#" + ColorUtility.ToHtmlStringRGBA(color);
        default: throw new ArgumentOutOfRangeException(nameof(declaration), declaration.type, "Unsupported graph variable type.");
      }
    }

    private static bool TryFloat(string literal, out float value) =>
      float.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);

    private static string Float(float value)
    {
      if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), "Variable literals require finite numbers.");
      return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static bool TryParseString(string literal, out string value)
    {
      value = null;
      if (literal.Length < 2 || literal[0] != '"' || literal[literal.Length - 1] != '"') return false;
      var builder = new StringBuilder();
      for (var i = 1; i < literal.Length - 1; i++)
      {
        var c = literal[i];
        if (c == '"' || char.IsControl(c)) return false;
        if (c != '\\') { builder.Append(c); continue; }
        if (++i >= literal.Length - 1) return false;
        switch (literal[i])
        {
          case '"': builder.Append('"'); break;
          case '\\': builder.Append('\\'); break;
          case 'n': builder.Append('\n'); break;
          case 'r': builder.Append('\r'); break;
          case 't': builder.Append('\t'); break;
          case 'b': builder.Append('\b'); break;
          case 'f': builder.Append('\f'); break;
          case 'u':
            if (i + 4 >= literal.Length - 1 || !ushort.TryParse(literal.Substring(i + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code)) return false;
            builder.Append((char)code);
            i += 4;
            break;
          default: return false;
        }
      }
      value = builder.ToString();
      return true;
    }

    private static string Quote(string value)
    {
      var builder = new StringBuilder("\"");
      foreach (var c in value)
      {
        switch (c)
        {
          case '"': builder.Append("\\\""); break;
          case '\\': builder.Append("\\\\"); break;
          case '\n': builder.Append("\\n"); break;
          case '\r': builder.Append("\\r"); break;
          case '\t': builder.Append("\\t"); break;
          default:
            if (char.IsControl(c) || char.IsSurrogate(c)) builder.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            else builder.Append(c);
            break;
        }
      }
      return builder.Append('"').ToString();
    }
  }
}
