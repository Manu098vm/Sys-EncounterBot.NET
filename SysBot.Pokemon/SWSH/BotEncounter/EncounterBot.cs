using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.Collections.Generic;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;

namespace SysBot.Pokemon
{
    public abstract class EncounterBot : PokeRoutineExecutor8, IEncounterBot
    {
        protected readonly PokeBotHub<PK8> Hub;
        public readonly IReadOnlyList<string> UnwantedMarks;
        private readonly IReadOnlyList<string> WantedNatures;
        private readonly IDumper DumpSetting;
        protected readonly EncounterSettings Settings;
        protected readonly int[] DesiredMinIVs;
        protected readonly int[] DesiredMaxIVs;
        protected readonly byte[] BattleMenuReady = { 0, 0, 0, 255 };
        public ICountSettings Counts => Settings;
        protected PokeDataPointers Pointers { get; private set; } = new PokeDataPointers();

        protected EncounterBot(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.SWSH_Encounter;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
            StopConditionSettings.ReadUnwantedMarks(Hub.Config.StopConditions, out UnwantedMarks);
            StopConditionSettings.ReadWantedNatures(Hub.Config.StopConditions, out WantedNatures);
        }

        protected int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            var settings = Hub.Config.SWSH_Encounter;
            Log("Identifying trainer data of the host console.");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            await InitializeHardware(settings, token).ConfigureAwait(false);

            try
            {
                Log($"Starting main {GetType().Name} loop.");
                Config.IterateNextRoutine();

                // Clear out any residual stick weirdness.
                await ResetStick(token).ConfigureAwait(false);
                await EncounterLoop(sav, token).ConfigureAwait(false);
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

        public override async Task HardStop()
        {
            await ResetStick(CancellationToken.None).ConfigureAwait(false);
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        protected abstract Task EncounterLoop(SAV8SWSH sav, CancellationToken token);

        // return true if breaking loop
        protected async Task<bool> HandleEncounter(PK8? pk, CancellationToken token)
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
                DumpPokemon(DumpSetting.DumpFolder, legendary ? "legends" : "encounters", pk);

            if (!StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, WantedNatures, UnwantedMarks))
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
