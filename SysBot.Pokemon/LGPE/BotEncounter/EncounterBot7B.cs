using PKHeX.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.Collections.Generic;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets7B;

namespace SysBot.Pokemon
{
    public class EncounterBot7B : PokeRoutineExecutor7B, IEncounterBot
    {
        protected readonly PokeBotHub<PK8> Hub;
        private readonly IDumper DumpSetting;
        protected readonly Encounter7BSettings Settings;
        protected readonly int[] DesiredMinIVs;
        protected readonly int[] DesiredMaxIVs;
        private readonly IReadOnlyList<string> WantedNatures;
        public ICountSettings Counts => Settings;
        public EncounterBot7B(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.LGPE_Encounter;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
            StopConditionSettings.ReadWantedNatures(Hub.Config.StopConditions, out WantedNatures);
        }

        protected int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            await InitializeHardware(Settings, token).ConfigureAwait(false);

            try
            {
                Log($"Starting main {GetType().Name} loop.");
                Config.IterateNextRoutine();

                // Clear out any residual stick weirdness.
                await ResetStick(token).ConfigureAwait(false);
                var task = Hub.Config.LGPE_Encounter.EncounteringType switch
                {
                    LetsGoMode.LiveStatsChecking => DoLiveStatsChecking(token),
                    _ => DoRestartingEncounter(token),
                };
                await task.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {GetType().Name} loop.");
            await HardStop().ConfigureAwait(false);
        }

