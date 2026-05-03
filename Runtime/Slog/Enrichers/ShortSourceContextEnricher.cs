using System.Text;
using Serilog.Core;
using Serilog.Events;

namespace Serilog
{
  // Splits a fully-qualified SourceContext into class name (kept in SourceContext) and
  // namespace (deposited as `feature` only if no explicit ForFeature has been set upstream).
  // For closed generic types, type arguments are recursively shortened, e.g.
  //   "MToolKit.Runtime.Core.Abstractions.DomainPlugin`2[[MToolKit...SettingsSystem, ...],[MToolKit...ISettingsSystem, ...]]"
  // becomes SourceContext="DomainPlugin<SettingsSystem,ISettingsSystem>" with feature="MToolKit.Runtime.Core.Abstractions".
  public class ShortSourceContextEnricher : ILogEventEnricher
  {
    private const string SOURCE_CONTEXT_PROPERTY = "SourceContext";
    private const string FEATURE_PROPERTY = "feature";

    #region ILogEventEnricher Members

    public void Enrich(LogEvent evt, ILogEventPropertyFactory propertyFactory)
    {
      if (!evt.Properties.TryGetValue(SOURCE_CONTEXT_PROPERTY, out LogEventPropertyValue value))
        return;

      if (value is not ScalarValue scalar || scalar.Value is not string fullName || string.IsNullOrEmpty(fullName))
        return;

      string shortName = ShortenTypeName(fullName);
      string namespacePart = ExtractNamespace(fullName);

      if (string.IsNullOrEmpty(shortName))
        return;

      evt.AddOrUpdateProperty(propertyFactory.CreateProperty(SOURCE_CONTEXT_PROPERTY, shortName));
      if (!string.IsNullOrEmpty(namespacePart))
        evt.AddPropertyIfAbsent(propertyFactory.CreateProperty(FEATURE_PROPERTY, namespacePart));
    }

    #endregion

    // Returns the namespace of the OUTER type only (we don't deposit per-arg namespaces).
    private static string ExtractNamespace(string name)
    {
      int declEnd = FindDeclarationEnd(name);
      int lastDot = name.LastIndexOf('.', declEnd - 1);
      return lastDot < 0 ? null : name.Substring(0, lastDot);
    }

    // Recursively shortens a (possibly closed-generic) assembly-qualified type name into:
    //   "Foo"                    for non-generic
    //   "Foo<Bar,Baz>"           for closed generic (args recursively shortened)
    //   "Foo`2"                  for open generic (no type-arg list present)
    private static string ShortenTypeName(string name)
    {
      int declEnd = FindDeclarationEnd(name);
      int lastDot = name.LastIndexOf('.', declEnd - 1);
      string shortName = lastDot < 0
        ? name.Substring(0, declEnd)
        : name.Substring(lastDot + 1, declEnd - lastDot - 1);

      // No backtick → not a generic, we're done.
      int tick = name.IndexOf('`');
      if (tick < 0)
        return shortName;

      // Backtick but no following '[' → open generic (e.g. typeof(List<>)). Keep the arity.
      int argsOpen = name.IndexOf('[', tick);
      if (argsOpen < 0)
        return name.Substring(lastDot < 0 ? 0 : lastDot + 1);

      int argsClose = FindMatchingBracket(name, argsOpen);
      if (argsClose < 0)
        return shortName;

      StringBuilder sb = new(shortName);
      sb.Append('<');
      bool first = true;
      int i = argsOpen + 1;
      while (i < argsClose)
      {
        if (name[i] != '[') { i++; continue; }

        int innerClose = FindMatchingBracket(name, i);
        if (innerClose < 0 || innerClose > argsClose) break;

        // Inner block: "TypeFullName, Asm, Version=..., Culture=..., PublicKeyToken=..."
        // The type name itself may contain nested "[[...]]" for nested generics, so we split on
        // the FIRST top-level comma.
        int comma = FindTopLevelComma(name, i + 1, innerClose);
        string typeNamePart = comma < 0
          ? name.Substring(i + 1, innerClose - i - 1)
          : name.Substring(i + 1, comma - i - 1);

        if (!first) sb.Append(',');
        sb.Append(ShortenTypeName(typeNamePart.Trim()));
        first = false;
        i = innerClose + 1;
      }
      sb.Append('>');
      return sb.ToString();
    }

    // The "declaration" of a type ends at the first '`' (arity marker) or '[' (type-arg list),
    // whichever comes first. For non-generics it ends at the end of the string.
    private static int FindDeclarationEnd(string name)
    {
      int end = name.Length;
      int tick = name.IndexOf('`');
      int bracket = name.IndexOf('[');
      if (tick >= 0 && tick < end) end = tick;
      if (bracket >= 0 && bracket < end) end = bracket;
      return end;
    }

    private static int FindMatchingBracket(string s, int openIdx)
    {
      int depth = 0;
      for (int i = openIdx; i < s.Length; i++)
      {
        char c = s[i];
        if (c == '[') depth++;
        else if (c == ']')
        {
          depth--;
          if (depth == 0) return i;
        }
      }
      return -1;
    }

    private static int FindTopLevelComma(string s, int start, int end)
    {
      int depth = 0;
      for (int i = start; i < end; i++)
      {
        char c = s[i];
        if (c == '[') depth++;
        else if (c == ']') depth--;
        else if (c == ',' && depth == 0) return i;
      }
      return -1;
    }
  }
}
