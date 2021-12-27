using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public sealed class EncounterBotLive : EncounterBot
    {
        public EncounterBotLive(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg, hub)
        {
        }

        protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (await IsInBattle(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                while (!await IsInBattle(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                Log("Encounter started! Checking details...");
                var pk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, 344, token).ConfigureAwait(false);
                if (pk == null)
                {
                    pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, 344, token).ConfigureAwait(false);
                    if (pk == null)
                        pk = await ReadUntilPresent(LegendaryPokemonOffset, 2_000, 0_200, 344, token).ConfigureAwait(false);
                }

                if (pk == null)
                    Log("Check error. Either a wrong offset is used, or the RAM is shifted.");
                else
                    await HandleEncounter(pk, token).ConfigureAwait(false);
            }
        }
    }
}
