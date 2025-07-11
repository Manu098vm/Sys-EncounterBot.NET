using PKHeX.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets7B;
using PKHeX.Core.Searching;

namespace SysBot.Pokemon
{
    public class EncounterBot7B : PokeRoutineExecutor7B
    {
        protected readonly PokeBotHub<PK8> Hub;
        private readonly IDumper DumpSetting;
        protected readonly Encounter7BSettings Settings;
        protected readonly int[] DesiredMinIVs;
        protected readonly int[] DesiredMaxIVs;
        private readonly IReadOnlyList<string> WantedNatures;
        public EncounterBot7B(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.LGPE_Encounter;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
            StopConditionSettings.ReadWantedNatures(Hub.Config.StopConditions, out WantedNatures);
        }

        protected int encounterCount;
        protected long EncounterTiming;

        public override async Task MainLoop(CancellationToken token)
        {
            EncounterTiming = 0;
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);
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
            var hash = "";
            await DetachController(token).ConfigureAwait(false);
            while (!token.IsCancellationRequested)
            {
                //Force the Fortune Teller Nature value, value is reset at the end of the day
                if (Settings.SetFortuneTellerNature != Nature.Random && 
                    (!await IsNatureTellerEnabled(token).ConfigureAwait(false) || await ReadWildNature(token).ConfigureAwait(false) != Settings.SetFortuneTellerNature))
                {
                    await EnableNatureTeller(token).ConfigureAwait(false);
                    await EditWildNature(Settings.SetFortuneTellerNature, token).ConfigureAwait(false);
                    Log($"Fortune Teller enabled, Nature set to {await ReadWildNature(token).ConfigureAwait(false)}.");
                }

                //Check Lure Type
                if (await ReadLureType(token).ConfigureAwait(false) != Settings.SetLure)
                    await EditLureType((uint)Settings.SetLure, token).ConfigureAwait(false);

                //Check Lure Steps
                if (Settings.SetLure != Lure.None && await ReadLureCounter(token).ConfigureAwait(false) < 20)
                    await EditLureCounter(100, token).ConfigureAwait(false);

                PB7? pk = await DetectAndRead(token).ConfigureAwait(false);

                if (pk is not null && hash != SearchUtil.HashByDetails(pk))
                {
                    await HandleEncounter(pk, token, false);
                    hash = hash.Equals("") || pk is not null ? SearchUtil.HashByDetails(pk) : "";
                }
            }
        }

        private async Task<PB7?> DetectAndRead(CancellationToken token)
        {
            var found = false;
            PB7? pk = null;
            while (!found && !token.IsCancellationRequested)
            {
                if (await IsInCatchScreen(token).ConfigureAwait(false))
                {
                    found = true;
                    pk = await ReadWildOrGo(token).ConfigureAwait(false);
                }
                else if (await IsGiftFound(token).ConfigureAwait(false))
                {
                    found = true;
                    pk = await ReadGiftOrFossil(token).ConfigureAwait(false);
                }
                else if (await IsInBattle(token).ConfigureAwait(false))
                {
                    found = true;
                    pk = await ReadStationary(token).ConfigureAwait(false);

                }
                else if (await IsInTrade(token).ConfigureAwait(false))
                {
                    found = true;
                    pk = await ReadTrade(token).ConfigureAwait(false);
                }
            }

            if (pk is null)
                pk = await ReadMainPokeData(token).ConfigureAwait(false);

            return pk;
        }

        private async Task DoRestartingEncounter(CancellationToken token)
        {
            var mode = Settings.EncounteringType;
            //Check Text Speed
            if (await ReadTextSpeed(token).ConfigureAwait(false) != TextSpeed.Fast)
                await EditTextSpeed(TextSpeed.Fast, token).ConfigureAwait(false);

            //Do not allow video recording for encounters with timers. User can manually record the encounter.
            if (mode is LetsGoMode.Stationary && Hub.Config.StopConditions.CaptureVideoClip)
                Hub.Config.StopConditions.CaptureVideoClip = false;

            while (!token.IsCancellationRequested)
            {
                //Force the Fortune Teller Nature value
                if (Settings.SetFortuneTellerNature != Nature.Random)
                {
                    await EnableNatureTeller(token).ConfigureAwait(false);
                    await EditWildNature(Settings.SetFortuneTellerNature, token).ConfigureAwait(false);
                    Log($"Fortune Teller enabled, Nature set to {await ReadWildNature(token).ConfigureAwait(false)}.");
                }

                await SpamUntilEncounter(token).ConfigureAwait(false);
                var pk = await ReadResetEncounter(mode, token).ConfigureAwait(false);

                if (pk is null)
                    Log("Can not read PKM data. Either a wrong offset has been used, or RAM is shifted.");
                else
                {
                    if (await HandleEncounter(pk, token, false).ConfigureAwait(false))
                    {
                        await Click(HOME, 1_000, token).ConfigureAwait(false);
                        return;
                    }
                }

                await ResetEncounter(mode, token).ConfigureAwait(false);
            }
        }

        private async Task SpamUntilEncounter(CancellationToken token)
        {
            const long MinTimeRecovery = 7_000;
            Stopwatch stopwatch = new();
            stopwatch.Start();

            if (Settings.EncounteringType is LetsGoMode.Stationary)
                while (!(await IsInBattle(token).ConfigureAwait(false) || (await IsInCatchScreen(token).ConfigureAwait(false))))
                {
                    if (EncounterTiming != 0 && stopwatch.ElapsedMilliseconds > EncounterTiming)
                        await DetachController(token).ConfigureAwait(false);
                    await Click(A, 0_200, token).ConfigureAwait(false);
                }
            else
                while (!(await IsInCatchScreen(token).ConfigureAwait(false) || await IsGiftFound(token).ConfigureAwait(false) || await IsInTrade(token).ConfigureAwait(false)))
                {
                    if (EncounterTiming != 0 && stopwatch.ElapsedMilliseconds > EncounterTiming)
                        await DetachController(token).ConfigureAwait(false);
                    await Click(A, 0_200, token).ConfigureAwait(false);
                }

            if (EncounterTiming == 0)
                EncounterTiming = stopwatch.ElapsedMilliseconds + MinTimeRecovery;
        }

        private async Task<PB7?> ReadResetEncounter(LetsGoMode mode, CancellationToken token)
        {
            var pk = mode switch
            {
                LetsGoMode.Stationary => await ReadStationary(token).ConfigureAwait(false),
                LetsGoMode.Fossils => await ReadGiftOrFossil(token).ConfigureAwait(false),
                LetsGoMode.Gifts => await ReadGiftOrFossil(token).ConfigureAwait(false),
                LetsGoMode.Trades => await ReadTrade(token).ConfigureAwait(false),
                LetsGoMode.GoPark => await ReadGoEntity(token).ConfigureAwait(false),
                _ => throw new NotImplementedException(),
            };

            if (pk is null)
                pk = await ReadMainPokeData(token).ConfigureAwait(false);

            return pk;
        }

        private async Task ResetEncounter(LetsGoMode mode, CancellationToken token)
        {
            if (mode is LetsGoMode.GoPark)
            {
                Log("Resetting the encounter by run away...");
                await FleeToOverworld(token).ConfigureAwait(false);
            }
            else
            {
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
        private async Task<bool> HandleEncounter(PB7? pk, CancellationToken token, bool showEndRoutine = true)
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

            var msg = "";
            if(showEndRoutine)
                msg = $"Result found!\n{print}\nStopping routine execution; restart the bot to search again.";

            if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
            Log(msg);

            return true;
        }

        protected async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }
    }
}
