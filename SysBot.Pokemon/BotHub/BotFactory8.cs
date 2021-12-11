using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public sealed class BotFactory8 : BotFactory<PK8>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeBotHub<PK8> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            //PokeRoutineType.LGPE_OverworldScan => new OverworldBot(cfg, Hub),
            //PokeRoutineType.LGPE_EncounterBot => new Letsgo(cfg, Hub),
            //PokeRoutineType.SWSH_OverworldScan => new OverworldScan(cfg, Hub),
            PokeRoutineType.BDSP_RNG => new BDSPBotRNG(cfg, Hub),
            PokeRoutineType.SWSH_EggBot => new EggBot(cfg, Hub),
            PokeRoutineType.SWSH_FossilBot => new FossilBot(cfg, Hub),
            PokeRoutineType.SWSH_MaxLair => new MaxLairBot(cfg, Hub),
            PokeRoutineType.SWSH_EncounterBot => Hub.Config.SWSH_Encounter.EncounteringType switch
			{
                EncounterMode.Dogs_or_Calyrex => new EncounterBotDog(cfg, Hub),
                EncounterMode.HorizontalLine => new EncounterBotLine(cfg, Hub),
                EncounterMode.VerticalLine => new EncounterBotLine(cfg, Hub),
                EncounterMode.Keldeo => new EncounterBotKeldeo(cfg, Hub),
                EncounterMode.LiveStatsChecking => new EncounterBotLive(cfg, Hub),
                _ => new EncounterBotReset(cfg, Hub),
			},
            PokeRoutineType.RemoteControl => new RemoteControlBot(cfg),
            _ => throw new ArgumentException(nameof(cfg.NextRoutineType)),
        };

        public override bool SupportsRoutine(PokeRoutineType type) => type switch
        {
            PokeRoutineType.LGPE_OverworldScan => true,
            PokeRoutineType.LGPE_EncounterBot => true,
            PokeRoutineType.SWSH_OverworldScan => true,
            PokeRoutineType.SWSH_EggBot => true,
            PokeRoutineType.SWSH_FossilBot => true,
            PokeRoutineType.SWSH_MaxLair => true,
            PokeRoutineType.SWSH_EncounterBot => true,
            PokeRoutineType.BDSP_RNG => true,
            PokeRoutineType.RemoteControl => true,
            _ => false,
        };
    }
}
