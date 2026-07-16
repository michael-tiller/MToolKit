using MToolKit.Runtime.Core.Abstractions;
using Serilog;
using VContainer;

namespace MToolKit.Theme
{
  /// <summary>
  ///   Bridges the existing <see cref="CurrentTheme" /> singleton into DI as <see cref="IThemeService" />.
  ///   Lives in the Theme assembly because GlobalInstaller (Runtime) can't reference Theme without a
  ///   circular asmdef. Not a ConfigPlugin: nothing to create or configure — CurrentTheme bootstraps
  ///   itself as a Singleton, so this is just the one registration. The static <see cref="CurrentTheme.Instance" />
  ///   stays the access path for bootstrapper/edit-time code where DI isn't folded in yet; this only
  ///   adds the injectable seam for code that wants it. Add the prefab to GlobalPluginConfig.GlobalPluginPrefabs.
  /// </summary>
  public class ThemePlugin : AbstractGamePlugin
  {
    // Lazy factory: resolves the live singleton on first inject. CurrentTheme is DDOL, so it survives
    // scene loads and the captured instance stays valid.
    public override void Register(IContainerBuilder builder)
    {
      builder.Register<IThemeService>(_ => CurrentTheme.Instance, Lifetime.Singleton);

      // Confirms the global installer picked up this prefab and ran Register. Guard with HasInstance so
      // we never force-create the singleton here — registration order vs CurrentTheme.Awake isn't fixed,
      // and a blank auto-created CurrentTheme would NRE in its own Awake. Information so it clears the
      // framework verbosity floor while verifying; drop to Verbose once you trust the wiring.
      string theme = CurrentTheme.HasInstance ? CurrentTheme.Instance.Current?.Id ?? "no theme set" : "singleton not alive yet";
      log.ForGameObject(gameObject).ForMethod().Information("ThemePlugin registered IThemeService -> CurrentTheme (theme: {Theme})", theme);
    }
  }
}
