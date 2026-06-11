namespace MToolKit.Runtime.VisualGraphs.Variables
{
  /// <summary>
  ///   Typed accessor over <see cref="Runtime.Interfaces.IGraphState" /> that resolves declared defaults
  ///   for missing keys. A thin wrapper, NOT a parallel store — all values live in the wrapped state.
  ///   <para>
  ///     The arithmetic operations are NOT idempotent: invoking one twice applies it twice. Callers that
  ///     receive duplicate message deliveries must deduplicate upstream; this surface offers no replay
  ///     protection.
  ///   </para>
  /// </summary>
  public interface IVariableStorage
  {
    /// <summary>
    ///   True when the key has a stored value OR a declaration (declared defaults are readable values).
    ///   Null/empty keys return false.
    /// </summary>
    bool Contains(string key);

    /// <summary>
    ///   Read a value: stored value if present and of type <typeparamref name="T" />; otherwise the declared
    ///   default if it is a <typeparamref name="T" />; otherwise <paramref name="fallbackDefault" />.
    ///   A stored value of the wrong type logs an error and yields <paramref name="fallbackDefault" />
    ///   (never the declared default — corruption must not read as a clean default). Never throws, never writes.
    /// </summary>
    T Get<T>(string key, T fallbackDefault = default);

    /// <summary>
    ///   Write a value. For declared keys the value's runtime type must exactly match the declared type
    ///   (null is allowed only for declared String); a mismatch logs an error and leaves state unchanged.
    ///   Undeclared keys pass through unrestricted (dynamic/mod keys stay legal).
    /// </summary>
    void Set<T>(string key, T value);

    /// <summary>Add <paramref name="amount" /> to an int key (default-initializing; NOT idempotent). Returns the new value.</summary>
    int Increment(string key, int amount = 1);

    /// <summary>Subtract <paramref name="amount" /> from an int key (default-initializing; NOT idempotent). Returns the new value.</summary>
    int Decrement(string key, int amount = 1);

    /// <summary>Add <paramref name="delta" /> to an int key (default-initializing; NOT idempotent). Returns the new value.</summary>
    int Add(string key, int delta);

    /// <summary>Add <paramref name="delta" /> to a float key (default-initializing; NOT idempotent). Returns the new value.</summary>
    float Add(string key, float delta);

    /// <summary>Multiply an int key by <paramref name="factor" /> (default-initializing; NOT idempotent). Returns the new value.</summary>
    int Multiply(string key, int factor);

    /// <summary>Multiply a float key by <paramref name="factor" /> (default-initializing; NOT idempotent). Returns the new value.</summary>
    float Multiply(string key, float factor);
  }
}
