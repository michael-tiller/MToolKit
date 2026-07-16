using System;
using MToolKit.Runtime.VisualGraphs.Runtime.Interfaces;
using Serilog;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace MToolKit.Runtime.VisualGraphs.Variables
{
  /// <summary>
  ///   Standard <see cref="IVariableStorage" /> over an <see cref="IGraphState" /> plus an optional
  ///   declaration set. Strictly typed: int and float never cross-convert (matching export validation,
  ///   where int-vs-float is an error).
  /// </summary>
  public sealed class VariableStorage : IVariableStorage
  {
    private static readonly Lazy<ILogger> logLazy = new(() =>
      Log.Logger.ForContext<VariableStorage>().ForFeature("VisualGraphs.State"));

    private static ILogger log => logLazy.Value ?? Logger.None;

    private readonly IGraphState state;
    private readonly GraphVariableSet declarations;

    /// <summary>
    ///   Wrap a graph state. Pass the graph's debuggable state (the loader-constructed
    ///   <c>DebuggableGraphState</c>): this type does not raise debug events itself — mutations emit
    ///   <c>IGraphStateChangeDebugEvent</c> only when the wrapped state does.
    /// </summary>
    /// <param name="state">The storage substrate. Required.</param>
    /// <param name="declarations">Optional declared-variables set supplying typed defaults.</param>
    public VariableStorage(IGraphState state, GraphVariableSet declarations = null)
    {
      this.state = state ?? throw new ArgumentNullException(nameof(state));
      this.declarations = declarations;
    }

    public bool Contains(string key)
    {
      if (string.IsNullOrEmpty(key)) return false;
      return state.Contains(key) || declarations?.Find(key) != null;
    }

    public T Get<T>(string key, T fallbackDefault = default)
    {
      if (string.IsNullOrEmpty(key)) return fallbackDefault;

      if (state.TryGet<T>(key, out var value)) return value;

      if (state.Contains(key))
      {
        log.Error("Stored value for '{Key}' is not a {RequestedType}; returning caller fallback", key, typeof(T).Name);
        return fallbackDefault;
      }

      var declaration = declarations?.Find(key);
      if (IsValidDeclaration(declaration) && declaration.GetDefaultValue() is T declaredDefault) return declaredDefault;

      return fallbackDefault;
    }

    public void Set<T>(string key, T value)
    {
      if (string.IsNullOrEmpty(key))
      {
        log.Error("Set called with a null or empty key; ignoring");
        return;
      }

      var declaration = declarations?.Find(key);
      if (declaration != null && !MatchesDeclaredType(declaration, value))
      {
        log.Error("Set rejected for '{Key}': value type {ValueType} does not match declared type {DeclaredType}",
          key, value?.GetType().Name ?? "null", declaration.type);
        return;
      }

      state.Set(key, value);
    }

    public int Increment(string key, int amount = 1) => Add(key, amount);

    public int Decrement(string key, int amount = 1) => Add(key, -amount);

    public int Add(string key, int delta)
    {
      if (!TryGetNumericBase<int>(key, EGraphVariableType.Int, d => d.intValue, out var baseValue)) return 0;
      var result = baseValue + delta;
      state.Set(key, result);
      return result;
    }

    public float Add(string key, float delta)
    {
      if (!TryGetNumericBase<float>(key, EGraphVariableType.Float, d => d.floatValue, out var baseValue)) return 0f;
      var result = baseValue + delta;
      state.Set(key, result);
      return result;
    }

    public int Multiply(string key, int factor)
    {
      if (!TryGetNumericBase<int>(key, EGraphVariableType.Int, d => d.intValue, out var baseValue)) return 0;
      var result = baseValue * factor;
      state.Set(key, result);
      return result;
    }

    public float Multiply(string key, float factor)
    {
      if (!TryGetNumericBase<float>(key, EGraphVariableType.Float, d => d.floatValue, out var baseValue)) return 0f;
      var result = baseValue * factor;
      state.Set(key, result);
      return result;
    }

    /// <summary>
    ///   Resolve the starting value for an arithmetic op. False = the op must no-op (wrong declared type,
    ///   wrong stored type, or invalid key) — never throws; the caller returns the numeric default.
    /// </summary>
    private bool TryGetNumericBase<T>(string key, EGraphVariableType expectedType,
      Func<GraphVariableDeclaration, T> declaredDefault, out T baseValue) where T : struct
    {
      baseValue = default;

      if (string.IsNullOrEmpty(key))
      {
        log.Error("Arithmetic op called with a null or empty key; ignoring");
        return false;
      }

      var declaration = declarations?.Find(key);
      if (declaration != null && declaration.type != expectedType)
      {
        log.Error("Arithmetic op rejected for '{Key}': declared type {DeclaredType} is not {ExpectedType}",
          key, declaration.type, expectedType);
        return false;
      }

      if (state.TryGet<T>(key, out var stored))
      {
        baseValue = stored;
        return true;
      }

      if (state.Contains(key))
      {
        log.Error("Arithmetic op rejected for '{Key}': stored value is not {ExpectedType}", key, expectedType);
        return false;
      }

      baseValue = declaration != null ? declaredDefault(declaration) : default;
      return true;
    }

    /// <summary>
    ///   Exact-match compatibility against the seven-type closed list — deliberately not IsAssignableFrom.
    ///   Null is a legal value only for declared String.
    /// </summary>
    private static bool MatchesDeclaredType<T>(GraphVariableDeclaration declaration, T value)
    {
      if (!IsValidDeclaration(declaration)) return false;
      if (value == null) return declaration.type == EGraphVariableType.String;
      return value.GetType() == declaration.GetValueType();
    }

    /// <summary>
    ///   Runtime paths must never throw on a corrupt serialized type value; export validation is where
    ///   out-of-range declarations fail loud.
    /// </summary>
    private static bool IsValidDeclaration(GraphVariableDeclaration declaration)
    {
      return declaration != null && Enum.IsDefined(typeof(EGraphVariableType), declaration.type);
    }
  }
}
