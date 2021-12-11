using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public sealed class EncounterBotKeldeo : EncounterBot
    {
        public EncounterBotKeldeo(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg, hub)
        {
        }

        protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
        {
            var tries = 0;
            while (!token.IsCancellationRequested)
            {
                await ResetStick(token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                while (!await IsInBattle(token).ConfigureAwait(false) && tries < 15)
                {
                    await Click(LSTICK, 1_000, token);
                    tries++;
                }
                await ResetStick(token).ConfigureAwait(false);
                if (await IsInBattle(token).ConfigureAwait(false))
                {
                    tries = 0;
                    Log("Encounter started! Checking details...");
                    var pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);
                    if (pk == null)
                    {
                        while (await IsInBattle(token).ConfigureAwait(false))
                            await FleeToOverworld(token).ConfigureAwait(false);
                        continue;
                    }

                    if (await HandleEncounter(pk, token).ConfigureAwait(false))
                        return;
                }
                else if (tries >= 15)
                {
                    Log("The starting position is probably wrong. If you see this message more than one time consider change your starting position and save the game again.");
                    tries = 0;
                }
                Log("Restarting game...");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }
    }
}