using System;
using System.Globalization;
using System.Reflection;
using MToolKit.Runtime.VisualGraphs.Variables;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Variables
{
  public class GraphVariableTextSyntaxTests
  {
    private static Type Syntax => typeof(GraphVariableDeclaration).Assembly.GetType(
      "MToolKit.Runtime.VisualGraphs.Variables.GraphVariableTextSyntax", true);

    [TestCase(EGraphVariableType.String, "\"hello\\n\\\"world\\\"\"")]
    [TestCase(EGraphVariableType.Int, "-42")]
    [TestCase(EGraphVariableType.Float, "1.25")]
    [TestCase(EGraphVariableType.Bool, "true")]
    [TestCase(EGraphVariableType.Vector2, "(1.25, -2)")]
    [TestCase(EGraphVariableType.Vector3, "(1.25, -2, 3)")]
    [TestCase(EGraphVariableType.Color, "#12345678")]
    public void ParsesSevenTypesUnderNonEnglishCulture(EGraphVariableType type, string literal)
    {
      var previous = CultureInfo.CurrentCulture;
      try
      {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var args = new object[] { type, literal, null };
        Assert.That(Syntax.GetMethod("TryParse").Invoke(null, args), Is.True);
        Assert.That(args[2], Is.Not.Null);
        var d = new GraphVariableDeclaration { type = type };
        switch (type)
        {
          case EGraphVariableType.String: d.stringValue = (string)args[2]; break;
          case EGraphVariableType.Int: d.intValue = (int)args[2]; break;
          case EGraphVariableType.Float: d.floatValue = (float)args[2]; break;
          case EGraphVariableType.Bool: d.boolValue = (bool)args[2]; break;
          case EGraphVariableType.Vector2: d.vector2Value = (Vector2)args[2]; break;
          case EGraphVariableType.Vector3: d.vector3Value = (Vector3)args[2]; break;
          case EGraphVariableType.Color: d.colorValue = (Color)args[2]; break;
        }
        var emitted = (string)Syntax.GetMethod("Emit").Invoke(null, new object[] { d });
        var again = new object[] { type, emitted, null };
        Assert.That(Syntax.GetMethod("TryParse").Invoke(null, again), Is.True);
        Assert.That(again[2], Is.EqualTo(args[2]));
      }
      finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestCase(EGraphVariableType.String, "unquoted")]
    [TestCase(EGraphVariableType.String, "\"bad\\q\"")]
    [TestCase(EGraphVariableType.Int, "1.5")]
    [TestCase(EGraphVariableType.Float, "NaN")]
    [TestCase(EGraphVariableType.Float, "1,25")]
    [TestCase(EGraphVariableType.Bool, "yes")]
    [TestCase(EGraphVariableType.Vector2, "(1, 2, 3)")]
    [TestCase(EGraphVariableType.Vector3, "(1, 2)")]
    [TestCase(EGraphVariableType.Color, "#123456")]
    public void RejectsMalformedLiteral(EGraphVariableType type, string literal)
    {
      var args = new object[] { type, literal, null };
      Assert.That(Syntax.GetMethod("TryParse").Invoke(null, args), Is.False);
      Assert.That(args[2], Is.Null);
    }
  }
}
