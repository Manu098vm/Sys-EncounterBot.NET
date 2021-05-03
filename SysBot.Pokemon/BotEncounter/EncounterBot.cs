using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class EncounterBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredIVs;
        private readonly byte[] BattleMenuReady = { 0, 0, 0, 255 };

        public EncounterBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
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

            Log("Starting main EncounterBot loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);

            var task = Hub.Config.SWSH_Encounter.EncounteringType switch
            {
                EncounterMode.LiveStatsChecking => DoLiveStatsChecking(token),
                EncounterMode.Regis => DoRestartingEncounter(token),
                EncounterMode.Regigigas => DoRestartingEncounter(token),
                EncounterMode.Spiritomb => DoRestartingEncounter(token),
                EncounterMode.SwordsJustice => DoRestartingEncounter(token),
                EncounterMode.Eternatus => DoRestartingEncounter(token),
                EncounterMode.Dogs_or_Calyrex => DoDogEncounter(token),
                EncounterMode.Keldeo => DoKeldeoEncounter(token),
                _ => DoLiveStatsChecking(token),
            };
            await task.ConfigureAwait(false);

            await ResetStick(token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }
        private async Task DoRestartingEncounter(CancellationToken token)
        {
            EncounterMode type = Hub.Config.SWSH_Encounter.EncounteringType;
            uint encounterOffset = (type == EncounterMode.Regigigas || type == EncounterMode.Eternatus) ? RaidPokemonOffset : WildPokemonOffset;
            bool isLegendary = !(type == EncounterMode.Spiritomb);
            bool skipRoutine = (type == EncounterMode.Spiritomb || type == EncounterMode.SwordsJustice);

            while (!token.IsCancellationRequested)
            {
                if (!skipRoutine)
                {
                    Log($"Looking for {type}...");

                    if (type == EncounterMode.Eternatus)
                    {
                        await SetStick(LEFT, 0, 20_000, 1_000, token).ConfigureAwait(false);
                        await ResetStick(token).ConfigureAwait(false);
                    }

                    //Click through all the menus until the encounter.
                    while (!await IsInBattle(token).ConfigureAwait(false))
                        await Click(A, 1_000, token).ConfigureAwait(false);

                    Log("An encounter has started! Checking details...");

                    var pk = await ReadUntilPresent(encounterOffset, 2_000, 0_200, token).ConfigureAwait(false);
                    if (pk != null)
                    {
                        if (await HandleEncounter(pk, isLegendary, token).ConfigureAwait(false))
                            return;
                    }

                    Log($"Resetting {type} by restarting the game");
                }

                skipRoutine = false;
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task DoLiveStatsChecking(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (await IsInBattle(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                while (!await IsInBattle(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                Log("Encounter started! Checking details...");
                var pk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                    if (pk == null)
                        pk = await ReadUntilPresent(LegendaryPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                }

                if (pk == null)
                    Log("Check error. Either a wrong offset is used, or the RAM is shifted.");
                else
                    await HandleEncounter(pk, IsPKLegendary(pk.Species), token);
            }
        }

        private async Task DoDogEncounter(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Log("Looking for a new legendary...");

                // At the start of each loop, an A press is needed to exit out of a prompt.
                await Click(A, 0_100, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 30000, 1_000, token).ConfigureAwait(false);

                // Encounters Zacian/Zamazenta and clicks through all the menus.
                while (!await IsInBattle(token).ConfigureAwait(false))
                    await Click(A, 0_300, token).ConfigureAwait(false);

                Log("Encounter started! Checking details...");
                var pk = await ReadUntilPresent(LegendaryPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    Log("Invalid data detected. Restarting loop.");
                    continue;
                }

                // Get rid of any stick stuff left over so we can flee properly.
                await ResetStick(token).ConfigureAwait(false);

                // Wait for the entire cutscene.
                await Task.Delay(15_000, token).ConfigureAwait(false);

                // Offsets are flickery so make sure we see it 3 times.
                for (int i = 0; i < 3; i++)
                    await ReadUntilChanged(BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);

                if (await HandleEncounter(pk, true, token).ConfigureAwait(false))
                    return;

                Log("Running away...");
                await FleeToOverworld(token).ConfigureAwait(false);

                // Extra delay to be sure we're fully out of the battle.
                await Task.Delay(0_250, token).ConfigureAwait(false);
            }
        }

        private async Task DoKeldeoEncounter(CancellationToken token)
        {
            int tries = 0;
            while (!token.IsCancellationRequested)
            {
                await ResetStick(token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                while (!await IsInBattle(token).ConfigureAwait(false) && tries < 15)
                {
                    await Click(LSTICK, 0_000, token);
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                    tries++;
                }
                    
                    
                await ResetStick(token).ConfigureAwait(false);

                if (await IsInBattle(token).ConfigureAwait(false))
                {
                    tries = 0;
                    Log("Encounter started! Checking details...");
                    var pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                    if (pk == null)
                    {
                        // Flee and continue looping.
                        while (await IsInBattle(token).ConfigureAwait(false))
                            await FleeToOverworld(token).ConfigureAwait(false);
                        continue;
                    }

                    if (await HandleEncounter(pk, true, token).ConfigureAwait(false))
                        return;

                }
                else if(tries >= 15)
                {
                    Log("The starting position is probably wrong. If you see this message more than one time consider change your starting position and save the game again.");
                    tries = 0;
                }

                Log("Restarting game...");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task<bool> HandleEncounter(PK8 pk, bool legends, CancellationToken token)
        {
            encounterCount++;

            //Star/Square Shiny Recognition
            var showdowntext = ShowdownParsing.GetShowdownText(pk);
            if (pk.IsShiny && pk.ShinyXor == 0)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
            else if(pk.IsShiny)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");

            Log($"Encounter: {encounterCount}{Environment.NewLine}{Environment.NewLine}{showdowntext}{Environment.NewLine}{GetRibbonsList(pk)}{Environment.NewLine}");
            if (legends)
                Counts.AddCompletedLegends();
            else
                Counts.AddCompletedEncounters();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, legends ? "legends" : "encounters", pk);

            if (StopConditionSettings.EncounterFound(pk, DesiredIVs, Hub.Config.StopConditions))
            {
                if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag) && Hub.Config.SWSH_Encounter.EncounteringType != EncounterMode.LiveStatsChecking)
                    Log($"<@{Hub.Config.Discord.UserTag}> result found! Stopping routine execution; restart the bot(s) to search again.");
                else if(Hub.Config.SWSH_Encounter.EncounteringType != EncounterMode.LiveStatsChecking)
                    Log("Result found! Stopping routine execution; restart the bot(s) to search again.");
                if (Hub.Config.StopConditions.CaptureVideoClip)
                {
                    await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                    await PressAndHold(CAPTURE, 2_000, 1_000, token).ConfigureAwait(false);
                }
                return true;
            }
            return false;
        }

        private string GetRibbonsList(PK8 pk)
        {
            string ribbonsList = "";
            for (var mark = MarkIndex.MarkLunchtime; mark <= MarkIndex.MarkSlump; mark++)
                if (pk.GetRibbon((int)mark))
                    ribbonsList += mark;

            return ribbonsList;
        }

        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }

        private async Task FleeToOverworld(CancellationToken token)
        {
            // This routine will always escape a battle.
            await Click(DUP, 0_200, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);

            while (await IsInBattle(token).ConfigureAwait(false))
            {
                await Click(B, 0_500, token).ConfigureAwait(false);
                await Click(B, 1_000, token).ConfigureAwait(false);
                await Click(DUP, 0_200, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
            }
        }
    }
}
