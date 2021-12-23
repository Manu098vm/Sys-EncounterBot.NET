﻿using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.Net.Sockets;
using System.Linq;



namespace SysBot.Pokemon
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class BDSPBotRNG : PokeRoutineExecutor8BS, ICountBot
    {
        private readonly PokeBotHub<PK8> Hub;
        private readonly RNGSettings Settings;
        private readonly RNG8b Calc;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        private readonly List<string> locations;

        public ICountSettings Counts => Settings;

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
            string res_data = Properties.Resources.text_bdsp_00000_en;
            locations = res_data.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        // Cached offsets that stay the same per session.
        private ulong RNGOffset;
        private ulong PlayerLocation;
        private ulong DayTime;

        public override async Task MainLoop(CancellationToken token)
        {
            try
            {
                await InitializeHardware(Hub.Config.BDSP_RNG, token).ConfigureAwait(false);

                Log("Identifying trainer data of the host console.");
                var sav = await IdentifyTrainer(token).ConfigureAwait(false);

                await InitializeSessionOffsets(token).ConfigureAwait(false);

                Log($"Starting main {nameof(BDSPBotRNG)} loop.");
                await InnerLoop(sav, token).ConfigureAwait(false);
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

        private async Task InnerLoop(SAV8BS sav, CancellationToken token)
        {
                Config.IterateNextRoutine();
                var task  = Hub.Config.BDSP_RNG.Routine switch
                {
                    RNGRoutine.AutoRNG => AutoRNG(sav, token),
                    RNGRoutine.Generator => Generator(sav, token, Hub.Config.BDSP_RNG.GeneratorSettings.GeneratorVerbose, Hub.Config.BDSP_RNG.GeneratorSettings.GeneratorMaxResults),
                    RNGRoutine.DelayCalc => CalculateDelay(sav, token),
                    RNGRoutine.LogAdvances => TrackAdvances(sav, token),
                    RNGRoutine.TEST => Test(sav, token),
                    _ => TrackAdvances(sav, token),
                };
                try
                {
                    await task.ConfigureAwait(false);
                }
                catch (SocketException e)
                {
                    Log(e.Message);
                    Connection.Reset();
                }
        }

        private async Task Test(SAV8BS sav, CancellationToken token)
		{
            GameVersion version = 0;
            var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false),0);
            var time = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
            var mode = Hub.Config.BDSP_RNG.WildMode;
            if (Offsets is PokeDataOffsetsBS_BD)
                version = GameVersion.BD;
            else if (Offsets is PokeDataOffsetsBS_SP)
                version = GameVersion.SP;

            Log($"({version}) {GetLocation(route)} ({route}) [{time}]");

            var mons = GetEncounterSlots(version, route, time, mode);
            Log("Available mons:");
            var i = 0;
            foreach (var mon in mons)
            {
                Log($"[{i}] {(Species)mon}");
                i++;
            }

            Log("Actions:");
            List<SwitchButton> actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            foreach (var button in actions)
                Log($"{button}");
            Log("Performing actions...");
            await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
            await Click(actions.Last(), 0_050, token).ConfigureAwait(false);
        }

        private async Task AutoRNG(SAV8BS sav, CancellationToken token)
        {
            if (Hub.Config.BDSP_RNG.AutoRNGSettings.AutoRNGMode is AutoRNGMode.AutoCalc)
            {
                bool found;
                if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed)
                {
                    while (!await AutoCalc(sav, token).ConfigureAwait(false))
                    {
                        var target = int.MaxValue;
                        var boot_pass = true;
                        while ((Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0 && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue) || !boot_pass)
                        {
                            await RestartGameBDSP(false, token).ConfigureAwait(false);
                            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
                            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
                            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
                            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
                            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
                            target = await CalculateTarget(xoro, sav, Hub.Config.BDSP_RNG.RNGType, Hub.Config.BDSP_RNG.WildMode, token).ConfigureAwait(false);
                            if (tmpS1 == tmpS3)
                            {
                                Log("Boot failed. Retry...");
                                boot_pass = false;
                            }
                            else
                            {
                                string msg = $"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}";
                                if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0 && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue)
                                    msg = $"{msg}\nTarget above the limit settings. Rebooting.";
                                else
                                    msg = $"{msg}\nTarget in: {target}";
                                Log(msg);
                                boot_pass = true;
                            }
                        }
                        await ResumeStart(Hub.Config, token).ConfigureAwait(false);
                    }
                    found = true;
                }
                else
                    found = await AutoCalc(sav, token).ConfigureAwait(false);

                if (found)
                {
                    if (Hub.Config.StopConditions.CaptureVideoClip)
                    {
                        await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                        await PressAndHold(SwitchButton.CAPTURE, 2_000, 0, token).ConfigureAwait(false);
                    }
                    if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                        Log($"{Hub.Config.StopConditions.MatchFoundEchoMention} result found.");
                }
            }
            else
                await TrackAdvances(sav, token, true).ConfigureAwait(false);
            return;
        }

        private async Task<bool> TrackAdvances(SAV8BS sav, CancellationToken token, bool auto = false)
		{
            var advances = 0;
            var target = 0;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
            var in_dex = false;
            var can_act = true;

			if (auto)
			{
                if (actions.Count <= 0)
                {
                    Log("\nYou must input at least One Action to trigger the encounter in the Hub settings.\n");
                    return true;
                }
                Hub.Config.BDSP_RNG.AutoRNGSettings.Target = 0;
                Log($"\n\nCurrent states:\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nCalculate a target and set it in the Hub Settings. The routine will continue automatically once detected a target.");
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
                        if (auto)
						{
							target = Hub.Config.BDSP_RNG.AutoRNGSettings.Target;
							if (target != 0 && advances >= target)
                            {
                                if (in_dex)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await CloseDex(token).ConfigureAwait(false);
                                }

                                System.Diagnostics.Stopwatch stopwatch = new();
                                stopwatch.Start();
                                await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                                Log($"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nStarting encounter...");
                                var offset = GetDestOffset(Hub.Config.BDSP_RNG.CheckMode);
                                PB8? pk;
                                do
                                {
                                    pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                                } while (pk is null && stopwatch.ElapsedMilliseconds < 5_000);
                                if (pk is null)
                                    return false;
                                Log($"\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                                return StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, null);
                            }
                            else if (target != 0 && advances > target)
                            {
                                if (in_dex)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await CloseDex(token).ConfigureAwait(false);
                                }
                                Log("Target frame missed. Probably a noisy area. New target calculation needed.");
                                Hub.Config.BDSP_RNG.AutoRNGSettings.Target = 0;
                                return false;
                            }
                            else if (Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil > 0 && target-advances > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil)
                            {
                                if (!in_dex)
                                {
                                    await Task.Delay(2_000, token).ConfigureAwait(false);
                                    await OpenDex(token).ConfigureAwait(false);
                                    in_dex = true;
                                }

                                if (target - advances - 400 > 7000)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await SetStick(SwitchStick.LEFT, 30_000, 0, 2_000, token).ConfigureAwait(false);
                                }
                                else if (target - advances - 400 > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await SetStick(SwitchStick.LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                                }
                                else
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await Click(SwitchButton.DUP, 0_500, token).ConfigureAwait(false);
                                }
                            }
                            else if (in_dex)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await CloseDex(token).ConfigureAwait(false);
                                in_dex = false;
                                if (target > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil && can_act)
                                {
                                    await Task.Delay(0_700).ConfigureAwait(false);
                                    Log("Perfoming actions...");
                                    await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                    can_act = false;
                                }
                            }
                            else if(can_act)
                            {
                                await Task.Delay(0_700).ConfigureAwait(false);
                                Log("Perfoming actions...");
                                await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                can_act = false;
                            }
                            else
							{
                                Log($"Target in {target-advances} Advances.");
                            }
                        }
                        else
                            Log($"\nAdvance {advances}\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\n");
                    }
                }
            }
            return false;
        }

        private async Task<bool> AutoCalc(SAV8BS sav, CancellationToken token)
		{
            var advances = 0;
            var print = true;
            var actions = ParseActions(Hub.Config.BDSP_RNG.AutoRNGSettings.Actions);
            var type = Hub.Config.BDSP_RNG.RNGType;
            var mode = Hub.Config.BDSP_RNG.WildMode;
            var checkmode = Hub.Config.BDSP_RNG.CheckMode;
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
            var target = Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue < 1000000 ? await CalculateTarget(xoro, sav, type, mode, token).ConfigureAwait(false) - Hub.Config.BDSP_RNG.AutoRNGSettings.Delay : 0;
            var d0_safe = Hub.Config.BDSP_RNG.AutoRNGSettings.Delay != 0;
            var modifier = Hub.Config.BDSP_RNG.RNGType is RNGType.MysteryGift ? 1 : 0;
            var check = false;
            var can_act = true;
            var in_dex = false;
            var force_check = false;
            int old_target;
            PB8? pk;
    
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

            var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
            var time = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
            GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
            Log($"[{version}] - Route: {GetLocation(route)} ({route}) [{time}]");

            Log($"Initial States: \n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nTarget in {target}.");

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
                        old_target = target;
                        target = await CalculateTarget(xoro, sav, type, mode, token).ConfigureAwait(false) - Hub.Config.BDSP_RNG.AutoRNGSettings.Delay - modifier;

                        if (check && old_target < target)
                        {
                            if (in_dex)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await CloseDex(token).ConfigureAwait(false);
                                in_dex = false;
                            }
                            else if (d0_safe)
                            {
                                Log("Traget frame missed.");
                                return false;
                            }
                            else
                                force_check = true;
                        }

                        if (target == 0 || force_check)
                        {
                            if (in_dex)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await CloseDex(token).ConfigureAwait(false);
                            }
                            
                            await Click(actions.Last(), 0_100, token).ConfigureAwait(false);
                            System.Diagnostics.Stopwatch stopwatch = new();
                            stopwatch.Start();
                            Log($"\n[S0] {tmpS0:X8}, [S1] {tmpS1:X8}\n[S2] {tmpS2:X8}, [S3] {tmpS3:X8}\nStarting encounter...");
                            var offset = GetDestOffset(checkmode);
                            pk = null;
                            uint seed = 0;
                            do
                            {
                                if (checkmode is CheckMode.Seed && type is RNGType.Roamer)
                                {
                                    var species = (int)Hub.Config.StopConditions.StopOnSpecies;
                                    pk = new PB8
                                    {
                                        TID = sav.TID,
                                        SID = sav.SID,
                                        OT_Name = sav.OT,
                                        Species = (species != 0) ? species : 482,
                                    };
                                    seed = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(await PointerAll(offset, token).ConfigureAwait(false), 4, token).ConfigureAwait(false), 0);
                                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, seed);
                                }
                                else
                                {
                                    seed = 1;
                                    pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                                }
                            } while ((pk is null || seed == 0) && stopwatch.ElapsedMilliseconds < 5_000);
                            if (pk is null)
                                return false;

                            Log($"\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                            Log("If target is missed, calculate a proper delay with DelayCalc mode and retry.");
                            return StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, null);
                        }
                        else if (Hub.Config.BDSP_RNG.AutoRNGSettings.RebootIfFailed && target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue)
                        {
                            Log($"Target above the limit settings. Rebooting...");
                            return false;
                        }
                        else if (Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil > 0 && target > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil)
                        {
                            if (!in_dex)
                            {
                                await Task.Delay(1_000, token).ConfigureAwait(false);
                                await OpenDex(token).ConfigureAwait(false);
                                in_dex = true;
                            }

                            if (target - 400 > 7000)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await SetStick(SwitchStick.LEFT, 30_000, 0, 2_000, token).ConfigureAwait(false);
                            }
                            else if (target - 1000 > Hub.Config.BDSP_RNG.AutoRNGSettings.ScrollDexUntil)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await SetStick(SwitchStick.LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                            }
                            else
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await Click(SwitchButton.DUP, 0_500, token).ConfigureAwait(false);
                            }
                        }
                        else if (in_dex)
                        {
                            await ResetStick(token).ConfigureAwait(false);
                            await CloseDex(token).ConfigureAwait(false);
                            in_dex = false;
                            if (target > Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue && can_act)
							{
                                await Task.Delay(0_700).ConfigureAwait(false);
                                Log("Perfoming Actions");
                                await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                can_act = false;
                            }
                        }
                        else
                        {
							if (can_act)
							{
                                await Task.Delay(0_700).ConfigureAwait(false);
                                Log("Perfoming Actions");
                                await DoActions(actions, Hub.Config.BDSP_RNG.AutoRNGSettings.ActionTimings, token).ConfigureAwait(false);
                                can_act = false;
                            }
                            print = true;
                        }

						if (print)
						{
                            Log($"Target in {target} Advances.");
                            print = false;
						}

                        check = true;
                    }
                }
            }
            return false;
        }

        async Task<int> CalculateTarget(Xorshift xoro, SAV8BS sav, RNGType type, WildMode mode, CancellationToken token)
		{
            int advances = 0;
            uint[] states = xoro.GetU32State();
            Xorshift rng = new(states[0], states[1], states[2], states[3]);
            List<int>? slots = null;
            int species = (int)Hub.Config.StopConditions.StopOnSpecies;

            if (mode is not WildMode.None)
            {
                var route = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(PlayerLocation, 2, token).ConfigureAwait(false), 0);
                var time = (GameTime)(await SwitchConnection.ReadBytesAbsoluteAsync(DayTime, 1, token).ConfigureAwait(false))[0];
                GameVersion version = (Offsets is PokeDataOffsetsBS_BD) ? GameVersion.BD : GameVersion.SP;
                //Log($"Route: {GetLocation(route)} ({route}) [{time}]");
                slots = GetEncounterSlots(version, route, time, mode);
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
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), mode, slots);
                    rng.Next();
                }
                advances++;
            } while (!HandleTarget(pk, false) && (advances - Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue < Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue && Hub.Config.BDSP_RNG.AutoRNGSettings.RebootValue > 0));
            return advances;
        }

        private async Task CalculateDelay(SAV8BS sav, CancellationToken token)
		{
            var action = Hub.Config.BDSP_RNG.DelayCalcSettings.Action;
            var dest = Hub.Config.BDSP_RNG.CheckMode;
            var advances = 0;
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
            var calculatedlist = await Generator(sav, token, false, 500, xoro).ConfigureAwait(false);
            int used;

            Log($"Initial State:\n[S0]: {tmpS0:X8}, [S1]: {tmpS1:X8}\n[S2]: {tmpS2:X8}, [S3]: {tmpS3:X8}\n");

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
                        Log($"Waiting for pokemon...");
                        var offset = GetDestOffset(dest);
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
                                seed = BitConverter.ToUInt32(await SwitchConnection.ReadBytesAbsoluteAsync(await PointerAll(offset, token).ConfigureAwait(false), 4, token).ConfigureAwait(false), 0);
                                pk = Calc.CalculateFromSeed(pk, Shiny.Random, RNGType.Roamer, seed);
                            }
                            else
                            {
                                seed = 1;
                                pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                            }
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
                        //Log($"Result: {result}, used: {used}");
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
            var isroutine = xoro == null;
            var result = new List<PB8>();
            List<int>? encounterslots = null;
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
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]), mode, encounterslots);
                    rng.Next();
                }

                result.Add(pk);

                var msg = $"\nAdvances: {advance}\n[S0] {states[0]:X8}, [S1] {states[1]:X8}\n[S2] {states[2]:X8}, [S3] {states[3]:X8}";
                if (Hub.Config.BDSP_RNG.WildMode is not WildMode.None)
                    msg = $"{msg}\nSpecies: {(Species)pk.Species} (EncounterSlot: {pk.Move1})";
                msg = $"{msg}\n{GetString(pk)}";
                if (verbose == true)
                    Log($"{Environment.NewLine}{msg}");

                bool found = HandleTarget(pk, isroutine);
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

        bool HandleTarget(PB8 pk, bool dump)
        {
            //Initialize random species
            if (pk.Species == 0)
                pk.Species = 1;

            if (!StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, null))
                return false;

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder) && dump)
                DumpPokemon(DumpSetting.DumpFolder, "BDSP_Layout_PKM", pk);

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

        private IReadOnlyList<long> GetDestOffset(CheckMode mode)
        {
            return mode switch
            {
                CheckMode.Team => Offsets.PartyStartPokemonPointer,
                CheckMode.Box => Offsets.BoxStartPokemonPointer,
                CheckMode.Wild => Offsets.OpponentPokemonPointer,
                CheckMode.Seed => Offsets.RoamerSeedPointer,
                _ => Offsets.OpponentPokemonPointer,
            };
        }
    }
}