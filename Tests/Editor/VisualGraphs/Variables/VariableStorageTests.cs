using MToolKit.Runtime.VisualGraphs.Runtime.Debug;
using MToolKit.Runtime.VisualGraphs.Runtime.State;
using MToolKit.Runtime.VisualGraphs.Variables;
using MToolKit.Tests.Editor.VisualGraphs.Support;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MToolKit.Tests.Editor.VisualGraphs.Variables
{
  /// <summary>
  ///   Pins the <see cref="VariableStorage" /> contract: declared-default fallback (read-only — never writes),
  ///   stored-type-corruption detection (caller fallback, NOT the declared default), effective Contains,
  ///   exact-match declared-type enforcement on Set, strictly-typed default-initializing arithmetic
  ///   (int and float never cross-convert), null-key pinning, and the debug-event contract (emission belongs
  ///   to the wrapped <see cref="DebuggableGraphState" />, never to VariableStorage itself).
  /// </summary>
  [TestFixture]
  public sealed class VariableStorageTests : UnityObjectCleanup
  {
    private GraphVariableSet declarations;

    [SetUp]
    public void SetUp()
    {
      // VariableStorage logs rejection paths via Serilog (not Unity Debug) — same convention as GraphRunnerTests:
      // assert observable behavior, never LogAssert.
      LogAssert.ignoreFailingMessages = true;
      NodeDebugEvents.ClearAllSubscribers();
      declarations = Track(ScriptableObject.CreateInstance<GraphVariableSet>());
    }

    [TearDown]
    public void TearDownEvents()
    {
      NodeDebugEvents.ClearAllSubscribers();
    }

    private void Declare(string key, EGraphVariableType type, int intDefault = 0, float floatDefault = 0f)
    {
      declarations.entries.Add(new GraphVariableDeclaration
        { key = key, type = type, intValue = intDefault, floatValue = floatDefault, stringValue = "declared" });
    }

    private VariableStorage NewStorage(out InMemoryGraphState state)
    {
      state = new InMemoryGraphState();
      return new VariableStorage(state, declarations);
    }

    // ---- Get ----

    [Test]
    public void Get_DeclaredKeyMissingFromState_ReturnsDeclaredDefault()
    {
      Declare("hp", EGraphVariableType.Int, intDefault: 50);
      var storage = NewStorage(out var state);

      Assert.That(storage.Get<int>("hp"), Is.EqualTo(50));
      Assert.That(state.Contains("hp"), Is.False, "Get never writes — the default is resolved, not materialized");
    }

    [Test]
    public void Get_UndeclaredKeyMissing_ReturnsCallerFallback()
    {
      var storage = NewStorage(out _);

      Assert.That(storage.Get("mod_key", 42), Is.EqualTo(42));
    }

    [Test]
    public void Get_StoredWrongType_ReturnsFallbackAndDoesNotUseDeclaredDefault()
    {
      Declare("hp", EGraphVariableType.Int, intDefault: 50);
      var storage = NewStorage(out var state);
      state.Set("hp", "corrupted"); // stored value violates the declaration

      Assert.That(storage.Get("hp", -1), Is.EqualTo(-1),
        "a corrupted stored value must surface as the caller fallback, never silently read as a clean declared default");
    }

    [Test]
    public void SetThenGet_RoundTripsEachPrimitiveType()
    {
      var storage = NewStorage(out _);

      storage.Set("s", "hello");
      storage.Set("i", 7);
      storage.Set("f", 1.5f);
      storage.Set("b", true);

      Assert.That(storage.Get<string>("s"), Is.EqualTo("hello"));
      Assert.That(storage.Get<int>("i"), Is.EqualTo(7));
      Assert.That(storage.Get<float>("f"), Is.EqualTo(1.5f));
      Assert.That(storage.Get<bool>("b"), Is.True);
    }

    [Test]
    public void SetThenGet_RoundTripsVector3Vector2Color()
    {
      var storage = NewStorage(out _);

      storage.Set("v3", new Vector3(1f, 2f, 3f));
      storage.Set("v2", new Vector2(4f, 5f));
      storage.Set("c", Color.red);

      Assert.That(storage.Get<Vector3>("v3"), Is.EqualTo(new Vector3(1f, 2f, 3f)));
      Assert.That(storage.Get<Vector2>("v2"), Is.EqualTo(new Vector2(4f, 5f)));
      Assert.That(storage.Get<Color>("c"), Is.EqualTo(Color.red));
    }

    // ---- Contains ----

    [Test]
    public void Contains_DeclaredMissingFromState_ReturnsTrue()
    {
      Declare("hp", EGraphVariableType.Int);
      var storage = NewStorage(out _);

      Assert.That(storage.Contains("hp"), Is.True, "declared defaults are readable values — effective contains");
    }

    [Test]
    public void Contains_UndeclaredMissing_ReturnsFalse()
    {
      var storage = NewStorage(out _);

      Assert.That(storage.Contains("nope"), Is.False);
    }

    // ---- Set enforcement ----

    [Test]
    public void Set_DeclaredWrongType_NoOpsAndLeavesStateUnchanged()
    {
      Declare("hp", EGraphVariableType.Int, intDefault: 50);
      var storage = NewStorage(out var state);

      storage.Set("hp", "not an int");

      Assert.That(state.Contains("hp"), Is.False,
        "typed Set must not become a typed-storage bypass that creates the corruption Get detects");
    }

    [Test]
    public void Set_DeclaredFloatKeyWithInt_NoOps()
    {
      Declare("speed", EGraphVariableType.Float);
      var storage = NewStorage(out var state);

      storage.Set("speed", 5); // exact-match typing: int is not float

      Assert.That(state.Contains("speed"), Is.False);
    }

    [Test]
    public void Set_NullForDeclaredString_Allowed_NullForValueType_Rejected()
    {
      Declare("name", EGraphVariableType.String);
      Declare("hp", EGraphVariableType.Int);
      var storage = NewStorage(out var state);

      storage.Set<string>("name", null);
      storage.Set<object>("hp", null);

      Assert.That(state.Contains("name"), Is.True, "null is a legal value only for declared String");
      Assert.That(state.Contains("hp"), Is.False);
    }

    [Test]
    public void Set_UndeclaredKey_PassesThroughUnrestricted()
    {
      var storage = NewStorage(out var state);

      storage.Set("mod_key", new Vector3(9f, 9f, 9f));

      Assert.That(state.TryGet<Vector3>("mod_key", out var v), Is.True);
      Assert.That(v, Is.EqualTo(new Vector3(9f, 9f, 9f)));
    }

    // ---- Null/empty key pinning ----

    [Test]
    public void NullOrEmptyKey_ContainsFalse_GetFallback_SetAndArithmeticNoOp()
    {
      var storage = NewStorage(out var state);

      Assert.That(storage.Contains(null), Is.False);
      Assert.That(storage.Contains(""), Is.False);
      Assert.That(storage.Get(null, 13), Is.EqualTo(13));
      storage.Set(null, 1);
      storage.Set("", 1);
      Assert.That(storage.Increment(null), Is.EqualTo(0));
      Assert.That(storage.Add("", 2f), Is.EqualTo(0f));
      Assert.That(state.AsReadOnly(), Is.Empty, "null/empty keys never reach the underlying dictionary");
    }

    // ---- Debug events ----

    [Test]
    public void Set_OverDebuggableState_RaisesStateChangedDebugEvent()
    {
      Declare("hp", EGraphVariableType.Int);
      var inner = new InMemoryGraphState();
      inner.Set("hp", 10);
      var storage = new VariableStorage(new DebuggableGraphState(inner, "g1"), declarations);
      using var recorder = new StateChangeRecorder();

      storage.Set("hp", 99);

      Assert.That(recorder.Changes, Has.Count.EqualTo(1),
        "exactly one event per write — VariableStorage never self-emits, the wrapped DebuggableGraphState does");
      Assert.That(recorder.Changes[0], Is.EqualTo(("g1", "hp", (object)10, (object)99)));
    }

    [Test]
    public void Set_OverPlainState_RaisesNoDebugEvent()
    {
      var storage = NewStorage(out _);
      using var recorder = new StateChangeRecorder();

      storage.Set("hp", 99);

      Assert.That(recorder.Changes, Is.Empty,
        "emission belongs to the wrapped state; an unwrapped substrate emits nothing");
    }

    // ---- Arithmetic ----

    [Test]
    public void Increment_MissingDeclaredInt_StartsFromDeclaredDefault()
    {
      Declare("kills", EGraphVariableType.Int, intDefault: 10);
      var storage = NewStorage(out var state);

      Assert.That(storage.Increment("kills"), Is.EqualTo(11));
      Assert.That(state.TryGet<int>("kills", out var v), Is.True);
      Assert.That(v, Is.EqualTo(11), "arithmetic default-initializes: missing key starts from the declared default");
    }

    [Test]
    public void Decrement_MissingDeclaredInt_StartsFromDeclaredDefault()
    {
      Declare("ammo", EGraphVariableType.Int, intDefault: 10);
      var storage = NewStorage(out _);

      Assert.That(storage.Decrement("ammo", 3), Is.EqualTo(7));
    }

    [Test]
    public void Add_Float_MissingDeclared_StartsFromDeclaredDefault()
    {
      Declare("speed", EGraphVariableType.Float, floatDefault: 1.5f);
      var storage = NewStorage(out _);

      Assert.That(storage.Add("speed", 0.5f), Is.EqualTo(2f));
    }

    [Test]
    public void Add_Int_MissingUndeclared_StartsFromZero()
    {
      var storage = NewStorage(out _);

      Assert.That(storage.Add("mod_counter", 4), Is.EqualTo(4));
    }

    [Test]
    public void Multiply_MissingDeclaredFloat_StartsFromDeclaredDefault()
    {
      Declare("multiplier", EGraphVariableType.Float, floatDefault: 2f);
      var storage = NewStorage(out _);

      Assert.That(storage.Multiply("multiplier", 3f), Is.EqualTo(6f));
    }

    [Test]
    public void AddInt_OnDeclaredFloatKey_NoOpsAndLeavesStateUnchanged()
    {
      Declare("speed", EGraphVariableType.Float, floatDefault: 1.5f);
      var storage = NewStorage(out var state);

      Assert.That(storage.Add("speed", 1), Is.EqualTo(0), "strict typing: int ops never touch float declarations");
      Assert.That(state.Contains("speed"), Is.False);
    }

    [Test]
    public void AddFloat_OnDeclaredIntKey_NoOpsAndLeavesStateUnchanged()
    {
      Declare("kills", EGraphVariableType.Int, intDefault: 10);
      var storage = NewStorage(out var state);

      Assert.That(storage.Add("kills", 1f), Is.EqualTo(0f), "strict typing: float ops never touch int declarations");
      Assert.That(state.Contains("kills"), Is.False);
    }

    [Test]
    public void Increment_DeclaredStringKey_NoOpsAndLeavesStateUnchanged()
    {
      Declare("name", EGraphVariableType.String);
      var storage = NewStorage(out var state);

      Assert.That(storage.Increment("name"), Is.EqualTo(0));
      Assert.That(state.Contains("name"), Is.False);
    }

    [Test]
    public void Add_StoredValueWrongType_NoOpsAndLeavesStateUnchanged()
    {
      var storage = NewStorage(out var state);
      state.Set("counter", "corrupted"); // undeclared key with a non-numeric stored value

      Assert.That(storage.Add("counter", 1), Is.EqualTo(0));
      Assert.That(state.TryGet<string>("counter", out var v), Is.True);
      Assert.That(v, Is.EqualTo("corrupted"), "the corrupt stored value is left for diagnosis, not overwritten");
    }
  }
}
