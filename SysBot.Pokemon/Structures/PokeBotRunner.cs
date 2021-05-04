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
            PokeRoutineType.LGPEOverworldScan => new OverworldBot(cfg, Hub),
            PokeRoutineType.LGPEEncounterBot => new Letsgo(cfg, Hub),
            PokeRoutineType.SWSHOverworldScan => new OverworldScan(cfg, Hub),
            PokeRoutineType.SWSHEggFetch => new EggBot(cfg, Hub),
            PokeRoutineType.SWSHFossilBot => new FossilBot(cfg, Hub),
            PokeRoutineType.SWSHDynamaxAdventure => new DynamaxAdventureBot(cfg, Hub),
            PokeRoutineType.SWSHEncounterBot => new EncounterBot(cfg, Hub),
            PokeRoutineType.Idle => new Idle(cfg),
            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };
    }
}