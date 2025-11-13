using System;
using System.Collections.Generic;
using System.Linq;
using MToolKit.Runtime.Core.Interfaces;
using Serilog;
using Serilog.Core;
using VContainer;
using ILogger = Serilog.ILogger;

namespace MToolKit.Runtime.Core.Host
{
  public class GameRuntime : IGameRuntime
  {
    private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameRuntime>().ForFeature("GameRuntime"));
    private static ILogger log => logLazy.Value ?? Logger.None;
    private readonly List<IRuntimeSystem> systems;

    [Inject]
    public GameRuntime(IEnumerable<IRuntimeSystem> systems)
    {
      this.systems = systems.ToList();
    }

    #region IGameRuntime Members

    public void Start()
    {
      systems.ForEach(static s => s.Start());
    }

    public void Tick(float deltaTime)
    {
      systems.ForEach(s => s.Tick(deltaTime));
    }

    public void LateTick(float deltaTime)
    {
      systems.ForEach(s => s.LateTick(deltaTime));
    }

    public void FixedTick(float deltaTime)
    {
      systems.ForEach(s => s.FixedTick(deltaTime));
    }

    public void Shutdown()
    {
      systems.Where(s => s != null).ToList().ForEach(s =>
      {
        try
        {
          s.Shutdown();
        }
        catch (Exception ex)
        {
          // Log error but continue with other systems
          log.ForMethod().Error(ex, "Error shutting down runtime system {SystemType}", s.GetType().Name);
        }
      });
    }

    #endregion
  }
}