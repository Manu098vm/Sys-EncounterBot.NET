using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public sealed class EncounterBotReset : EncounterBot
    {
        public EncounterBotReset(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg, hub)
        {
        }

        protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
        {
            var type = Hub.Config.SWSH_Encounter.EncounteringType;
            var monoffset = await GetResetOffset(type, token).ConfigureAwait(false);
            var isgift = false;
            var skiproutine = IsStrongSpawn(type);

            if(type is EncounterMode.Trades)
                Log("Be sure to have the requested Pokémon in Box 1 Slot 1!");

            var sw = new Stopwatch();
            var time = (long)0;
            while (!token.IsCancellationRequested)
            {
                if(!skiproutine)
                {
                    sw.Restart();
                    PK8? pk;
                    Log($"Looking for {type}...");

                    do
                    {
                        await DoExtraCommands(token, type).ConfigureAwait(false);

                        if (type is EncounterMode.Gifts) { 
                            isgift = await IsGiftFound(token).ConfigureAwait(false);
                            if (isgift)
                                monoffset = await GetResetOffset(type, token).ConfigureAwait(false); //Reload pointed address when gift is found
                        }

                        pk = await ReadUntilPresent(monoffset, 0_050, 0_050, BoxFormatSlotSize, token).ConfigureAwait(false);

                        if (time == 0)
                        {
                            time = sw.ElapsedMilliseconds + 10_000;
                            sw.Restart();
                        }
                    } while (pk is null && !isgift && sw.ElapsedMilliseconds < time);

                    //SearchUtil.HashByDetails(pkoriginal) == SearchUtil.HashByDetails(pknew)

                    if (await HandleEncounter(pk, token).ConfigureAwait(false))
                        return;

                    Log("No match, resetting the game...");
                }
                skiproutine = false;
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task DoExtraCommands(CancellationToken token, EncounterMode mode)
        {
            switch (mode)
            {
                case EncounterMode.Eternatus or EncounterMode.MotostokeGym:
                    await SetStick(LEFT, 0, 20_000, 0_500, token).ConfigureAwait(false);
                    await ResetStick(token).ConfigureAwait(false);
                    break;
                case EncounterMode.Trades:
                    await DoTrades(token).ConfigureAwait(false);
                    break;
                default:
                    await Click(A, 0_050, token).ConfigureAwait(false);
                    break;
            }
        }

        private async Task DoTrades(CancellationToken token)
        {
            System.Diagnostics.Stopwatch stopwatch = new();
            await SetCurrentBox(0, token).ConfigureAwait(false);
            while (!token.IsCancellationRequested)
            {
                Log("Skipping dialogue...");
                stopwatch.Restart();
                while (stopwatch.ElapsedMilliseconds < 6000 || !await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                    await Click(A, 0_400, token).ConfigureAwait(false);
                Log("Pokémon received. Checking details...");
            }
        }

        private async Task<uint> GetResetOffset(EncounterMode mode, CancellationToken token)
        {
            return mode switch
            {
                EncounterMode.Gifts => await GetAddressFromPointer(Pointers.GiftPokemon, token).ConfigureAwait(false),
                EncounterMode.Trades => BoxStartOffset,
                EncounterMode.Regigigas or EncounterMode.Eternatus => RaidPokemonOffset,
                EncounterMode.MotostokeGym => LegendaryPokemonOffset,
                _ => WildPokemonOffset,
            };
        }

        private bool IsStrongSpawn(EncounterMode mode)
        {
            return mode switch
            {
                EncounterMode.StrongSpawn or EncounterMode.Spiritomb or EncounterMode.SwordsJustice => true,
                _ => false,
            };
        }
    }
}
