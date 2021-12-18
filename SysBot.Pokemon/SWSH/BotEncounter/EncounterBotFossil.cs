using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System;
using PKHeX.Core;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public sealed class EncounterBotFossil : EncounterBot
    {
        public EncounterBotFossil(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg, hub)
        {
        }

        protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
        {
            var settings = Hub.Config.SWSH_Encounter.FossilSettings;

            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("Checking item counts...");
            var pouchData = await Connection.ReadBytesAsync(ItemTreasureAddress, 80, token).ConfigureAwait(false);
            var counts = FossilCount.GetFossilCounts(pouchData);
            int reviveCount = counts.PossibleRevives(settings.Species);
            if (reviveCount == 0)
            {
                Log("Insufficient fossil pieces. Please obtain at least one of each required fossil piece first.");
                return;
            }

            try
            {
                await InitializeHardware(Hub.Config.SWSH_Encounter, token).ConfigureAwait(false);
                Log($"Starting main FossilBot loop.");
                Config.IterateNextRoutine();

                int maxcount;
                if (settings.MaxRevivals > 0 && settings.MaxRevivals <= reviveCount)
                    maxcount = settings.MaxRevivals;
                else
                    maxcount = reviveCount;

                await InnerLoop(maxcount, settings, counts, token).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(EncounterBotFossil)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        private async Task InnerLoop(int reviveCount, FossilSettings settings, FossilCount counts, CancellationToken token)
        {
            Stopwatch sw = new();
            sw.Start();

            while (!token.IsCancellationRequested)
            {
                if ((base.encounterCount != 0 && encounterCount % reviveCount == 0) || sw.ElapsedMilliseconds > 3_000)
                {
                    Log($"Ran out of fossils to revive {settings.Species} or Box space.");
                    Log("Restarting the game.");
                    await CloseGame(Hub.Config, token).ConfigureAwait(false);
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
                }

                while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                    await Click(B, 0_400, token).ConfigureAwait(false);

                await ReviveFossil(settings, counts, token).ConfigureAwait(false);
                Log("Fossil revived. Checking details...");

                var pk = await ReadUntilPresentPointer(Pointers.GiftPokemon, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);

                if (pk == null || pk.Species == 0)
                    Log("RAM may be shifted, please restart the game and the bot.");
                else
                {
                    sw.Restart();

                    if (await HandleEncounter(pk, token).ConfigureAwait(false))
                        return;

                    Log("No match, resetting the game...");
                }
            }
        }

        private async Task ReviveFossil(FossilSettings settings, FossilCount count, CancellationToken token)
        {
            Log("Starting fossil revival routine...");
            if (GameLang == LanguageID.Spanish)
                await Click(A, 0_900, token).ConfigureAwait(false);

            await Click(A, 1_100, token).ConfigureAwait(false);

            // French is slightly slower.
            if (GameLang == LanguageID.French)
                await Task.Delay(0_200, token).ConfigureAwait(false);

            await Click(A, 1_300, token).ConfigureAwait(false);

            // Selecting first fossil.
            if (count.UseSecondOption1(settings.Species))
                await Click(DDOWN, 0_300, token).ConfigureAwait(false);
            await Click(A, 1_300, token).ConfigureAwait(false);

            // Selecting second fossil.
            if (count.UseSecondOption2(settings.Species))
                await Click(DDOWN, 300, token).ConfigureAwait(false);

            // A spam through accepting the fossil and agreeing to revive.
            while (!await IsGiftFound(token).ConfigureAwait(false))
                await Click(A, 0_400, token).ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            await CleanExit(Hub.Config.SWSH_Encounter, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
