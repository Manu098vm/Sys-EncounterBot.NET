using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class FossilBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredIVs;

        public FossilBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            DesiredIVs = StopConditionSettings.InitializeTargetIVs(Hub);
        }

        private int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);
            Log("Checking item counts...");
            var pouchData = await Connection.ReadBytesAsync(ItemTreasureAddress, 80, token).ConfigureAwait(false);
            var counts = FossilCount.GetFossilCounts(pouchData);
            int reviveCount = counts.PossibleRevives(Hub.Config.Fossil.Species);
            if (reviveCount == 0)
            {
                Log("Insufficient fossil pieces. Please obtain at least one of each required fossil piece first.");
                return;
            }

            Config.IterateNextRoutine();

            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.FossilBot)
            {
                await ReviveFossil(counts, token).ConfigureAwait(false);
                Log("Fossil revived. Checking details...");

                var pk = await ReadUntilPresent(await ParsePointer(giftpoke, token), 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    Log("RAM probably shifted.");
                    continue;
                }

                encounterCount++;

                string showdowntext = ShowdownParsing.GetShowdownText(pk);
                if (pk.IsShiny && pk.ShinyXor == 0)
                    showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
                else if (pk.IsShiny)
                    showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");

                Log($"Encounter: {encounterCount}:{Environment.NewLine}{Environment.NewLine}{showdowntext}{Environment.NewLine}{Environment.NewLine}");
                if (DumpSetting.Dump)
                    DumpPokemon(DumpSetting.DumpFolder, "fossil", pk);

                Counts.AddCompletedFossils();

                if (StopConditionSettings.EncounterFound(pk, DesiredIVs, Hub.Config.StopConditions))
                {
                    if (Hub.Config.StopConditions.CaptureVideoClip)
                    {
                        await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                        await PressAndHold(CAPTURE, 2_000, 1_000, token).ConfigureAwait(false);
                    }

                    if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                        Log($"<@{Hub.Config.Discord.UserTag}> Result found! Stopping routine execution; restart the bot(s) to search again.");
                    else
                        Log("Result found! Stopping routine execution; restart the bot(s) to search again.");

                    await DetachController(token).ConfigureAwait(false);
                    return;
                }
                else
                {
                    await CloseGame(Hub.Config, token).ConfigureAwait(false);
                    await StartGame(Hub.Config, token).ConfigureAwait(false);
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
            if (count.UseSecondOption1(Hub.Config.Fossil.Species))
                await Click(DDOWN, 0_300, token).ConfigureAwait(false);
            await Click(A, 1_300, token).ConfigureAwait(false);

            // Selecting second fossil.
            if (count.UseSecondOption2(Hub.Config.Fossil.Species))
                await Click(DDOWN, 300, token).ConfigureAwait(false);

            // A spam through accepting the fossil and agreeing to revive.
            while (await ReadUntilPresent(await ParsePointer(giftpoke, token), 2_000, 0_200, token).ConfigureAwait(false) == null)
            {
                await Click(A, 0_400, token).ConfigureAwait(false);
                await Task.Delay(1_000, token).ConfigureAwait(false);
            }
        }
    }
}
