﻿using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        private readonly byte[] BattleMenuReady = { 0, 0, 0, 255 };

        public EncounterBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
        }

        private int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("Starting main EncounterBot loop.");
            Config.IterateNextRoutine();
            Log("CIAOO");

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);
            Log("HEHE");
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
                EncounterMode.VerticalLine => WalkInLine(token),
                EncounterMode.HorizontalLine => WalkInLine(token),
                EncounterMode.Gifts => DoRestartingEncounter(token),
                EncounterMode.Trades => DoTrades(token),
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

                    Log("Here, Click A");
                    await Click(A, 0_300, token).ConfigureAwait(false);

                    //Click through all the menus until the encounter.
                    while (!await IsInBattle(token).ConfigureAwait(false) && !await SWSHIsGiftFound(token).ConfigureAwait(false))
                        await Click(A, 0_300, token).ConfigureAwait(false);

                    Log("An encounter found! Checking details...");

                    PK8? pk;
                    if (type == EncounterMode.Gifts)
                        pk = await ReadUntilPresent(await ParsePointer(PokeGift, token), 2_000, 0_200, token).ConfigureAwait(false);
                    else
                    {
						pk = await ReadUntilPresent(encounterOffset, 2_000, 0_200, token).ConfigureAwait(false);
                        pk = null;
                    }

                    if (pk != null)
                        if (await HandleEncounter(pk, IsPKLegendary(pk.Species), token).ConfigureAwait(false))
                            return;

                    Log($"Resetting {type} by restarting the game");
                }

                skipRoutine = false;
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task DoTrades(CancellationToken token)
        {
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            Log("Be sure to have the requested Pokémon in Box 1 Slot 1!");
            while (!token.IsCancellationRequested)
            {
                await SetCurrentBox(0, token).ConfigureAwait(false);

                Log("Skipping dialogue...");
                stopwatch.Restart();
                while (stopwatch.ElapsedMilliseconds < 6000 || !await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                    await Click(A, 0_400, token).ConfigureAwait(false);

                Log("Pokémon received. Checking details...");
                var pk = await ReadBoxPokemon(0, 0, token).ConfigureAwait(false);
                if (pk != null)
                    if (await HandleEncounter(pk, IsPKLegendary(pk.Species), token).ConfigureAwait(false))
                        return;

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

        private async Task WalkInLine(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var attempts = await StepUntilEncounter(token).ConfigureAwait(false);
                if (attempts < 0) // aborted
                    continue;

                Log($"Encounter found after {attempts} attempts! Checking details...");

                // Reset stick while we wait for the encounter to load.
                await ResetStick(token).ConfigureAwait(false);

                var pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    Log("Invalid data detected. Restarting loop.");

                    // Flee and continue looping.
                    await FleeToOverworld(token).ConfigureAwait(false);
                    continue;
                }

                // Offsets are flickery so make sure we see it 3 times.
                for (int i = 0; i < 3; i++)
                    await ReadUntilChanged(BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);

                if (await HandleEncounter(pk, false, token).ConfigureAwait(false))
                    return;

                Log("Running away...");
                await FleeToOverworld(token).ConfigureAwait(false);
            }
        }

        private async Task<int> StepUntilEncounter(CancellationToken token)
        {
            Log("Walking around until an encounter...");
            int attempts = 0;
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.SWSH_EncounterBot)
            {
                if (!await IsInBattle(token).ConfigureAwait(false))
                {
                    switch (Hub.Config.SWSH_Encounter.EncounteringType)
                    {
                        case EncounterMode.VerticalLine:
                            await SetStick(LEFT, 0, -30000, 2_400, token).ConfigureAwait(false);
                            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset

                            // Quit early if we found an encounter on first sweep.
                            if (await IsInBattle(token).ConfigureAwait(false))
                                break;

                            await SetStick(LEFT, 0, 30000, 2_400, token).ConfigureAwait(false);
                            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset
                            break;
                        case EncounterMode.HorizontalLine:
                            await SetStick(LEFT, -30000, 0, 2_400, token).ConfigureAwait(false);
                            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset

                            // Quit early if we found an encounter on first sweep.
                            if (await IsInBattle(token).ConfigureAwait(false))
                                break;

                            await SetStick(LEFT, 30000, 0, 2_400, token).ConfigureAwait(false);
                            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset
                            break;
                    }

                    attempts++;
                    if (attempts % 10 == 0)
                        Log($"Tried {attempts} times, still no encounters.");
                }

                if (await IsInBattle(token).ConfigureAwait(false))
                    return attempts;
            }

            return -1; // aborted
        }

        private async Task<bool> HandleEncounter(PK8 pk, bool legends, CancellationToken token)
        {
            encounterCount++;

            //Star/Square Shiny recognition
            var showdowntext = ShowdownParsing.GetShowdownText(pk);
            if (pk.IsShiny)
            {
                Counts.AddShinyEncounters();
                if (pk.ShinyXor == 0)
                    showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
                else
                    showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");
            }

            Log($"Encounter: {encounterCount}{Environment.NewLine}{Environment.NewLine}{showdowntext}{Environment.NewLine}{GetRibbonsList(pk)}{Environment.NewLine}");
            if (legends)
                Counts.AddCompletedLegends();
            else
                Counts.AddCompletedEncounters();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            {
                DumpPokemon(DumpSetting.DumpFolder, legends ? "legends" : "encounters", pk);
                Counts.AddCompletedDumps();
            }

            if (StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions))
            {
                if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag) && Hub.Config.SWSH_Encounter.EncounteringType != EncounterMode.LiveStatsChecking)
                    Log($"<@{Hub.Config.Discord.UserTag}> result found! Stopping routine execution; restart the bot(s) to search again.");
                else if(Hub.Config.SWSH_Encounter.EncounteringType != EncounterMode.LiveStatsChecking)
                    Log("Result found!");
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
