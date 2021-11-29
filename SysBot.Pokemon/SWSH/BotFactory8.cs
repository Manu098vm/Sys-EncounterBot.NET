using System;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public sealed class BotFactory8 : BotFactory<PK8>
    {
        public override PokeRoutineExecutorBase CreateBot(PokeTradeHub<PK8> Hub, PokeBotState cfg) => cfg.NextRoutineType switch
        {
            PokeRoutineType.LGPE_OverworldScan => new OverworldBot(cfg, Hub),
            PokeRoutineType.LGPE_EncounterBot => new Letsgo(cfg, Hub),
            PokeRoutineType.SWSH_OverworldScan => new OverworldScan(cfg, Hub),
            PokeRoutineType.SWSH_EggBot => new EggBot(cfg, Hub),
            PokeRoutineType.SWSH_FossilBot => new FossilBot(cfg, Hub),
            PokeRoutineType.SWSH_DynamaxAdventure => new DynamaxAdventureBot(cfg, Hub),
            PokeRoutineType.SWSH_EncounterBot => new EncounterBot(cfg, Hub),
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
            PokeRoutineType.SWSH_DynamaxAdventure => true,
            PokeRoutineType.SWSH_EncounterBot => true,
            PokeRoutineType.RemoteControl => true,
            _ => false,
        };
    }
}
