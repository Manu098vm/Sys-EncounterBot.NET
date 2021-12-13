using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class FossilBot : PokeRoutineExecutor8, IEncounterBot
    {
        private readonly PokeBotHub<PK8> Hub;
        private readonly FossilSettings Settings;
        private readonly BotCompleteCounts Count;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        protected SWSH.PokeDataPointers Pointers { get; private set; } = new SWSH.PokeDataPointers();
        public ICountSettings Counts => Settings;

        public FossilBot(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.SWSH_Fossil;
            Count = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
        }

        private int encounterCount;

        public override async Task MainLoop(CancellationToken token)
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

            //TODO: Check free box space

            try
            {
                await InitializeHardware(Settings, token).ConfigureAwait(false);
                Log($"Starting main FossilBot loop.");
                Config.IterateNextRoutine();
                await InnerLoop(reviveCount, pouchData, counts, token).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(FossilBot)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        private async Task InnerLoop(int reviveCount, byte[] pouchData, FossilCount counts, CancellationToken token)
        {
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.SWSH_FossilBot)
            {
                if (encounterCount != 0 && encounterCount % reviveCount == 0)
                {
                    Log($"Ran out of fossils to revive {Settings.Species}.");
                    Log("Restarting the game to restore the puch data.");
                    await CloseGame(Hub.Config, token).ConfigureAwait(false);
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
                }

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
                        Count.AddShinyEncounters();
                        if (pk.ShinyXor == 0)
                            showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
                        else
                            showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");
                    }

                    if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                        showdowntext = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {showdowntext}";


                    Log($"Encounter: {encounterCount}:{Environment.NewLine}{showdowntext}{Environment.NewLine}");
                    if (DumpSetting.Dump)
                    {
                        DumpPokemon(DumpSetting.DumpFolder, "fossil", pk);
                        Count.AddCompletedDumps();
                    }

                    Settings.AddCompletedFossils();

                    if (StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, null))
                    {
                        if (Hub.Config.StopConditions.CaptureVideoClip)
                        {
                            await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                            await PressAndHold(CAPTURE, 2_000, 1_000, token).ConfigureAwait(false);
                        }

                        Log("Result found! Stopping routine execution; restart the bot(s) to search again.");
                        await DetachController(token).ConfigureAwait(false);
                        return;
                    }

                    IsWaiting = true;
                    while (IsWaiting)
                        await Task.Delay(1_000, token).ConfigureAwait(false);

                    while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                        await Click(B, 0_200, token).ConfigureAwait(false);
                }
            }
        }

        public override async Task HardStop()
        {
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        private bool IsWaiting;
        public void Acknowledge() => IsWaiting = false;

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
            for (int i = 0; i < 8; i++)
                await Click(A, 0_400, token).ConfigureAwait(false);

            // Safe to mash B from here until we get out of all menus.
            while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                await Click(B, 0_400, token).ConfigureAwait(false);
        }
    }
}
