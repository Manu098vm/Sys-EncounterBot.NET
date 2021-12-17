using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System;
using PKHeX.Core;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public sealed class EncounterBotFossil : EncounterBot
    {
        private FossilSettings Settings;
        private new readonly BotCompleteCounts Counts;
        private readonly IDumper DumpSettings;
        private readonly int[] DesiredMinIV;
        private readonly int[] DesiredMaxIV;
        private int encounterCount;
        public EncounterBotFossil(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg, hub)
        {
            encounterCount = 0;
            Settings = Hub.Config.SWSH_Encounter.FossilSettings;
            Counts = Hub.Counts;
            DumpSettings = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIV, out DesiredMaxIV);
        }

        protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
        {
            
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("Checking item counts...");
            var pouchData = await Connection.ReadBytesAsync(ItemTreasureAddress, 80, token).ConfigureAwait(false);
            var counts = FossilCount.GetFossilCounts(pouchData);
            int reviveCount = counts.PossibleRevives(Settings.Species);
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
                if (Settings.MaxRevivals > 0 && Settings.MaxRevivals <= reviveCount)
                    maxcount = Settings.MaxRevivals;
                else
                    maxcount = reviveCount;

                await InnerLoop(maxcount, counts, token).ConfigureAwait(false);
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

        private async Task InnerLoop(int reviveCount, FossilCount counts, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (encounterCount != 0 && encounterCount % reviveCount == 0)
                {
                    Log($"Ran out of fossils to revive {Settings.Species}.");
                    Log("Restarting the game to restore the puch data.");
                    await CloseGame(Hub.Config, token).ConfigureAwait(false);
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
                }

                while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                    await Click(B, 0_400, token).ConfigureAwait(false);

                await ReviveFossil(counts, token).ConfigureAwait(false);
                Log("Fossil revived. Checking details...");

                var pk = await ReadUntilPresentPointer(Pointers.GiftPokemon, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);
                if (pk == null)
                    Log("RAM may be shifted, please restart the game and the bot.");
                else
                {
                    encounterCount++;
                    var showdowntext = ShowdownParsing.GetShowdownText(pk);
                    if (pk.IsShiny)
                    {
                        Counts.AddShinyEncounters();
                        if (pk.ShinyXor == 0)
                            showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
                        else
                            showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");
                    }

                    if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                        showdowntext = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {showdowntext}";


                    Log($"Encounter: {encounterCount}:{Environment.NewLine}{showdowntext}{Environment.NewLine}");
                    if (DumpSettings.Dump)
                    {
                        DumpPokemon(DumpSettings.DumpFolder, "fossil", pk);
                        Counts.AddCompletedDumps();
                    }

                    Settings.AddCompletedFossils();

                    if (StopConditionSettings.EncounterFound(pk, DesiredMinIV, DesiredMaxIV, Hub.Config.StopConditions, null))
                    {
                        if (Hub.Config.StopConditions.CaptureVideoClip)
                        {
                            await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                            await PressAndHold(CAPTURE, 2_000, 0, token).ConfigureAwait(false);
                        }

                        Log("Result found! Stopping routine execution; restart the bot(s) to search again.");
                        await DetachController(token).ConfigureAwait(false);
                        return;
                    }
                }
            }
        }

        private async Task ReviveFossil(FossilCount count, CancellationToken token)
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
            if (count.UseSecondOption1(Settings.Species))
                await Click(DDOWN, 0_300, token).ConfigureAwait(false);
            await Click(A, 1_300, token).ConfigureAwait(false);

            // Selecting second fossil.
            if (count.UseSecondOption2(Settings.Species))
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
