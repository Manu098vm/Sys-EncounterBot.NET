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
    public class BotOverworld : PokeRoutineExecutor8, IEncounterBot
    {
        private readonly PokeBotHub<PK8> Hub;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        private readonly IReadOnlyList<string> UnwantedMarks;
        private readonly IReadOnlyList<string> WantedNatures;
        private readonly OverworldScanSettings Settings;
        private readonly byte[] BattleMenuReady = { 0, 0, 0, 255 };
        public ICountSettings Counts => Settings;

        public BotOverworld(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.SWSH_OverworldScan;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
            StopConditionSettings.ReadUnwantedMarks(Hub.Config.StopConditions, out UnwantedMarks);
            StopConditionSettings.ReadWantedNatures(Hub.Config.StopConditions, out WantedNatures);

        }

        private int encounterCount;

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

                var task = Settings.EncounteringType switch
                {
                    ScanMode.OverworldSpawn => Overworld(sav, token),
                    _ => DoSeededEncounter(sav, token),
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

        public override async Task HardStop()
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0, CancellationToken.None).ConfigureAwait(false); // reset
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }

        private async Task DoSeededEncounter(SAV8SWSH sav, CancellationToken token)
        {
            ScanMode type = Hub.Config.SWSH_OverworldScan.EncounteringType;
            Species dexn = 0;
            uint offset = 0x00;
            if (type == ScanMode.G_Articuno)
            {
                dexn = (Species)144;
                offset = PokeDataOffsets.CrownTundraSnowslideSlopeSpawns;
            }
            else if (type == ScanMode.G_Zapdos)
            {
                dexn = (Species)145;
                offset = PokeDataOffsets.WildAreaMotostokeSpawns;
            }
            else if (type == ScanMode.G_Moltres)
            {
                dexn = (Species)146;
                offset = PokeDataOffsets.IsleOfArmorStationSpaws;
            }
            else if (type == ScanMode.IoA_Wailord)
            {
                dexn = (Species)321;
                offset = PokeDataOffsets.IsleOfArmorStationSpaws;
            }

            while (!token.IsCancellationRequested && offset != 0)
            {
                await FlyToRerollSeed(token).ConfigureAwait(false);
                var pkm = await ReadOwPokemon(dexn, offset, null, sav, token).ConfigureAwait(false);
                if (pkm != null && await LogPKMs(pkm, token).ConfigureAwait(false))
                {
                    await Click(X, 2_000, token).ConfigureAwait(false);
                    await Click(R, 2_000, token).ConfigureAwait(false);
                    await Click(A, 5_000, token).ConfigureAwait(false);
                    await Click(X, 2_000, token).ConfigureAwait(false);
                    Log($"The overworld encounter has been found. The progresses has been saved and the game is paused, you can now go and catch {SpeciesName.GetSpeciesName((int)dexn, 2)}");
                    return;
                }
            }
        }
        private async Task FlyToRerollSeed(CancellationToken token)
        {
            var exit = false;
            var encounter = Hub.Config.SWSH_OverworldScan.EncounteringType;

            while (!exit)
            {
                await Click(X, 2_000, token).ConfigureAwait(false);
                await Click(PLUS, 5_000, token).ConfigureAwait(false);

                if (encounter is ScanMode.G_Moltres or ScanMode.IoA_Wailord)
                    await Click(DLEFT, 0_500, token).ConfigureAwait(false);
                else if (encounter is ScanMode.G_Articuno)
                {
                    await PressAndHold(DDOWN, 0_150, 1_000, token).ConfigureAwait(false);
                    await PressAndHold(DRIGHT, 0_090, 1_000, token).ConfigureAwait(false);
                }

                for (int i = 0; i < 5; i++)
                    await Click(A, 1_000, token).ConfigureAwait(false);

                await Task.Delay(4_000, token).ConfigureAwait(false);

                if (encounter is not ScanMode.G_Articuno || (encounter is ScanMode.G_Articuno && await IsArticunoPresent(token).ConfigureAwait(false)))
                    exit = true;
                else
                    Log("Articuno not found on path. Flying to reset Articuno location.");
            }

            //Save the game
            await Click(X, 2_000, token).ConfigureAwait(false);
            await Click(R, 2_000, token).ConfigureAwait(false);
            await Click(A, 5_000, token).ConfigureAwait(false);

            Log("Game saved, checking details from KCoord block...");
        }

        private async Task Overworld(SAV8SWSH sav, CancellationToken token)
        {
            await ResetStick(token).ConfigureAwait(false);
            var movementslist = ParseMovements(Settings.MovementOrder, Settings.MoveUpMs, Settings.MoveRightMs, Settings.MoveDownMs, Settings.MoveLeftMs);
            byte[] KCoordinates;
            List<PK8> PK8s;
            List<PK8> checked_pks = new();

            while (!token.IsCancellationRequested)
            {
                KCoordinates = await ReadKCoordinates(token).ConfigureAwait(false);

                PK8s = await ReadOwPokemonFromBlock(KCoordinates, sav, token).ConfigureAwait(false);
                if (PK8s.Count > 0)
                {
                    foreach (PK8 pkm in PK8s)
                    {
                        if (!CheckIfPresent(pkm, checked_pks))
                        {
                            checked_pks.Add(pkm);

                            //Keep the list small.
                            if (checked_pks.Count >= 50)
                                checked_pks.RemoveAt(0);

                            //Log($"{(Species)pkm.Species}");
                            if (await LogPKMs(pkm, token).ConfigureAwait(false))
                            {
                                //Save the game to update KCoordinates block
                                if (!await IsInBattle(token).ConfigureAwait(false))
                                {
                                    await Click(X, 2_000, token).ConfigureAwait(false);
                                    await Click(R, 2_000, token).ConfigureAwait(false);
                                    await Click(A, 5_000, token).ConfigureAwait(false);
                                    await Click(X, 2_000, token).ConfigureAwait(false);
                                }
                                return;
                            }
                        }
                    }
                }
                else
                    Log("Empty list, no overworld data in KCoordinates!");

                //Check if encountered an unwanted pokemon
                if (await IsInBattle(token).ConfigureAwait(false))
                {
                    // Offsets are flickery so make sure we see it 3 times.
                    for (int i = 0; i < 3; i++)
                        await ReadUntilChanged(PokeDataOffsets.BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);
                    Log("Unwanted encounter started, running away...");
                    await FleeToOverworld(token).ConfigureAwait(false);
                    // Extra delay to be sure we're fully out of the battle.
                    await Task.Delay(0_250, token).ConfigureAwait(false);
                }
                else
                {
                    if (Settings.GetOnOffBike)
                    {
                        await Click(PLUS, 0_600, token).ConfigureAwait(false);
                        await Click(PLUS, 5_500, token).ConfigureAwait(false);
                    }

                    //Movements/Delay/Actions routines
                    if (Settings.WaitMsBeforeSave > 0)
                        await Task.Delay(Settings.WaitMsBeforeSave, token).ConfigureAwait(false);

                    foreach (int[] move in movementslist)
                    {
                        await ResetStick(token).ConfigureAwait(false);
                        await SetStick(LEFT, (short)(move[0]), (short)(move[1]), move[2], token).ConfigureAwait(false);
                        //Check again is a wild encounter popped up while moving
                        if (await IsInBattle(token).ConfigureAwait(false))
                        {
                            await ResetStick(token).ConfigureAwait(false);
                            for (int i = 0; i < 3; i++)
                                await ReadUntilChanged(PokeDataOffsets.BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);
                            Log("Unwanted encounter started, running away...");
                            await FleeToOverworld(token).ConfigureAwait(false);
                            await Task.Delay(0_250, token).ConfigureAwait(false);
                        }
                        await ResetStick(token).ConfigureAwait(false);
                    }

                    //Save the game to update KCoordinates block
                    await Click(X, 2_000, token).ConfigureAwait(false);
                    await Click(R, 2_000, token).ConfigureAwait(false);
                    await Click(A, 5_000, token).ConfigureAwait(false);

                    Log("Game saved, reading new details...");
                }
            }
        }

        private bool CheckIfPresent(PK8 el, List<PK8> list)
		{
            foreach(var pk in list)
                if (pk.EncryptionConstant == el.EncryptionConstant)
                    return true;
            return false;
		}

        private async Task<bool> LogPKMs(PK8? pk, CancellationToken token)
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

            Settings.AddCompletedScans();

            var legendary = Legal.Legends.Contains(pk.Species) || Legal.SubLegends.Contains(pk.Species);
            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, legendary ? "ow_legends" : "ow_encounters", pk);

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
    }
}
