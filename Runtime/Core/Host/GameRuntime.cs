using System.Collections.Generic;
using System.Linq;
using VContainer;
using Serilog;
using ILogger = Serilog.ILogger;
using System;
using MToolKit.Runtime.Core.Interfaces;

namespace MToolKit.Runtime.Core.Host
{
    public class GameRuntime : IGameRuntime
    {
        private static readonly Lazy<ILogger> logLazy = new(() => Log.Logger.ForContext<GameRuntime>().ForFeature("GameRuntime"));
        private static ILogger log => logLazy.Value ?? Serilog.Core.Logger.None;
        private readonly List<IRuntimeSystem> systems;

        [Inject]
        public GameRuntime(IEnumerable<IRuntimeSystem> systems)
        {
            this.systems = systems.ToList();
        }
        public void Start() => systems.ForEach(static s => s.Start());
        public void Tick(float deltaTime) => systems.ForEach(s => s.Tick(deltaTime));
        public void LateTick(float deltaTime) => systems.ForEach(s => s.LateTick(deltaTime));
        public void FixedTick(float deltaTime) => systems.ForEach(s => s.FixedTick(deltaTime));
        public void Shutdown() => systems.Where(s => s != null).ToList().ForEach(s =>
        {
            try
            {
                s.Shutdown();
            }
            catch (System.Exception ex)
            {
                // Log error but continue with other systems
                log.ForMethod().Error(ex, "Error shutting down runtime system {SystemType}", s.GetType().Name);
            }
        });
    }
}
