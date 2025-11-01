using MToolKit.Runtime.Core.Interfaces;
using VContainer;

namespace MToolKit.Runtime.Core
{
  public static class GameRoot
  {
    public static PluginRegistry PluginRegistry { get; private set; }
    public static IObjectResolver Resolver { get; private set; }

    public static void Initialize(IObjectResolver resolver)
    {
      Resolver = resolver;
      PluginRegistry = resolver.Resolve<PluginRegistry>();
    }

    public static void RegisterAndInit(IRuntimePlugin plugin)
    {
      PluginRegistry?.InitializeRuntimePlugin(plugin, Resolver);
    }
  }
}