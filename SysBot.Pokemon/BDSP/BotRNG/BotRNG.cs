﻿using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.Net.Sockets;

namespace SysBot.Pokemon
{
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class BDSPBotRNG : PokeRoutineExecutor8BS, ICountBot
    {
        private readonly PokeBotHub<PK8> Hub;
        private readonly RNGSettings Settings;
        private readonly BotCompleteCounts Count;
        private readonly RNG8b Calc;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;

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
            Count = Hub.Counts;
            DumpSetting = hub.Config.Folder;
            Calc = new RNG8b();
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
        }

        // Cached offsets that stay the same per session.
        private ulong RNGOffset;

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
                    RNGRoutine.Generator => Generator(sav, token, Hub.Config.BDSP_RNG.GeneratorVerbose, Hub.Config.BDSP_RNG.GeneratorMaxResults),
                    RNGRoutine.DelayCalc => CalculateDelay(sav, token),
                    RNGRoutine.LogAdvances => LogAdvances(sav, token),
                    _ => LogAdvances(sav, token),
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

        private async Task AutoRNG(SAV8BS sav, CancellationToken token)
        {
            if (Hub.Config.BDSP_RNG.AutoRNGMode is AutoRNGMode.AutoCalc)
            {
                while(!await AutoCalc(sav, token).ConfigureAwait(false))
				{
                    var target = int.MaxValue;
                    while (Hub.Config.BDSP_RNG.RebootIf > 0 && target > Hub.Config.BDSP_RNG.RebootIf)
                    {
                        await RestartGameBDSP(false, token).ConfigureAwait(false);
                        var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
                        var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
                        var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
                        var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
                        var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
                        var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
                        target = CalculateTarget(xoro, sav, Hub.Config.BDSP_RNG.RNGType);
                        Log($"\n[S0] {tmpS0:X}, [S1] {tmpS1:X}\n[S2] {tmpS2:X}, [S3] {tmpS3:X}\nTarget in: {target}");
                    }
                    await ResumeStart(Hub.Config, token).ConfigureAwait(false);
                }
            }
            else
                await LogAdvances(sav, token, true).ConfigureAwait(false);
            return;
        }

