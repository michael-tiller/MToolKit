using System;
using MToolKit.Runtime.VisualGraphs.Variables;
using NUnit.Framework;
using UnityEngine;

namespace MToolKit.Tests.Editor.VisualGraphs.Variables
{
  /// <summary>
  ///   Pins the typed-default and runtime-type mapping of <see cref="GraphVariableDeclaration" /> across the
  ///   full seven-type closed list, and the fail-loud contract for out-of-range serialized enum values.
  /// </summary>
  [TestFixture]
  public sealed class GraphVariableDeclarationTests
  {
    [Test]
    public void GetDefaultValue_ReturnsTypedDefaultForAllSevenTypes()
    {
      var declaration = new GraphVariableDeclaration
      {
        key = "k",
        stringValue = "hello",
        intValue = 7,
        floatValue = 1.5f,
        boolValue = true,
        vector3Value = new Vector3(1f, 2f, 3f),
        vector2Value = new Vector2(4f, 5f)
        // colorValue left at its field default — Color.white, not default(Color)
      };

      declaration.type = EGraphVariableType.String;
      Assert.That(declaration.GetDefaultValue(), Is.EqualTo("hello"));
      declaration.type = EGraphVariableType.Int;
      Assert.That(declaration.GetDefaultValue(), Is.EqualTo(7));
      declaration.type = EGraphVariableType.Float;
      Assert.That(declaration.GetDefaultValue(), Is.EqualTo(1.5f));
      declaration.type = EGraphVariableType.Bool;
      Assert.That(declaration.GetDefaultValue(), Is.True);
      declaration.type = EGraphVariableType.Vector3;
      Assert.That(declaration.GetDefaultValue(), Is.EqualTo(new Vector3(1f, 2f, 3f)));
      declaration.type = EGraphVariableType.Vector2;
      Assert.That(declaration.GetDefaultValue(), Is.EqualTo(new Vector2(4f, 5f)));
      declaration.type = EGraphVariableType.Color;
      Assert.That(declaration.GetDefaultValue(), Is.EqualTo(Color.white),
        "color defaults to white — default(Color) is invisible transparent black, a bad authoring default");
    }

    [Test]
    public void GetValueType_ReturnsRuntimeTypeForAllSevenTypes()
    {
      var declaration = new GraphVariableDeclaration { key = "k" };

      declaration.type = EGraphVariableType.String;
      Assert.That(declaration.GetValueType(), Is.EqualTo(typeof(string)));
      declaration.type = EGraphVariableType.Int;
      Assert.That(declaration.GetValueType(), Is.EqualTo(typeof(int)));
      declaration.type = EGraphVariableType.Float;
      Assert.That(declaration.GetValueType(), Is.EqualTo(typeof(float)));
      declaration.type = EGraphVariableType.Bool;
      Assert.That(declaration.GetValueType(), Is.EqualTo(typeof(bool)));
      declaration.type = EGraphVariableType.Vector3;
      Assert.That(declaration.GetValueType(), Is.EqualTo(typeof(Vector3)));
      declaration.type = EGraphVariableType.Vector2;
      Assert.That(declaration.GetValueType(), Is.EqualTo(typeof(Vector2)));
      declaration.type = EGraphVariableType.Color;
      Assert.That(declaration.GetValueType(), Is.EqualTo(typeof(Color)));
    }

    [Test]
    public void GetDefaultValueAndGetValueType_OutOfRangeEnum_Throw()
    {
      var declaration = new GraphVariableDeclaration { key = "k", type = (EGraphVariableType)999 };

      Assert.Throws<ArgumentOutOfRangeException>(() => declaration.GetDefaultValue(),
        "direct API fails loud on corrupt serialized enums; runtime wrappers guard before calling");
      Assert.Throws<ArgumentOutOfRangeException>(() => declaration.GetValueType());
    }
  }
}