        private async Task DoLiveStatsChecking(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (Settings.SetFortuneTellerNature is not Nature.Random && !await IsNatureTellerEnabled(token).ConfigureAwait(false))
                {
                    await EnableNatureTeller(token).ConfigureAwait(false);
                    await EditWildNature(Settings.SetFortuneTellerNature, token).ConfigureAwait(false);
                    Log($"Fortune Teller enabled, Nature set to {await ReadWildNature(token).ConfigureAwait(false)}.");
                }

                while (await IsInCatchScreen(token).ConfigureAwait(false) || await IsGiftFound(token).ConfigureAwait(false) || await IsInBattle(token).ConfigureAwait(false) || await IsInTrade(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                while (!await IsInCatchScreen(token).ConfigureAwait(false) && !await IsGiftFound(token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false) && !await IsInTrade(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                if (!await IsInTitleScreen(token).ConfigureAwait(false))
                {
                    Log("Pokémon found! Checking details...");
                    var pk = await ReadUntilPresentMain(PokeData, 2_000, 0_200, token).ConfigureAwait(false);
                    if (pk == null)
                        pk = await ReadUntilPresent(StationaryBattleData, 2_000, 0_200, token).ConfigureAwait(false);

                    if (pk == null)
                        Log("Check error. Either a wrong offset is used, or the RAM is shifted.");
                    else
                        await HandleEncounter(pk, token);
                }
            }
        }

        private async Task DoRestartingEncounter(CancellationToken token)
        {
            var mode = Settings.EncounteringType;
            var offset = mode is LetsGoMode.Stationary ? StationaryBattleData : PokeData;
            var isheap = mode is LetsGoMode.Stationary;
            var stopwatch = new Stopwatch();
            var ms = (long)0;

            if (mode == LetsGoMode.Stationary)
                Log("Ensure to have a powerful Pokémon in the first slot of your team, with a move that can knock out the enemy in a few turns as first move.");

            while (!token.IsCancellationRequested)
            {

                //Force the Fortune Teller Nature value
                if (Settings.SetFortuneTellerNature != Nature.Random)
                {
                    await Task.Delay(2_000, token).ConfigureAwait(false);
                    await EnableNatureTeller(token).ConfigureAwait(false);
                    await EditWildNature(Settings.SetFortuneTellerNature, token).ConfigureAwait(false);
                    Log($"Fortune Teller enabled, Nature set to {await ReadWildNature(token).ConfigureAwait(false)}.");
                }

                stopwatch.Restart();

                //Spam A until battle starts
                if (mode is LetsGoMode.Stationary)
                {
                    while (!await IsInBattle(token).ConfigureAwait(false) && !(ms != 0 && stopwatch.ElapsedMilliseconds > ms))
                        await Click(A, 0_200, token).ConfigureAwait(false);
                    Log("Battle started, checking details...");
                }
                else if (mode == LetsGoMode.Trades)
                {
                    while (!await IsInTrade(token).ConfigureAwait(false) && !(ms != 0 && stopwatch.ElapsedMilliseconds > ms))
                        await Click(A, 0_200, token).ConfigureAwait(false);
                    Log("Trade started, checking details...");
                }
                else if (mode == LetsGoMode.Gifts)
                {
                    while (!await IsGiftFound(token).ConfigureAwait(false) && !(ms != 0 && stopwatch.ElapsedMilliseconds > ms))
                        await Click(A, 0_200, token).ConfigureAwait(false);
                    Log("Gift found, checking details...");
                }

                //Ms taken from a single encounter + margin
                if (ms == 0)
                    ms = stopwatch.ElapsedMilliseconds + 2500;

                var pk = new PB7();
                if (isheap)
                    pk = await ReadUntilPresentMain(offset, 2_000, 0_200, token).ConfigureAwait(false);
                else
                    pk = await ReadUntilPresent(offset, 2_000, 0_200, token).ConfigureAwait(false);

                if (pk != null)
                {
                    if (await HandleEncounter(pk, token).ConfigureAwait(false))
                    {
                        if (mode is LetsGoMode.Stationary)
                        {
                            Log("Result found, defeating the enemy.");
                            stopwatch.Restart();
                            //Spam A until the battle ends
                            while (!await IsInCatchScreen(token).ConfigureAwait(false))
                            {
                                if (stopwatch.ElapsedMilliseconds > 60000)
                                {
                                    Log("Enemy could not be defeated. Target has been lost. Use a pokemon with a more powerful move in the first slot next time.");
                                    return;
                                }
                                await Click(A, 0_500, token).ConfigureAwait(false);
                            }
                        }
                        await Click(HOME, 1_000, token).ConfigureAwait(false);
                        return;
                    }
                }

                Log($"Resetting the encounter by restarting the game...");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await OpenGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        public override async Task HardStop()
        {
            await ResetStick(CancellationToken.None).ConfigureAwait(false);
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        // return true if breaking loop
        private async Task<bool> HandleEncounter(PB7? pk, CancellationToken token)
        {
            if (pk == null)
                return false;

            encounterCount++;
            var print = Hub.Config.StopConditions.GetPrintName(pk);

            if (pk.IsShiny)
            {
                if (pk.ShinyXor == 0)
                    print = print.Replace("Shiny: Yes", "Shiny: Square");
                else
                    print = print.Replace("Shiny: Yes", "Shiny: Star");
            }

            Log($"Encounter: {encounterCount}{Environment.NewLine}{print}{Environment.NewLine}");

            var legendary = Legal.Legends.Contains(pk.Species) || Legal.SubLegends.Contains(pk.Species);
            if (legendary)
                Settings.AddCompletedLegends();
            else
                Settings.AddCompletedEncounters();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, legendary ? "lgpe_legends" : "lgpe_encounters", pk);

            if (!StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions,WantedNatures, null))
                return false;

            if (Hub.Config.StopConditions.CaptureVideoClip)
            {
                await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                await PressAndHold(CAPTURE, 2_000, 0, token).ConfigureAwait(false);
            }

            var msg = $"Result found!\n{print}\nStopping routine execution; restart the bot to search again.";

            if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
            EchoUtil.Echo(msg);
            Log(msg);

            IsWaiting = true;
            while (IsWaiting)
                await Task.Delay(1_000, token).ConfigureAwait(false);
            return false;
        }

        private bool IsWaiting;
        public void Acknowledge() => IsWaiting = false;

        protected async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }
    }
}
