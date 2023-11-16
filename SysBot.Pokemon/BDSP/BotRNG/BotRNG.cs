using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using SysBot.Base;
using System.Linq;

namespace SysBot.Pokemon
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class BDSPBotRNG : PokeRoutineExecutor8BS
    {
        private readonly PokeBotHub<PK8> Hub;
        private readonly RNGSettings Settings;
        private readonly IReadOnlyList<string> WantedNatures;
        private readonly RNG8b Calc;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        private readonly List<string> locations;

        /// <summary>
        /// Folder to dump received trade data to.
        /// </summary>
        /// <remarks>If null, will skip dumping.</remarks>
        private readonly IDumper DumpSetting;

        /// <summary>
        /// Synchronized start for multiple bots.
        /// </summary>
        public bool ShouldWaitAtBarrier { get; private set; }

        /// <summary>
        /// Tracks failed synchronized starts to attempt to re-sync.
        /// </summary>
        public int FailedBarrier { get; private set; }

        public BDSPBotRNG(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;

            Settings = hub.Config.BDSP_RNG;
            DumpSetting = hub.Config.Folder;
            Calc = new RNG8b();
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
            StopConditionSettings.ReadWantedNatures(Hub.Config.StopConditions, out WantedNatures);
            string res_data = Properties.Resources.text_bdsp_00000_en;
            res_data = res_data.Replace("\r", String.Empty);
            locations = res_data.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        // Cached offsets that stay the same per session.
        private ulong RNGOffset;
        private ulong PlayerLocation;
        private ulong DayTime;
        private GameTime GameTime;

        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                await InitializeHardware(Hub.Config.BDSP_RNG, token).ConfigureAwait(false);

                Log("Identifying trainer data of the host console.");
                var sav = await IdentifyTrainer(token).ConfigureAwait(false);

                await InitializeSessionOffsets(token).ConfigureAwait(false);

                Log($"Starting main {nameof(BDSPBotRNG)} loop.");
                Config.IterateNextRoutine();
                var task = Hub.Config.BDSP_RNG.Routine switch
                {
                    RNGRoutine.AutoRNG => AutoRNG(sav, token),
                    RNGRoutine.Generator => Generator(sav, token, Hub.Config.BDSP_RNG.GeneratorSettings.GeneratorVerbose, Hub.Config.BDSP_RNG.GeneratorSettings.GeneratorMaxResults),
                    RNGRoutine.DelayCalc => CalculateDelay(sav, token),
                    RNGRoutine.LogAdvances => TrackAdvances(sav, token),
                    RNGRoutine.CheckAvailablePKM => CheckAvailablePKM(sav, token),
                    _ => TrackAdvances(sav, token),
                };
                await task.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(BDSPBotRNG)} loop.");
            await CleanExit(Hub.Config.BDSP_RNG, token).ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            await ResetStick(CancellationToken.None).ConfigureAwait(false);
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task AutoRNG(SAV8BS sav, CancellationToken token)
        {
            bool found;
            if (Hub.Config.BDSP_RNG.AutoRNGSettings.AutoRNGMode is AutoRNGMode.AutoCalc)
            {
                if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed)
                {
                    //First calculations are made at game booted
                    GameTime = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                    while (!await AutoCalc(sav, token).ConfigureAwait(false))
                    {
                        var target = int.MaxValue;
                        //Calculate new targets at game boot
                        while ((Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0 && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue))
                        {
                            await RestartGameBDSP(false, token).ConfigureAwait(false);
                            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
                            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
                            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
                            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
                            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
                            Log("Calculating target...");
                            target = await CalculateTarget(xoro, sav, Hub.Config.BDSP_RNG.RNGType, Hub.Config.BDSP_RNG.WildMode, token).ConfigureAwait(false);
                            string msg = $"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}";
                            if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0 && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue)
                                msg = $"{msg}\nTarget above the limit settings. Rebooting.";
                            else
                                msg = $"{msg}\nTarget in: {target}";
                            Log(msg);
                        }

                        //If found a reasonable target, resume the game reboot and check for a boot error. Reinitialize offsets in case.
                        if (!await ResumeStart(Hub.Config, token).ConfigureAwait(false))
                            await InitializeSessionOffsets(token).ConfigureAwait(false);
                    }
                    found = true;
                }
                else
                    found = await AutoCalc(sav, token).ConfigureAwait(false);
            }
            else
                //ExternalCalc routine
                found = await TrackAdvances(sav, token, true).ConfigureAwait(false);

            if (found && Hub.Config.BDSP_RNG.RNGType is not RNGType.Egg)
            {
                if (Hub.Config.StopConditions.CaptureVideoClip)
                {
                    await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                    await PressAndHold(SwitchButton.CAPTURE, 2_000, 0, token).ConfigureAwait(false);
                }
                if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                    Log($"{Hub.Config.StopConditions.MatchFoundEchoMention} result found.");
            }
            return;
        }

        private async Task<bool> AutoCalc(SAV8BS sav, CancellationToken token)
        {
            var advances = 0;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var type = Hub.Config.BDSP_RNG.RNGType;
            var mode = Hub.Config.BDSP_RNG.WildMode;

            //Read RNG states from RAM
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);

            if (type is RNGType.Egg)
            {
                Log($"\nCannot calculate Egg generation. Pokefinder is the suggested tool to do calculations.\nPlease restart the bot changing your configurations.");
                return true;
            }

            if (actions.Count <= 0)
            {
                Log("\nYou must input at least One Action to trigger the encounter in the Hub settings.\n");
                return true;
            }

            if (Hub.Config.BDSP_RNG.WildMode is WildMode.None && Hub.Config.StopConditions.StopOnSpecies <= 0)
            {
                Log("\nPlease set a valid Species in the Stop Conditions settings.");
                return true;
            }

            //Read player details from RAM
            var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
            DayTime = await SwitchConnection.PointerAll(Offsets.DayTimePointer, token).ConfigureAwait(false);
            GameTime = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
            GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
            Log($"[{version}] - Route: {GetLocation(route)} ({route}) [{GameTime}]");

            Log($"Initial States: \n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}.");

            while (!token.IsCancellationRequested)
            {
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        Log("Calculating target...");
                        var target = await CalculateTarget(xoro, sav, type, mode, token).ConfigureAwait(false) - Hub.Config.BDSP_RNG.AutoRNGSettings.Delay;

                        if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue)
                        {
                            Log($"Target above the limit settings. Rebooting...");
                            return false;
                        }
                        if (target <= 0)
                            return false;

                        Log($"Target in {target} Advances.");

                        return await TrackAdvances(sav, token, true, target, xoro).ConfigureAwait(false);
                    }
                }
            }
            return false;
        }

        private async Task<bool> TrackAdvances(SAV8BS sav, CancellationToken token, bool auto = false, int aux_target = 0, Xorshift? ex_xoro = null)
		{
            var advances = 0;
            var target = 0;
            var to_hit = 0;
            var steps = 0;
            var dex_time = new Stopwatch();
            var print = Hub.Config.BDSP_RNG.AutoRNGSettings.LogAdvances;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var type = Hub.Config.BDSP_RNG.RNGType;
            var EggStepOffset = await SwitchConnection.PointerAll(Offsets.EggStepPointer, token).ConfigureAwait(false);
            var in_dex = false;
            var can_act = true;

            //Initialize RNG states
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = ex_xoro is null ? new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3) : ex_xoro;

            //ExternalCalc routine only
			if (auto && aux_target == 0)
			{
                if (actions.Count <= 0)
                {
                    Log("\nYou must input at least One Action to trigger the encounter in the Hub settings.\n");
                    return true;
                }
                Hub.Config.BDSP_RNG.AutoRNGSettings.Target = 0;
                //Show current RAM State so user can do external calculations
                Log($"\n\nCurrent states:\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nCalculate a target and set it in the Hub Settings. The routine will continue automatically once detected a target.");
                
                //Wait for user input for calculated target
                while (Hub.Config.BDSP_RNG.AutoRNGSettings.Target <= 0)
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                Log("CONTINUING...");
			}

            while (!token.IsCancellationRequested)
            {
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        //AutoRNG routine
                        if (auto)
						{
                            //If AutoCalc use aux_target, if ExternalCalc use settings target
							target = aux_target > 0 ? aux_target : Hub.Config.BDSP_RNG.AutoRNGSettings.Target;
                            to_hit = target - advances;

                            if (type is RNGType.Egg)
                            {
                                var step_tmp = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(EggStepOffset, 2, token).ConfigureAwait(false), 0);

                                //Possible egg generation is done after 180 steps
                                var step_until_generation = (0xB4 - step_tmp) == 0 ? 180 : (0xB4 - step_tmp);
                                if(steps != step_until_generation || print)
                                    Log($"Target in {to_hit} advances.\nSteps until possible egg generation: {step_until_generation}\n");
                            }
                            else
                            {
                                if (print)
                                    Log($"Target in {to_hit} advances.");
                            }

                            //If frame is hit or missed
                            if (target != 0 && to_hit <= 0)
                            {
                                if (in_dex)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await CloseDex(token).ConfigureAwait(false);
                                }

                                Log($"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}");

                                //Only check encounter if users did not decide to pause the game
                                if (actions.Last() is not SwitchButton.HOME)
                                {
                                    Log("Starting encounter...");
                                   
                                    var mode = Hub.Config.BDSP_RNG.CheckMode;
                                    var wild = Hub.Config.BDSP_RNG.WildMode;
                                    Stopwatch stopwatch = new();
                                    stopwatch.Start();
                                    await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                    var offset = GetDestOffset(mode, type);
                                    PB8? pk = null;
                                    uint seed = 0;
                                    do
                                    {
                                        //If reading mode is set to Seed, calculate the hit Pokémon
                                        if (mode is CheckMode.Seed)
                                        {
                                            seed = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(offset, token).ConfigureAwait(false), 4, token).ConfigureAwait(false), 0);
                                            var species = (int)Hub.Config.StopConditions.StopOnSpecies;
                                            pk = new PB8
                                            {
                                                TID = sav.TID,
                                                SID = sav.SID,
                                                OT_Name = sav.OT,
                                                Species = (species != 0) ? species : 482,
                                            };

                                            if (type is RNGType.Roamer)
                                                pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, seed);
                                            else if(type is RNGType.Egg)
											{
                                                if (seed > 0)
                                                    Log($"Egg generated. Egg seed is {seed:X8}.");
                                                else
                                                    Log("Egg not generated, target frame probably missed.");

                                                return true;
											}
                                        }

                                        //Otherwise read Pokémon directly from RAM
                                        else
                                        {
                                            seed = 1;
                                            pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                                        }

                                        if (type is RNGType.Gift or RNGType.Gift_3IV)
                                            await Click(SwitchButton.B, 0_050, token).ConfigureAwait(false);

                                    } while ((pk is null || seed == 0) && stopwatch.ElapsedMilliseconds < 10_000);

                                    if (pk is null)
                                        return false;

                                    Log($"\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                                    var success = HandleTarget(pk, true, true);
                                    if (!success)
                                    {                                        
                                        var mismatch = await CalculateMismatch(new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3), sav, type, wild, pk.EncryptionConstant, token).ConfigureAwait(false);
                                        if (mismatch is not null && advances == target)
                                        {
                                            Log("If target is missed, calculate a proper delay with DelayCalc mode and retry.");
                                            Log($"Calculated delay mismatch is {mismatch}.");
                                        }
                                        else
                                            Log("Target frame missed.");
                                    }
                                    return success;
                                }
                                else
                                {
                                    await Click(SwitchButton.L, 0_100, token).ConfigureAwait(false);
                                    await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                    Log("Game paused.");
                                    return true;
                                }
                            }
                            //Use Pokédex scrolling to fast advance frames
                            else if (Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil > 0 && target-advances > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil+50)
                            {
                                if (!in_dex && target-advances > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil+1100)
                                {
                                    await Task.Delay(2_000, token).ConfigureAwait(false);
                                    await OpenDex(Hub.Config.Timings.KeypressTime, token).ConfigureAwait(false);
                                    dex_time.Restart();
                                    in_dex = true;
                                }
                                if (in_dex)
                                {
                                    //Faster advance, stick to left/right
                                    if (target - (advances + 400) > 7000)
                                    {
                                        //ReOpen dex time to time to mantain advancing performance
                                        if (dex_time.ElapsedMilliseconds > 185_000)
                                        {
                                            await ResetStick(token).ConfigureAwait(false);
                                            await ReOpenDex(Hub.Config.Timings.KeypressTime, token).ConfigureAwait(false);
                                            dex_time.Restart();
                                        }
                                        else
                                        {
                                            await ResetStick(token).ConfigureAwait(false);
                                            await SetStick(SwitchStick.LEFT, 30_000, 0, 2_000, token).ConfigureAwait(false);
                                        }
                                    }
                                    //Middle advance, stick to UP/DOWN
                                    else if (target - (advances + 400) > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil)
                                    {
                                        await ResetStick(token).ConfigureAwait(false);
                                        await SetStick(SwitchStick.LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                                    }
                                    //Slow advance, single button
                                    else
                                    {
                                        await ResetStick(token).ConfigureAwait(false);
                                        await Click(SwitchButton.DUP, 0_500, token).ConfigureAwait(false);
                                    }
                                }
                            }
                            else
                            {
                                //Perform actions to reach the last input before Pokémon generation
                                if (in_dex)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await CloseDex(token).ConfigureAwait(false);
                                    in_dex = false;
                                }
                                if (can_act && actions.Count > 1)
                                {
                                    await Task.Delay(0_700).ConfigureAwait(false);
                                    if (actions.Count > 1)
                                    {
                                        Log("Perfoming actions...");
                                        await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                    }
                                    can_act = false;
                                }
                            }
                        }
                        else
                            Log($"\nAdvance {advances}\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\n");
                    }
                }
            }
            return false;
        }

        async Task<int?> CalculateMismatch(Xorshift xoro, SAV8BS sav, RNGType type, WildMode mode, uint hit_ec, CancellationToken token)
        {
            var delay = Hub.Config.BDSP_RNG.AutoRNGSettings.Delay * 2;
            var range = delay > 100 ? delay : 100;
            var states = xoro.GetU32State();
            var species = (int)Hub.Config.StopConditions.StopOnSpecies;
            var events = Hub.Config.BDSP_RNG.Event;
            var rng = new Xorshift(states[0], states[1], states[2], states[3]);
            var target = await CalculateTarget(xoro, sav, type, mode, token).ConfigureAwait(false);
            int[]? unownForms = null;
            var advances = 0;
            List<int>? slots = null;

            if (mode is not WildMode.None)
            {
                var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
                GameTime = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
                slots = GetEncounterSlots(version, route, GameTime, mode);
                
                if (GetLocation(route).Contains("Solaceon Ruins"))
                    unownForms = GetUnownForms(route);
            }

            var pk = new PB8
            {
                TID = sav.TID,
                SID = sav.SID,
                OT_Name = sav.OT,
                Species = (species != 0) ? species : 482,
            };

            do
            {
                if (type is RNGType.Roamer)
                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, rng.Next());
                else
                {
                    states = rng.GetU32State();
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), mode, slots, events, unownForms);
                    rng.Next();
                }
                advances++;
            } while (pk.EncryptionConstant != hit_ec && advances <= range);

            if (advances >= range || target >= range)
                return null;
            return (advances - target);
        }

        async Task<int> CalculateTarget(Xorshift xoro, SAV8BS sav, RNGType type, WildMode mode, CancellationToken token)
        {
            int advances = 0;
            uint[] states = xoro.GetU32State();
            Xorshift rng = new(states[0], states[1], states[2], states[3]);
            List<int>? slots = null;
            int[]? unownForms = null;
            int species = (int)Hub.Config.StopConditions.StopOnSpecies;
            var events = Hub.Config.BDSP_RNG.Event;

            if (mode is not WildMode.None)
            {
                var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
                var tmp = (await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                if (tmp >= 0 && tmp <= 4)
                    GameTime = (GameTime)tmp;
                GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
                slots = GetEncounterSlots(version, route, GameTime, mode);

                if (GetLocation(route).Contains("Solaceon Ruins"))
                    unownForms = GetUnownForms(route);
            }

            var pk = new PB8
            {
                TID = sav.TID,
                SID = sav.SID,
                OT_Name = sav.OT,
                Species = (species != 0) ? species : 482,
            };

            do
            {
                if (type is RNGType.Roamer)
                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, rng.Next());
                else
                {
                    states = rng.GetU32State();
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), mode, slots, events, unownForms);
                    rng.Next();
                }
                advances++;
            } while (!HandleTarget(pk, false, false) && (advances - Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue < Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue && Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0));
            if (Hub.Config.BDSP_RNG.RNGType is RNGType.MysteryGift && Hub.Config.BDSP_RNG.Event is PokeEvents.KorRegigigas)
                advances--;
            return advances;
        }

        private async Task CalculateDelay(SAV8BS sav, CancellationToken token)
		{
            var action = Hub.Config.BDSP_RNG.DelayCalcSettings.Action;
            var dest = Hub.Config.BDSP_RNG.CheckMode;
            var type = Hub.Config.BDSP_RNG.RNGType;
            var advances = 0;
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
            var calculatedlist = await Generator(sav, token, false, 500, xoro).ConfigureAwait(false);
            int used;

            //Log($"Initial State:\n[S0]: {tmpS0:X8}, [S1]: {tmpS1:X8}\n[S2]: {tmpS2:X8}, [S3]: {tmpS3:X8}\n");

            while (!token.IsCancellationRequested)
			{
                tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                var ramS0 = BitConverter.ToUInt32(tmpRamState, 0);
                var ramS1 = BitConverter.ToUInt32(tmpRamState, 4);
                var ramS2 = BitConverter.ToUInt32(tmpRamState, 8);
                var ramS3 = BitConverter.ToUInt32(tmpRamState, 12);

                while (ramS0 != tmpS0 || ramS1 != tmpS1 || ramS2 != tmpS2 || ramS3 != tmpS3)
                {
                    xoro.Next();
                    tmpS0 = xoro.GetU32State()[0];
                    tmpS1 = xoro.GetU32State()[1];
                    tmpS2 = xoro.GetU32State()[2];
                    tmpS3 = xoro.GetU32State()[3];
                    advances++;

                    if (ramS0 == tmpS0 && ramS1 == tmpS1 && ramS2 == tmpS2 && ramS3 == tmpS3)
                    {
                        await Click(action, 0_100, token).ConfigureAwait(false);
                        used = advances;
                        PB8? pk;
                        uint seed = 0;
                        //Log($"Waiting for pokemon...");
                        var offset = GetDestOffset(dest, type);
                        do
                        {
                            if (dest is CheckMode.Seed)
                            {
                                var species = (int)Hub.Config.StopConditions.StopOnSpecies;
                                pk = new PB8
                                {
                                    TID = sav.TID,
                                    SID = sav.SID,
                                    OT_Name = sav.OT,
                                    Species = (species != 0) ? species : 482,
                                };
                                seed = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(await SwitchConnection.PointerAll(offset, token).ConfigureAwait(false), 4, token).ConfigureAwait(false), 0);
                                pk = Calc.CalculateFromSeed(pk, Shiny.Random, RNGType.Roamer, seed);
                            }
                            else
                            {
                                seed = 1;
                                pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                            }

                            if (type is RNGType.Gift or RNGType.Gift_3IV)
                                await Click(SwitchButton.B, 0_050, token).ConfigureAwait(false);
                        } while (pk is null || seed == 0);

                        var hit = pk.EncryptionConstant;

                        Log($"\nFinal State:\n[S0]: {tmpS0:X8}, [S1]: {tmpS1:X8}\n[S2]: {tmpS2:X8}, [S3]: {tmpS3:X8}\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");

                        var result = 0;
                        var i = 0;

                        foreach (var item in calculatedlist)
                        {
                            i++;
                            if (item.EncryptionConstant == hit)
                            {
                                result = i;
                                break;
                            }
                        }
                        //Log($"Result: {result}, used: {used}, difference: {result - used}");
                        var delay = (result-used) != -1 ? (result-used) : 0;
                        Log($"\nCalculated delay is {delay}.\n");

                        return;
                    }
				} 
            }
        }

        private async Task<List<PB8>> Generator(SAV8BS sav, CancellationToken token, bool verbose, int maxadvances, Xorshift? xoro = null)
		{
            var type = Hub.Config.BDSP_RNG.RNGType;
            var mode = Hub.Config.BDSP_RNG.WildMode;
            var events = Hub.Config.BDSP_RNG.Event;
            var isroutine = xoro == null;
            var result = new List<PB8>();
            List<int>? encounterslots = null;
            int[]? unownForms = null;
            int advance;
            uint initial_s0f;
            uint initial_s1f;
            uint initial_s2f;
            uint initial_s3f;

            if (isroutine)
            {
                var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);

                initial_s0f = BitConverter.ToUInt32(tmpRamState, 0);
                initial_s1f = BitConverter.ToUInt32(tmpRamState, 4);
                initial_s2f = BitConverter.ToUInt32(tmpRamState, 8);
                initial_s3f = BitConverter.ToUInt32(tmpRamState, 12);

                Log($"Initial states:\n[S0] {initial_s0f:X8}, [S1] {initial_s1f:X8}\n[S2] {initial_s2f:X8}, [S3] {initial_s3f:X8}\n");
            } 
            else
			{
                initial_s0f = (xoro != null) ? xoro.GetU32State()[0] : 0;
                initial_s1f = (xoro != null) ? xoro.GetU32State()[1] : 0;
                initial_s2f = (xoro != null) ? xoro.GetU32State()[2] : 0;
                initial_s3f = (xoro != null) ? xoro.GetU32State()[3] : 0;
            }

            if (mode is not WildMode.None)
            {
                var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
                var time = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
                encounterslots = GetEncounterSlots(version, route, time, mode);

                if (GetLocation(route).Contains("Solaceon Ruins"))
                    unownForms = GetUnownForms(route);

                if (isroutine)
                {
                    Log($"({version}) {GetLocation(route)} ({route}) [{time}]");
                    Log("Available mons:");
                    if (encounterslots.Count > 0)
                    {
                        var i = 0;
                        foreach (var mon in encounterslots)
                        {
                            if (unownForms is null || unownForms.Length == 0)
                                Log($"[{i}] {(Species)mon}");
                            else
                            {
                                var formstr = " ";
                                foreach (var form in unownForms!)
                                    formstr = $"{formstr}{form} ";
                                Log($"[{i}] {(Species)mon}-[{formstr}]");
                            }

                            i++;
                        }
                    }
                    else
                    {
                        Log("None");
                    }
                }
            }

            var rng = new Xorshift(initial_s0f, initial_s1f, initial_s2f, initial_s3f);

            for (advance = 0; advance < maxadvances; advance++)
            {
                uint[] states = rng.GetU32State();
				var pk = new PB8
				{
					TID = sav.TID,
					SID = sav.SID,
					OT_Name = sav.OT,
					Species = (int)Hub.Config.StopConditions.StopOnSpecies
				};

                if (type is RNGType.Egg)
                {
                    Log("Cannot calculate Egg generation. Pokefinder is the suggested tool to do calculations.");
                    return result;
                }
                else if (type is RNGType.Roamer)
                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, rng.Next());
                else
                {
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), mode, encounterslots, events, unownForms);
                    rng.Next();
                }

                result.Add(pk);

                var msg = $"\nAdvances: {advance}\n[S0] {states[0]:X8}, [S1] {states[1]:X8}\n[S2] {states[2]:X8}, [S3] {states[3]:X8}";
                if (Hub.Config.BDSP_RNG.WildMode is not WildMode.None)
                    msg = $"{msg}\nSpecies: {(Species)pk.Species}-{(pk.PersonalInfo.FormCount > 0 ? $"[{pk.Form}]" : "")} (EncounterSlot: {pk.Move1})";
                msg = $"{msg}\n{GetString(pk)}";
                if (verbose == true)
                    Log($"{Environment.NewLine}{msg}");

                bool found = HandleTarget(pk, false, isroutine);
                if (token.IsCancellationRequested || (found && isroutine))
                {
					if (found)
					{
                        msg = $"Details for found target:\n{msg}";
                        Log($"{Environment.NewLine}{msg}");
					}
                    return result;
                }
            }
            if(isroutine)
                Log($"Target not found in {advance} attempts.");
            return result;
        }

        private async Task CheckAvailablePKM(SAV8BS sav, CancellationToken token)
        {
            var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
            var time = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
            GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;

            var mode = Hub.Config.BDSP_RNG.WildMode == WildMode.None ? WildMode.Grass_or_Cave : Hub.Config.BDSP_RNG.WildMode;

            var slots = GetEncounterSlots(version, route, time, mode);
            var unownForms = GetLocation(route).Contains("Solaceon Ruins") ? GetUnownForms(route) : null;

            Log($"({version}) {GetLocation(route)} ({route}) [{time}]");
            Log($"Available mons for {mode} encounters:");
            if (slots.Count > 0)
            {
                var i = 0;
                foreach (var slot in slots)
                {
                    if (unownForms is null || unownForms.Length == 0)
                        Log($"[{i}] {(Species)slot}");
                    else
                    {
                        var formstr = " ";
                        foreach (var form in unownForms!)
                            formstr = $"{formstr}{form} ";
                        Log($"[{i}] {(Species)slot}-[{formstr}]");
                    }
                    i++;
                }
            }
            else
            {
                Log("None");
            }

            return;
        }

        private bool HandleTarget(PB8 pk, bool encounter, bool dump)
        {
            //Initialize a species
            if (pk.Species == 0)
                pk.Species = 1;

            if (!StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, WantedNatures, null))
                return false;

            if (dump)
            {
                if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder) && encounter)
                    DumpPokemon(DumpSetting.DumpFolder, "BDSP_RNG_Encounters", pk);
                else
                    DumpPokemon(DumpSetting.DumpFolder, "BDSP_RNG_Layout", pk);
            }

            if (encounter)
                Settings.AddCompletedRNGs();

            return true;
        }

        private string GetLocation(int location_id)
        {
            return (this.locations.ElementAt(location_id));
        }

        // These don't change per session and we access them frequently, so set these each time we start.
        private async Task InitializeSessionOffsets(CancellationToken token)
        {
            Log("Caching session offsets...");
            RNGOffset = await SwitchConnection.PointerAll(Offsets.MainRNGState, token).ConfigureAwait(false);
            PlayerLocation = await SwitchConnection.PointerAll(Offsets.LocationPointer, token).ConfigureAwait(false);
            DayTime = await SwitchConnection.PointerAll(Offsets.DayTimePointer, token).ConfigureAwait(false);
            //Click useless key to actually initialize simulated controller
            await Click(SwitchButton.L, 0_050, token).ConfigureAwait(false);
        }

        private async Task RestartGameBDSP(bool untiloverworld, CancellationToken token)
        {
            await ReOpenGame(untiloverworld, Hub.Config, token).ConfigureAwait(false);
            await InitializeSessionOffsets(token).ConfigureAwait(false);
        }

        protected async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(SwitchStick.RIGHT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
            await SetStick(SwitchStick.LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }

        private IReadOnlyList<long> GetDestOffset(CheckMode mode, RNGType type = RNGType.Custom)
        {
            return mode switch
            {
                CheckMode.TeamSlot1 => Offsets.PartyStartPokemonPointer,
                CheckMode.TeamSlot2 => Offsets.PartySlot2PokemonPointer,
                CheckMode.Box1Slot1 => Offsets.BoxStartPokemonPointer,
                CheckMode.Encounter => Offsets.OpponentPokemonPointer,
                CheckMode.Seed => type is RNGType.Egg ? Offsets.EggSeedPointer : GetRoamerOffset(),
                _ => Offsets.OpponentPokemonPointer,
            };
        }

        private IReadOnlyList<long> GetRoamerOffset()
		{
            if ((int)Hub.Config.StopConditions.StopOnSpecies == 481)
                return Offsets.R1_SeedPointer;
            else
                return Offsets.R2_SeedPointer;
		}
    }
}