using System;
using MToolKit.Runtime.Utilities;
using R3;
using Serilog;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using ILogger = Serilog.ILogger;
using Logger = Serilog.Core.Logger;

namespace Theme
{
  public class CurrentTheme : Singleton<CurrentTheme>
  {
    [Serializable]
    public class ThemeEvent : UnityEvent<Theme, Theme> { } // concrete subclass is the only way UnityEvent<T0,T1> serializes/shows in Inspector

    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<CurrentTheme>().ForFeature("Theme"));
    private static ILogger log => logLazy.Value ?? Logger.None;

    protected override bool dontDestroyOnLoad => true;

    [field: SerializeField]
    [field: Required]
    public ThemeRegistry ThemeRegistry { get; private set; }

    [field: SerializeField]
    public Theme DefaultTheme { get; private set; }

    private readonly ReactiveProperty<Theme> onThemeUpdated = new();
    public Observable<Theme> OnThemeUpdated => onThemeUpdated; // current theme (replay-1 to late subscribers)
    public Observable<(Theme Previous, Theme Current)> OnThemeChanged => onThemeUpdated.Pairwise(); // old/new transitions

    [SerializeField]
    private ThemeEvent onThemeUpdatedEvent = new(); // Inspector-wired listeners (designers); R3 is for code subscribers

    [ShowInInspector]
    [ReadOnly]
    private Theme current;

    public Theme Current => current;

    protected override void Awake()
    {
      base.Awake();
      SetTheme(DefaultTheme ? DefaultTheme : ThemeRegistry.Themes[0]);
    }

    public void SetTheme(string id, bool isSilent = false)
    {
      SetTheme(ThemeRegistry.GetTheme(id), isSilent);
    }

    public void SetTheme(Theme theme, bool isSilent = false)
    {
      Theme previous = current;
      current = theme; // always update current, even when silent
      if (!isSilent)
      {
        onThemeUpdated.Value = theme; // R3 value (replay-1); subscribers wanting old/new use OnThemeChanged (Pairwise)
        onThemeUpdatedEvent.Invoke(previous, theme); // Inspector-wired listeners get (old, new)
      }
    }
  }
}