        private async Task LogAdvances(SAV8BS sav, CancellationToken token, bool auto = false)
		{
            var advances = 0;
            var target = 0;
            var actions = ParseActions();
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
                    return;
                }
                Log($"\n\nCurrent states:\n[S0] 0x{tmpS0:X} [S1] 0x{tmpS1:X}\nCalculate a target and set it in the Hub Settings. The routine will continue automatically once detected a target.");
                while(Hub.Config.BDSP_RNG.Target <= 0)
                    await Task.Delay(1_000, token).ConfigureAwait(false); 
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
                            target = Hub.Config.BDSP_RNG.Target;
                            if (target != 0 && advances == target)
                            {
                                await Click(actions[0], 0_100, token).ConfigureAwait(false);
                                Log("Starting encounter...");
                                var offset = GetDestOffset(Hub.Config.BDSP_RNG.CheckMode);
                                PB8? pk;
                                do
                                {
                                    pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                                } while (pk is null);
                                Log($"\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                                Log("If target is missed, calculate a proper delay with CalculateDelay mode and retry.");
                                return;
                            }
                            else if (target != 0 && advances > target)
                            {
                                Log("Target frame missed. Probably a noisy area. New target calculation needed.");
                                Hub.Config.BDSP_RNG.Target = 0;
                                advances = 0;
                                Log($"\n\nCurrent states:\n[S0] 0x{tmpS0:X} [S1] 0x{tmpS1:X}\nCalculate a target and set it in the Hub Settings. The routine will continue automatically once detected a target.");
                                while (Hub.Config.BDSP_RNG.Target == 0)
                                    await Task.Delay(1_000, token).ConfigureAwait(false);
                            }
                            else if (Hub.Config.BDSP_RNG.ScrollDexIf > 0 && target-advances > Hub.Config.BDSP_RNG.ScrollDexIf)
                            {
                                if (!in_dex)
                                {
                                    await OpenDex(token).ConfigureAwait(false);
                                    in_dex = true;
                                }

                                if (target - 400 > 7000)
                                {
                                    await ResetStick(token).ConfigureAwait(false);
                                    await SetStick(SwitchStick.LEFT, 30_000, 0, 2_000, token).ConfigureAwait(false);
                                }
                                else if (target - 400 > Hub.Config.BDSP_RNG.ScrollDexIf)
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
                                if (target > Hub.Config.BDSP_RNG.ScrollDexIf && can_act)
                                {
                                    await Task.Delay(0_500).ConfigureAwait(false);
                                    Log("Perfoming Actions");
                                    await DoActions(actions, token).ConfigureAwait(false);
                                    can_act = false;
                                }
                            }
                            else if(can_act)
                            {
                                await Task.Delay(0_500).ConfigureAwait(false);
                                Log("Perfoming Actions");
                                await DoActions(actions, token).ConfigureAwait(false);
                                can_act = false;
                            }
                        }
                        else
                            Log($"\nAdvance {advances}\n[S1] 0x{tmpS1:X}, [S0] 0x{tmpS0:X}");
                    }
                }
            }
        }

        private async Task<bool> AutoCalc(SAV8BS sav, CancellationToken token)
		{
            Log("Encounter Slot calculations for wild Pokémon have to be calculated through Pokefinder. Please use ExternalCalc Mode for that.");

            var advances = 0;
            var print = true;
            var actions = ParseActions();
            var type = Hub.Config.BDSP_RNG.RNGType;
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
            var check = false;
            var can_act = true;
            bool in_dex = false;
            var target = 0;
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

            Log($"Initial States: \n[S0] {tmpS0:X}, [S1] {tmpS1:X}\n[S2] {tmpS2:X}, [S3] {tmpS3:X}");

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
                        target = CalculateTarget(xoro, sav, type) - Hub.Config.BDSP_RNG.Delay;

                        if (target == 0)
                        {
                            if (in_dex)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await CloseDex(token).ConfigureAwait(false);
                                in_dex = false;
                            }

                            await Click(actions[0], 0_100, token).ConfigureAwait(false);
                            Log("Starting encounter...");
                            var offset = GetDestOffset(Hub.Config.BDSP_RNG.CheckMode);
                            pk = null;
                            do
                            {
                                pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                            } while (pk is null);
                            Log($"\n\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                            Log("If target is missed, calculate a proper delay with DelayCalc mode and retry.");
                            return StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions);
                        }
                        else if (Hub.Config.BDSP_RNG.RebootIf > 0 && target > Hub.Config.BDSP_RNG.RebootIf)
                        {
                            Log($"Target in {target}. Rebooting.");
                            return false;
                        }
                        else if (check && old_target < target || target < 0)
                        {
                            if (in_dex)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await CloseDex(token).ConfigureAwait(false);
                                in_dex = false;
                            }

                            if (can_act)
                            {
                                Log("Target frame missed. Calculating new target...");
                                print = true;
                            }
							else
							{
                                Log("Traget frame missed.");
                                return false;
							}
                        }
                        else if (Hub.Config.BDSP_RNG.ScrollDexIf > 0 && target > Hub.Config.BDSP_RNG.ScrollDexIf)
                        {
                            if (!in_dex)
                            {
                                await OpenDex(token).ConfigureAwait(false);
                                in_dex = true;
                            }

                            if (target - 400 > 7000)
                            {
                                await ResetStick(token).ConfigureAwait(false);
                                await SetStick(SwitchStick.LEFT, 30_000, 0, 2_000, token).ConfigureAwait(false);
                            }
                            else if (target - 400 > Hub.Config.BDSP_RNG.ScrollDexIf)
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
                            if (target > Hub.Config.BDSP_RNG.ScrollDexIf && can_act)
							{
                                await Task.Delay(0_500).ConfigureAwait(false);
                                Log("Perfoming Actions");
                                await DoActions(actions, token).ConfigureAwait(false);
                                can_act = false;
                            }
                        }
                        else
                        {
							if (can_act)
							{
                                await Task.Delay(0_500).ConfigureAwait(false);
                                Log("Perfoming Actions");
                                await DoActions(actions, token).ConfigureAwait(false);
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

        int CalculateTarget(Xorshift xoro, SAV8BS sav, RNGType type)
		{
            int advances = 0;
            uint[] states = xoro.GetU32State();
            Xorshift rng = new(states[0], states[1], states[2], states[3]);
            int species = (int)Hub.Config.StopConditions.StopOnSpecies;
            var pk = new PB8
            {
                TID = sav.TID,
                SID = sav.SID,
                OT_Name = sav.OT,
                Species = (species != 0) ? species : 1,
            };

			do
			{
                if (type is RNGType.Roamer)
                    pk = Calc.CalculateFromSeed(pk, Shiny.Random, type, rng.Next());
                else
				{
                    states = rng.GetU32State();
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]));
                    rng.Next();
                }
                advances++;
            } while (!HandleTarget(pk, false));
            return advances;
        }

        private async Task CalculateDelay(SAV8BS sav, CancellationToken token)
		{
            var actions = ParseActions();
            var advances = 0;
            var tmpRamState = await SwitchConnection.ReadBytesAbsoluteAsync(RNGOffset, 16, token).ConfigureAwait(false);
            var tmpS0 = BitConverter.ToUInt32(tmpRamState, 0);
            var tmpS1 = BitConverter.ToUInt32(tmpRamState, 4);
            var tmpS2 = BitConverter.ToUInt32(tmpRamState, 8);
            var tmpS3 = BitConverter.ToUInt32(tmpRamState, 12);
            var xoro = new Xorshift(tmpS0, tmpS1, tmpS2, tmpS3);
            var calculatedlist = await Generator(sav, token, false, 500, xoro).ConfigureAwait(false);
            int used;

            if (actions.Count <= 0 || actions.Count > 1)
            {
                Log("\nYou must input at least One and only One Action to trigger the encounter in the Hub settings.\n");
                return;
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
                        await Click(actions[0], 0_100, token).ConfigureAwait(false);
                        used = advances;
                        PB8? pk;
                        Log($"Waiting for pokemon...");
                        var offset = GetDestOffset(Hub.Config.BDSP_RNG.CheckMode);
                        do
                        {
                            pk = await ReadUntilPresentPointer(offset, 0_050, 0_050, 344, token).ConfigureAwait(false);
                        } while (pk is null);
                        var hit = pk.EncryptionConstant;
                        Connection.Log($"\nSpecies: {(Species)pk.Species}{GetString(pk)}");
                        //var result = calculatedlist.IndexOf(hit);
                        var result = 0;
                        var i = 0;

                        foreach(var item in calculatedlist)
						{
                            i++;
                            if(item.EncryptionConstant == hit)
							{
                                result = i;
                                break;
							}
						}
                        var delay = result - used;
                        Log($"\nCalculated delay is {delay}.\n");

                        return;
                    }
				} 
            }
        }

        private async Task<List<PB8>> Generator(SAV8BS sav, CancellationToken token, bool verbose, int maxadvances, Xorshift? xoro = null)
		{
            var type = Hub.Config.BDSP_RNG.RNGType;
            var isroutine = xoro == null;
            var result = new List<PB8>();
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
                    pk = Calc.CalculateFromStates(pk, (type is not RNGType.MysteryGift) ? Shiny.Random : Shiny.Never, type, new Xorshift(states[0], states[1], states[2], states[3]));
                    rng.Next();
                }

                result.Add(pk);

                var msg = $"\nAdvances: {advance}\n[S0] {states[0]:X}, [S1] {states[1]:X}\n[S2] {states[2]:X}, [S3] {states[3]:X}\n{GetString(pk)}";
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

        private string GetString(PB8 pk)
        {

            return $"\nEC: {pk.EncryptionConstant:X}\nPID: {pk.PID:X} {GetShinyType(pk)}\n" +
                $"{(Nature)pk.Nature} nature\nAbility slot: {pk.AbilityNumber}\n" +
                $"IVs: [{pk.IV_HP}, {pk.IV_ATK}, {pk.IV_DEF}, {pk.IV_SPA}, {pk.IV_SPD}, {pk.IV_SPE}]\n";
        }

        private string GetShinyType(PB8 pk)
		{
			if (pk.IsShiny)
			{
                if (pk.ShinyXor == 0)
                    return "(Square)";
                return "(Star)";
			}
            return "";
		}

        bool HandleTarget(PB8 pk, bool dump)
        {
            //Initialize random species
            if (pk.Species == 0)
                pk.Species = 1;

            if (!StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions))
                return false;

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder) && dump)
            {
                DumpPokemon(DumpSetting.DumpFolder, "BDSP_Layout_PKM", pk);
                Count.AddCompletedDumps();
            }

            return true;
        }


        // These don't change per session and we access them frequently, so set these each time we start.
        private async Task InitializeSessionOffsets(CancellationToken token)
        {
            Log("Caching session offsets...");
            RNGOffset = await SwitchConnection.PointerAll(Offsets.MainRNGState, token).ConfigureAwait(false);
            await Task.Delay(1_000).ConfigureAwait(false);
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
                _ => Offsets.OpponentPokemonPointer,
            };
		}

        private List<SwitchButton> ParseActions()
		{
            var action = new List<SwitchButton>();
            var actions = $"{Hub.Config.BDSP_RNG.Actions.ToUpper()},";
            var word = "";
            var index = 0;

            while(index < actions.Length - 1)
			{
                if ((actions.Length > 1 && (actions[index + 1] == ',' || actions[index + 1] == '.')) || actions.Length == 1)
                {
                    word += actions[index];
                    if (Enum.IsDefined(typeof(SwitchButton), word))
                        action.Add((SwitchButton)Enum.Parse(typeof(SwitchButton), word));
                    actions.Remove(0, 1);
                    word = "";
                }
                else if (actions[index] == ',' || actions[index] == '.' || actions[index] == ' ' || actions[index] == '\n' || actions[index] == '\t' || actions[index] == '\0')
                    actions.Remove(0, 1);
                else
				{
                    word += actions[index];
                    actions.Remove(0, 1);
				}
                index++;
			}

            return action;
		}

        private async Task DoActions(List<SwitchButton> actions, CancellationToken token)
		{
            for (var i = 0; i < actions.Count - 1; i++)
            {
                await Click(actions[0], 0_500, token).ConfigureAwait(false);
                actions.RemoveAt(0);
            }
        }
    }
}