using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class GiftBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredIVs;

        public GiftBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            DesiredIVs = StopConditionSettings.InitializeTargetIVs(Hub);
        }

        private int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if(Hub.Config.StopConditions.MarkTarget != 0)
                {
                    Log("Gift Pokemon cannot have Marks. Check your stop conditions and restart the bot.");
                    return;
                }

                Log("Looking for a gift pokemon...");

                // Click through all the menus untill the encounter.
                while (!await SWSHIsGiftFound(token).ConfigureAwait(false))
                    await Click(A, 0_250, token).ConfigureAwait(false);

                Log("Gift found! Checking details...");
                var pk = await ReadUntilPresent(await ParsePointer(PokeGift, token), 2_000, 0_200, token).ConfigureAwait(false);
                if (pk != null)
                {
                    if (HandlePokemon(pk, IsPKLegendary(pk.Species)))
                    {
                        if (Hub.Config.StopConditions.CaptureVideoClip)
                        {
                            await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                            await PressAndHold(CAPTURE, 2_000, 1_000, token).ConfigureAwait(false);
                        }
                        return;
                    }
                }

                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private bool HandlePokemon(PK8 pk, bool legends)
        {
            encounterCount++;

            //Star/Square Shiny Recognition
            var showdowntext = ShowdownParsing.GetShowdownText(pk);
            if (pk.IsShiny && pk.ShinyXor == 0)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
            else if (pk.IsShiny)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");

            Log($"Encounter: {encounterCount}{Environment.NewLine}{Environment.NewLine}{showdowntext}{Environment.NewLine}{Environment.NewLine}");
            if (legends)
                Counts.AddCompletedLegends();
            else
                Counts.AddCompletedEncounters();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, legends ? "legends" : "encounters", pk);

            if (StopConditionSettings.EncounterFound(pk, DesiredIVs, Hub.Config.StopConditions))
            {
                if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                    Log($"<@{Hub.Config.Discord.UserTag}> Result found! Stopping routine execution; restart the bot(s) to search again.");
                else
                    Log("Result found! Stopping routine execution; restart the bot(s) to search again.");
           
                return true;
            }
            return false;
        }
    }

}
