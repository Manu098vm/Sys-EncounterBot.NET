﻿using PKHeX.Core;
using SysBot.Base;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutor : SwitchRoutineExecutor<PokeBotState>
    {
        protected PokeRoutineExecutor(PokeBotState cfg) : base(cfg) { }

        public LanguageID GameLang { get; private set; }
        public GameVersion Version { get; private set; }
        public string InGameName { get; private set; } = "E-BOT";

        public override void SoftStop() => Config.Pause();

        public async Task Click(SwitchButton b, int delayMin, int delayMax, CancellationToken token) =>
            await Click(b, Util.Rand.Next(delayMin, delayMax), token).ConfigureAwait(false);

        public async Task SetStick(SwitchStick stick, short x, short y, int delayMin, int delayMax, CancellationToken token) =>
            await SetStick(stick, x, y, Util.Rand.Next(delayMin, delayMax), token).ConfigureAwait(false);

        private static uint GetBoxSlotOffset(int box, int slot) => BoxStartOffset + (uint)(BoxFormatSlotSize * ((30 * box) + slot));

        public async Task<PK8> ReadPokemon(uint offset, CancellationToken token, int size = BoxFormatSlotSize)
        {
            var data = await Connection.ReadBytesAsync(offset, size, token).ConfigureAwait(false);
            return new PK8(data);
        }
        public async Task<PK8> ReadPokemon(ulong offset, CancellationToken token, int size = BoxFormatSlotSize)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            return new PK8(data);
        }

        public async Task<PB7> LGReadPokemon(uint offset, CancellationToken token, int size = EncryptedSize, bool heap = true)
        {
            byte[] data;
            if (heap == true)
                data = await Connection.ReadBytesAsync(offset, size, token).ConfigureAwait(false);
            else
                data = await SwitchConnection.ReadBytesMainAsync(offset, size, token).ConfigureAwait(false);
            return new PB7(data);
        }
        public async Task<PB7> LGReadPokemon(ulong offset, CancellationToken token, int size = EncryptedSize)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            return new PB7(data);
        }

        public async Task SetBoxPokemon(PK8 pkm, int box, int slot, CancellationToken token, SAV8? sav = null)
        {
            if (sav != null)
            {
                // Update PKM to the current save's handler data
                DateTime Date = DateTime.Now;
                pkm.Trade(sav, Date.Day, Date.Month, Date.Year);
                pkm.RefreshChecksum();
            }
            var ofs = GetBoxSlotOffset(box, slot);
            pkm.ResetPartyStats();
            await Connection.WriteBytesAsync(pkm.EncryptedPartyData, ofs, token).ConfigureAwait(false);
        }

        public async Task<PK8> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            var ofs = GetBoxSlotOffset(box, slot);
            return await ReadPokemon(ofs, token, BoxFormatSlotSize).ConfigureAwait(false);
        }

        public async Task SetCurrentBox(int box, CancellationToken token)
        {
            await Connection.WriteBytesAsync(BitConverter.GetBytes(box), CurrentBoxOffset, token).ConfigureAwait(false);
        }

        public async Task<int> GetCurrentBox(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(CurrentBoxOffset, 1, token).ConfigureAwait(false);
            return data[0];
        }

        public async Task<PK8?> ReadUntilPresent(uint offset, int waitms, int waitInterval, CancellationToken token, int size = BoxFormatSlotSize)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await ReadPokemon(offset, token, size).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }
        public async Task<PK8?> ReadUntilPresent(ulong offset, int waitms, int waitInterval, CancellationToken token, int size = BoxFormatSlotSize)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await ReadPokemon(offset, token, size).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }
        public async Task<PB7?> LGReadUntilPresent(uint offset, int waitms, int waitInterval, CancellationToken token, int size = EncryptedSize, bool heap = true)
        {
            int msWaited = 0;
            while(msWaited < waitms)
            {
                var pk = await LGReadPokemon(offset, token, size, heap).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }
        public async Task<PB7?> LGReadUntilPresent(ulong offset, int waitms, int waitInterval, CancellationToken token, int size = EncryptedSize)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await LGReadPokemon(offset, token, size).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }

        public async Task<bool> IsArticunoPresent(CancellationToken token) => (await Connection.ReadBytesAsync(IsArticunoInSnowslide, 1, token).ConfigureAwait(false))[0] == 1;

        public async Task<byte[]> ReadKCoordinates(CancellationToken token) => await SwitchConnection.ReadBytesLargeAsync(KCoordinatesBlock, 24592, token).ConfigureAwait(false);
        
        public async Task<List<PK8>> ReadOwPokemonFromBlock(byte[] KCoordinates, SAV8 sav, CancellationToken token)
        {
            List<PK8> PK8s = new List<PK8>();

            int i = 8;
            int j = 0;
            int last_index = 8;
            int count = 0;

            while(!token.IsCancellationRequested && i < KCoordinates.Length)
            {
                //If someone finds a better way to run through the block and find the spawns, feel free to improve this function.
                if(j == 12)
                {
                    if (KCoordinates[i - 68] != 0 && KCoordinates[i - 68] != 255)
                    {
                        byte[] Bytes = KCoordinates.Slice(i - 68, 56);
                        j = 0;
                        i = last_index + 8;
                        last_index = i;
                        count++;
                        var pkm = await ReadOwPokemon(0, 0, Bytes, sav, token).ConfigureAwait(false);
                        if (pkm != null)
                            PK8s.Add(pkm);
                    }
                }

                if(KCoordinates[i] == 255)
                {
                    if (i % 8 == 0)
                        last_index = i;
                    i++;
                    j++;
                }
                else
                {
                    j = 0;
                    if (i == last_index)
                    {
                        i += 8;
                        last_index = i;
                    }
                    else
                    {
                        i = last_index + 8;
                        last_index = i;
                    }
                }
                
            }
            return PK8s;
        }

        public async Task<PK8?> ReadOwPokemon(Species target, uint startoffset, byte[]? mondata, SAV8 TrainerData, CancellationToken token)
        {
            byte[]? data = null;
            Species species = 0;
            uint offset = startoffset;
            int i = 0;

            if (target != (Species)0)
            {
                do
                {
                    data = await Connection.ReadBytesAsync(offset, 56, token).ConfigureAwait(false);
                    species = (Species)BitConverter.ToUInt16(data.Slice(0, 2), 0);
                    offset += 192;
                    i++;
                } while (target != 0 && species != 0 && target != species && i <= 40);
                if (i > 40)
                    data = null;
            }
            else if (mondata != null)
            {
                data = mondata;
                species = (Species)BitConverter.ToUInt16(data.Slice(0, 2), 0);
            }

            if (data != null && data[20] == 1)
            {
                PK8 pk = new PK8
                {
                    Species = (int)species,
                    Form = data[2],
                    CurrentLevel = data[4],
                    Met_Level = data[4],
                    Gender = (data[10] == 1) ? 0 : 1,
                    OT_Name = TrainerData.OT,
                    TID = TrainerData.TID,
                    SID = TrainerData.SID,
                    OT_Gender = TrainerData.Gender,
                    HT_Name = TrainerData.OT,
                    HT_Gender = TrainerData.Gender,
                    Move1 = BitConverter.ToUInt16(data.Slice(48, 2), 0),
                    Move2 = BitConverter.ToUInt16(data.Slice(50, 2), 0),
                    Move3 = BitConverter.ToUInt16(data.Slice(52, 2), 0),
                    Move4 = BitConverter.ToUInt16(data.Slice(54, 2), 0),
                    Version = 44,
                };
                pk.SetNature(data[8]);
                pk.SetAbility(data[12] - 1);
                if (data[22] != 255)
                    pk.SetRibbonIndex((RibbonIndex)data[22]);
                if (!pk.IsGenderValid())
                    pk.Gender = 2;
                if (data[14] == 1)
                    pk.HeldItem = data[16];

                Shiny shinyness = (Shiny)(data[6] + 1);
                int ivs = data[18];
                uint seed = BitConverter.ToUInt32(data.Slice(24, 4), 0);

                pk = OverworldSWSHRNG.CalculateFromSeed(pk, shinyness, ivs, seed);
                return pk;
            }
            else
                return null;
        }

        // Reads an offset until it changes to either match or differ from the comparison value.
        // If "match" is set to true, then the function returns true when the offset matches the given value.
        // Otherwise, it returns true when the offset no longer matches the given value.
        public async Task<bool> ReadUntilChanged(uint offset, byte[] comparison, int waitms, int waitInterval, bool match, CancellationToken token)
        {
            var sw = new Stopwatch();
            sw.Start();
            do
            {
                var result = await Connection.ReadBytesAsync(offset, comparison.Length, token).ConfigureAwait(false);
                if (match == result.SequenceEqual(comparison))
                    return true;

                await Task.Delay(waitInterval, token).ConfigureAwait(false);
            } while (sw.ElapsedMilliseconds < waitms);
            return false;
        }

        public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
        {
            var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
            return !result.SequenceEqual(original);
        }

        public async Task<SAV8SWSH> IdentifyTrainer(CancellationToken token)
        {
            Log("Grabbing trainer data of host console...");
            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            GameLang = (LanguageID)sav.Language;
            Version = sav.Version;
            InGameName = sav.OT;
            Connection.Label = $"{InGameName}-{sav.DisplayTID:000000}";
            Log($"{Connection.Name} identified as {Connection.Label}, using {GameLang}.");

            return sav;
        }
        public async Task<SAV7b> LGIdentifyTrainer(CancellationToken token)
        {
            Log("Grabbing trainer data of host console...");
            SAV7b sav = await LGGetFakeTrainerSAV(token).ConfigureAwait(false);
            GameLang = (LanguageID)sav.Language;
            Version = sav.Version;
            InGameName = sav.OT;
            Connection.Label = $"{InGameName}-{sav.DisplayTID:000000}";
            Log($"{Connection.Name} identified as {Connection.Label}, using {GameLang}.");

            return sav;
        }

        public static void DumpPokemon(string folder, string subfolder, PKM pk)
        {
            if (!Directory.Exists(folder))
                return;
            var dir = Path.Combine(folder, subfolder);
            Directory.CreateDirectory(dir);
            var fn = Path.Combine(dir, Util.CleanFileName(pk.FileName));
            File.WriteAllBytes(fn, pk.DecryptedPartyData);
            LogUtil.LogInfo($"Saved file: {fn}", "Dump");
        }

        /// <summary>
        /// Identifies the trainer information and loads the current runtime language.
        /// </summary>
        public async Task<SAV8SWSH> GetFakeTrainerSAV(CancellationToken token)
        {
            var sav = new SAV8SWSH();
            var info = sav.MyStatus;
            var read = await Connection.ReadBytesAsync(TrainerDataOffset, TrainerDataLength, token).ConfigureAwait(false);
            read.CopyTo(info.Data, 0);
            return sav;
        }

        public async Task<SAV7b> LGGetFakeTrainerSAV(CancellationToken token)
        {
            SAV7b lgpe = new SAV7b();
            byte[] dest = lgpe.Blocks.Status.Data;
            int startofs = lgpe.Blocks.Status.Offset;
            byte[]? data = await Connection.ReadBytesAsync(TrainerData, TrainerSize, token).ConfigureAwait(false);
            data.CopyTo(dest, startofs);
            return lgpe;
        }


        public async Task ReOpenGame(PokeTradeHubConfig config, CancellationToken token)
        {
            await CloseGame(config, token).ConfigureAwait(false);
            await StartGame(config, token).ConfigureAwait(false);
        }

        public async Task CloseGame(PokeTradeHubConfig config, CancellationToken token)
        {
            // Close out of the game
            await Click(HOME, 2_000 + config.Timings.ExtraTimeReturnHome, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000 + config.Timings.ExtraTimeCloseGame, token).ConfigureAwait(false);
            Log("Closed out of the game!");
        }

        public async Task StartGame(PokeTradeHubConfig config, CancellationToken token)
        {
            // Open game.
            await Click(A, 1_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            // Menus here can go in the order: Update Prompt -> Profile -> DLC check -> Unable to use DLC.
            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (config.Timings.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

            await Click(A, 1_000 + config.Timings.ExtraTimeCheckDLC, token).ConfigureAwait(false);
            // If they have DLC on the system and can't use it, requires an UP + A to start the game.
            // Should be harmless otherwise since they'll be in loading screen.
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 0_600, token).ConfigureAwait(false);

            Log("Restarting the game!");

            // Switch Logo lag, skip cutscene, game load screen
            await Task.Delay(10_000 + config.Timings.ExtraTimeLoadGame, token).ConfigureAwait(false);

            for (int i = 0; i < 4; i++)
                await Click(A, 1_000, token).ConfigureAwait(false);

            var timer = 60_000;
            while (!await IsOnOverworld(config, token).ConfigureAwait(false))
            {
                await Task.Delay(0_200, token).ConfigureAwait(false);
                timer -= 0_250;
                // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
                // Don't risk it if hub is set to avoid updates.
                if (timer <= 0 && !config.Timings.AvoidSystemUpdate)
                {
                    Log("Still not in the game, initiating rescue protocol!");
                    while (!await IsOnOverworld(config, token).ConfigureAwait(false))
                        await Click(A, 6_000, token).ConfigureAwait(false);
                    break;
                }
            }

            Log("Back in the overworld!");
        }

        public async Task<bool> IsEggReady(Enumerations daycare, CancellationToken token)
        {
            var ofs = GetDaycareEggIsReadyOffset(daycare);
            // Read a single byte of the Daycare metadata to check the IsEggReady flag.
            var data = await Connection.ReadBytesAsync(ofs, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        public async Task SetEggStepCounter(Enumerations daycare, CancellationToken token)
        {
            // Set the step counter in the Daycare metadata to 180. This is the threshold that triggers the "Should I create a new egg" subroutine.
            // When the game executes the subroutine, it will generate a new seed and set the IsEggReady flag.
            // Just setting the IsEggReady flag won't refresh the seed; we want a different egg every time.
            var data = new byte[] { 0xB4, 0, 0, 0 }; // 180
            var ofs = GetDaycareStepCounterOffset(daycare);
            await Connection.WriteBytesAsync(data, ofs, token).ConfigureAwait(false);
        }

        public async Task<bool> IsCorrectScreen(uint expectedScreen, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
            return BitConverter.ToUInt32(data, 0) == expectedScreen;
        }

        public async Task<uint> GetCurrentScreen(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
            return BitConverter.ToUInt32(data, 0);
        }
        
        public async Task<bool> IsInBattle(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(Version == GameVersion.SH ? InBattleRaidOffsetSH : InBattleRaidOffsetSW, 1, token).ConfigureAwait(false);
            return data[0] == (Version == GameVersion.SH ? 0x40 : 0x41);
        }

        public async Task<bool> IsInLairWait(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(LairWait, 1, token).ConfigureAwait(false))[0] == 0;

        public async Task<bool> IsInLairEndList(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(LairRewards, 1, token).ConfigureAwait(false))[0] != 0;

        public async Task<bool> SWSHIsGiftFound(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(GiftFound, 1, token).ConfigureAwait(false))[0] > 0;

        public async Task<bool> IsOnOverworld(PokeTradeHubConfig config, CancellationToken token)
        {
            // Uses CurrentScreenOffset and compares the value to CurrentScreen_Overworld.
            if (config.ScreenDetection == ScreenDetectionMode.Original)
            {
                var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
                var dataint = BitConverter.ToUInt32(data, 0);
                return dataint == CurrentScreen_Overworld1 || dataint == CurrentScreen_Overworld2;
            }
            // Uses an appropriate OverworldOffset for the console language.
            else if (config.ScreenDetection == ScreenDetectionMode.ConsoleLanguageSpecific)
            {
                var data = await Connection.ReadBytesAsync(GetOverworldOffset(config.ConsoleLanguage), 1, token).ConfigureAwait(false);
                return data[0] == 1;
            }
            return false;
        }

        public bool IsPKLegendary(int species) => Enum.IsDefined(typeof(Legendary), (Legendary)species);
        
        public async Task<bool> LGIsInTitleScreen(CancellationToken token) => !((await SwitchConnection.ReadBytesMainAsync(IsInTitleScreen, 1, token).ConfigureAwait(false))[0] == 1);
        public async Task<bool> LGIsInBattle(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInBattleScenario, 1, token).ConfigureAwait(false))[0] > 0;
        public async Task<bool> LGIsInCatchScreen(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInOverworld, 1, token).ConfigureAwait(false))[0] != 0;
        public async Task<bool> LGIsInTrade(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInTrade, 1, token).ConfigureAwait(false))[0] != 0;
        public async Task<bool> LGIsGiftFound(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsGiftFound, 1, token).ConfigureAwait(false))[0] > 0;
        public async Task<uint> LGEncounteredWild(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(CatchingSpecies, 2, token).ConfigureAwait(false),0);
        public async Task<GameVersion> LGWhichGameVersion(CancellationToken token)
        {
            byte[] data = await Connection.ReadBytesAsync(LGGameVersion, 1, token).ConfigureAwait(false);
            if (data[0] == 0x01)
                return GameVersion.GP;
            else if (data[0] == 0x02)
                return GameVersion.GE;
            else
                return GameVersion.Invalid;
        }

        public async Task<bool> LGIsNatureTellerEnabled(CancellationToken token) => (await Connection.ReadBytesAsync(NatureTellerEnabled, 1, token).ConfigureAwait(false))[0] == 0x04;
        public async Task<Nature> LGReadWildNature(CancellationToken token) => (Nature)BitConverter.ToUInt16(await Connection.ReadBytesAsync(WildNature, 2, token).ConfigureAwait(false), 0);
        public async Task LGEnableNatureTeller(CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes(0x04), NatureTellerEnabled, token).ConfigureAwait(false);
        public async Task LGEditWildNature(Nature target, CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes((uint)target), WildNature, token).ConfigureAwait(false);
        public async Task<uint> LGReadSpeciesCombo(CancellationToken token) =>
            BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
        public async Task<uint> LGReadComboCount(CancellationToken token) =>
            BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
        public async Task LGEditSpeciesCombo(uint species, CancellationToken token) =>
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(species), await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), token).ConfigureAwait(false);
        public async Task LGEditComboCount(uint count, CancellationToken token) =>
            await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes(count), await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), token).ConfigureAwait(false);
        public async Task<long> LGCountMilliseconds(PokeTradeHubConfig config, CancellationToken token)
        {
            long WaitMS = config.LGPE_OverworldScan.MaxMs;
            bool stuck = false;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            byte[] data = await SwitchConnection.ReadBytesMainAsync(FreezedValue, 1, token).ConfigureAwait(false);
            byte[] comparison = data;
            do
            {
                data = await SwitchConnection.ReadBytesMainAsync(FreezedValue, 1, token).ConfigureAwait(false);
                if (stopwatch.ElapsedMilliseconds > WaitMS)
                    stuck = true;
            } while (data.SequenceEqual(comparison) && stuck == false && !token.IsCancellationRequested);
            if (!stuck)
            {
                stopwatch.Restart();
                comparison = data;
                do
                {
                    data = await SwitchConnection.ReadBytesMainAsync(FreezedValue, 1, token).ConfigureAwait(false);
                } while (data == comparison && !token.IsCancellationRequested);
                return stopwatch.ElapsedMilliseconds;
            }             
            else
                return 0;
        }

        //Let's Go useful cheats for testing purposes.
        public async Task LGZaksabeast(CancellationToken token, GameVersion version)
        {
            uint offset = version == GameVersion.GP ? PGeneratingFunction1 : EGeneratingFunction1;
            //This is basically the Zaksabeast code ported for the newest Let's game version. 
            byte[] inject = new byte[] { 0xE9, 0x03, 0x00, 0x2A, 0x60, 0x12, 0x40, 0xB9, 0xE1, 0x03, 0x09, 0x2A, 0x69, 0x06, 0x00, 0xF9, 0xDC, 0xFD, 0xFF, 0x97, 0x40, 0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(inject, offset, token).ConfigureAwait(false);
        }
        public async Task LGUnfreeze(CancellationToken token, GameVersion version)
        {
            uint offset = version == GameVersion.GP ? PGeneratingFunction7 : EGeneratingFunction7;
            byte[] data = new byte[] { 0x0C, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(data, offset, token).ConfigureAwait(false);
        }
        public async Task LGForceShiny(CancellationToken token, GameVersion version)
        {
            uint offset = version == GameVersion.GP ? PShinyValue : EShinyValue;
            //100% Shiny Odds
            byte[] inject = new byte[] { 0x27, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(inject, offset, token).ConfigureAwait(false);
        }
        public async Task LGNormalShiny(CancellationToken token, GameVersion version)
        {
            uint offset = version == GameVersion.GP ? PShinyValue : EShinyValue;
            //Standard shiny odds
            byte[] inject = new byte[] { 0xE0, 0x02, 0x00, 0x54 };
            await SwitchConnection.WriteBytesMainAsync(inject, offset, token).ConfigureAwait(false);
        }

        public async Task LGOpenGame(PokeTradeHubConfig config, CancellationToken token)
        {
            // Open game.
            await Click(A, 1_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (config.Timings.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            while (await LGIsInTitleScreen(token).ConfigureAwait(false))
            {
                if(stopwatch.ElapsedMilliseconds > 6000)
                    await DetachController(token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);
            }
            Log("Game started.");
        }

        public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
            return (TextSpeedOption)(data[0] & 3);
        }

        public async Task SetTextSpeed(TextSpeedOption speed, CancellationToken token)
        {
            var textSpeedByte = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
            var data = new[] { (byte)((textSpeedByte[0] & 0xFC) | (int)speed) };
            await Connection.WriteBytesAsync(data, TextSpeedOffset, token).ConfigureAwait(false);
        }

        //Pointer parser, code from ALM
        public async Task<ulong> ParsePointer(String pointer, CancellationToken token)
        {
            var ptr = pointer;
            uint finadd = 0;
            if (!ptr.EndsWith("]"))
                finadd = Util.GetHexValue(ptr.Split('+').Last());
            var jumps = ptr.Replace("main", "").Replace("[", "").Replace("]", "").Split(new[] { "+" }, StringSplitOptions.RemoveEmptyEntries);
            if (jumps.Length == 0)
            {
                Log("Invalid Pointer");
                return 0;
            }

            var initaddress = Util.GetHexValue(jumps[0].Trim());
            ulong address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesMainAsync(initaddress, 0x8, token).ConfigureAwait(false), 0);
            foreach (var j in jumps)
            {
                var val = Util.GetHexValue(j.Trim());
                if (val == initaddress)
                    continue;
                if (val == finadd)
                {
                    address += val;
                    break;
                }
                address = BitConverter.ToUInt64(await SwitchConnection.ReadBytesAbsoluteAsync(address + val, 0x8, token).ConfigureAwait(false), 0);
            }
            return address;
        }

        public static uint GetOverworldOffset(ConsoleLanguageParameter value)
        {
            return value switch
            {
                ConsoleLanguageParameter.French => OverworldOffsetFrench,
                ConsoleLanguageParameter.German => OverworldOffsetGerman,
                ConsoleLanguageParameter.Spanish => OverworldOffsetSpanish,
                ConsoleLanguageParameter.Italian => OverworldOffsetItalian,
                ConsoleLanguageParameter.Japanese => OverworldOffsetJapanese,
                ConsoleLanguageParameter.ChineseTraditional => OverworldOffsetChineseT,
                ConsoleLanguageParameter.ChineseSimplified => OverworldOffsetChineseS,
                ConsoleLanguageParameter.Korean => OverworldOffsetKorean,
                _ => OverworldOffset,
            };
        }
    }
}
