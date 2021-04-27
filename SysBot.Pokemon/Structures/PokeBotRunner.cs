using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading;

namespace SysBot.Pokemon
{
    public abstract class PokeBotRunner : BotRunner<PokeBotState>
    {
        public readonly PokeTradeHub<PK8> Hub;

        protected PokeBotRunner(PokeTradeHub<PK8> hub) => Hub = hub;
        protected PokeBotRunner(PokeTradeHubConfig config) => Hub = new PokeTradeHub<PK8>(config);

        protected virtual void AddIntegrations() { }

        public override void Add(RoutineExecutor<PokeBotState> bot)
        {
            base.Add(bot);
        }

        public override bool Remove(IConsoleBotConfig cfg, bool callStop)
        {
            return base.Remove(cfg, callStop);
        }

        public override void StartAll()
        {
            InitializeStart();

            base.StartAll();
        }

        public override void InitializeStart()
        {
            Hub.Counts.LoadCountsFromConfig(); // if user modified them prior to start
            if (RunOnce)
                return;

            AddIntegrations();

            base.InitializeStart();
        }

        public override void StopAll()
        {
            base.StopAll();

            // bots currently don't de-register
            Thread.Sleep(100);
        }

        public override void PauseAll()
        {
            base.PauseAll();
        }

        public override void ResumeAll()
        {
            base.ResumeAll();
        }

        public PokeRoutineExecutor CreateBotFromConfig(PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.Letsgo => new Letsgo(cfg, Hub),
            PokeRoutineType.EggFetch => new EggBot(cfg, Hub),
            PokeRoutineType.GiftBot => new GiftBot(cfg, Hub),
            PokeRoutineType.FossilBot => new FossilBot(cfg, Hub),
            PokeRoutineType.DynamaxAdventure => new DynamaxAdventureBot(cfg, Hub),
            PokeRoutineType.EncounterBot => new EncounterBot(cfg, Hub),
            PokeRoutineType.LiveStatsChecking => new LiveStatsChecking(cfg, Hub),
            PokeRoutineType.TidSidSearcher => new TidSidSearcher(cfg, Hub),
            PokeRoutineType.Idle => new Idle(cfg),
            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };
    }
}