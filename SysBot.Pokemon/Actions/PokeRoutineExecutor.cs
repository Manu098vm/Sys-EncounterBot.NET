using PKHeX.Core;
using SysBot.Base;
using System;
using System.Diagnostics;
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
        public string InGameName { get; private set; } = "SysBot.NET";

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

        public async Task<PK8?> ReadOwPokemon(Species target, uint startoffset, SAV8 TrainerData, CancellationToken token)
        {
            byte[] data;
            Species species = (Species)0;
            uint offset = startoffset;
            do
            {
                data = await Connection.ReadBytesAsync(offset, 56, token).ConfigureAwait(false);
                species = (Species)BitConverter.ToUInt16(data.Slice(0, 2), 0);
                Log($"Target: {target}, Encountered: {species}");
                offset += 192;
            } while (target != 0 && species != 0 && target != species);

            if (data[20] == 1)
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
                    TrainerID7 = TrainerData.TrainerID7,
                    TrainerSID7 = TrainerData.TrainerSID7,
                    OT_Gender = TrainerData.Gender,
                    HT_Name = TrainerData.OT,
                    HT_Gender = TrainerData.Gender,
                    Move1 = BitConverter.ToUInt16(data.Slice(48, 2), 0),
                    Move2 = BitConverter.ToUInt16(data.Slice(50, 2), 0),
                    Move3 = BitConverter.ToUInt16(data.Slice(52, 2), 0),
                    Move4 = BitConverter.ToUInt16(data.Slice(54, 2), 0),
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

                Log($"Stats in RAM: Shinyness {shinyness}, IVs {ivs}, Seed: {String.Format("{0:X}", seed)}");

                pk = Overworld8RNG.CalculateFromSeed(pk, shinyness, ivs, seed);
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

            if (await GetTextSpeed(token).ConfigureAwait(false) != TextSpeedOption.Fast)
                Log("Text speed should be set to FAST. Stop the bot and fix this if you encounter problems.");

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
            read.CopyTo(info.Data);
            return sav;
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

            while (!(await IsOnOverworld(config, token).ConfigureAwait(false) || await IsInBattle(token)))
            {
                await Click(A, 0_500, token).ConfigureAwait(false);
                await Task.Delay(2_000, token).ConfigureAwait(false);
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

        public async Task<bool> IsInLairWait(CancellationToken token)
        {
            byte[] menu = BitConverter.GetBytes(CurrentScreen_LairMenu);
            byte[] docked = await Connection.ReadBytesAsync(CurrentLairScreenOffset, 4, token).ConfigureAwait(false);
            byte[] handheld = await Connection.ReadBytesAsync(CurrentLairScreenOffset, 4, token);
            if (menu.SequenceEqual(docked) || menu.SequenceEqual(handheld))
                return true;
            else return false;
        }

        public async Task<int> IsInLairEndList(CancellationToken token)
        {
            //Checking all the lair rewards pointers, if one contains a Pokémon, it means the adventure is ended.
            //Returns the pointer id used.
            /*Legenda:
             * 0 = No rewards, adventure not started/ended or lost at the first raid.
             * 1 = First pointer used
             * 2 = Second pointer used
             * etc. check Structures/RAM/PokeDataOffsets
             * */
            int i = 0;
            foreach(string pointer in dynamaxRewards)
            {
                i++;
                var pkm = await ReadPokemon(await ParsePointer(pointer, token), token, 344).ConfigureAwait(false);
                if (pkm != null && pkm.Species != 0 && pkm.ChecksumValid)
                    return i;
            }
            return 0;
        }

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
