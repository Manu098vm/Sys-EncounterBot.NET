using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using Sysbot.Pokemon;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Executor for SW/SH games.
    /// </summary>
    public abstract class PokeRoutineExecutor8 : PokeRoutineExecutor<PK8>
    {
        protected PokeRoutineExecutor8(PokeBotState cfg) : base(cfg) { }

        private static uint GetBoxSlotOffset(int box, int slot) => BoxStartOffset + (uint)(BoxFormatSlotSize * ((30 * box) + slot));

        public override async Task<PK8> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

        public override async Task<PK8> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync((uint) offset, size, token).ConfigureAwait(false);
            return new PK8(data);
        }

        public override async Task<PK8> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
            if (!valid)
                return new PK8();
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            return new PK8(data);
        }

        public async Task<PK8> ReadSurpriseTradePokemon(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(SurpriseTradePartnerPokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            return new PK8(data);
        }

        public async Task SetBoxPokemon(PK8 pkm, int box, int slot, CancellationToken token, ITrainerInfo? sav = null)
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

        public override async Task<PK8> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            var ofs = GetBoxSlotOffset(box, slot);
            return await ReadPokemon(ofs, BoxFormatSlotSize, token).ConfigureAwait(false);
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

        public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
        {
            var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
            return !result.SequenceEqual(original);
        }

        public async Task<SAV8SWSH> IdentifyTrainer(CancellationToken token)
        {
            Log("Grabbing trainer data of host console...");
            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            InitSaveData(sav);

            if (await GetTextSpeed(token).ConfigureAwait(false) != TextSpeedOption.Fast)
                Log("Text speed should be set to FAST. Stop the bot and fix this if you encounter problems.");

            return sav;
        }

        public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
        {
            Log("Detaching on startup.");
            await DetachController(token).ConfigureAwait(false);
            if (settings.ScreenOff)
            {
                Log("Turning off screen.");
                await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
            }
        }

        public async Task CleanExit(IBotStateSettings settings, CancellationToken token)
        {
            if (settings.ScreenOff)
            {
                Log("Turning on screen.");
                await SetScreen(ScreenState.On, token).ConfigureAwait(false);
            }
            Log("Detaching controllers on routine exit.");
            await DetachController(token).ConfigureAwait(false);
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

        public async Task<bool> IsGiftFound(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(GiftFound, 1, token).ConfigureAwait(false))[0] > 0;

        public async Task ReOpenGame(PokeBotHubConfig config, CancellationToken token)
        {
            // Reopen the game if we get soft-banned
            Log("Potential soft ban detected, reopening game just in case!");
            await CloseGame(config, token).ConfigureAwait(false);
            await StartGame(config, token).ConfigureAwait(false);

            // In case we are soft banned, reset the timestamp
            await Unban(token).ConfigureAwait(false);
        }

        public async Task Unban(CancellationToken token)
        {
            // Like previous generations, the game uses a Unix timestamp for 
            // how long we are soft banned and once the soft ban is lifted
            // the game sets the value back to 0 (1970/01/01 12:00 AM (UTC))
            Log("Soft ban detected, unbanning.");
            var data = BitConverter.GetBytes(0);
            await Connection.WriteBytesAsync(data, SoftBanUnixTimespanOffset, token).ConfigureAwait(false);
        }

        public async Task<bool> CheckIfSoftBanned(CancellationToken token)
        {
            // Check if the Unix Timestamp isn't zero, if so we are soft banned.
            var data = await Connection.ReadBytesAsync(SoftBanUnixTimespanOffset, 1, token).ConfigureAwait(false);
            return data[0] > 1;
        }

        public async Task CloseGame(PokeBotHubConfig config, CancellationToken token)
        {
            var timing = config.Timings;
            // Close out of the game
            await Click(HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
            await Click(X, 1_000, token).ConfigureAwait(false);
            await Click(A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
            Log("Closed out of the game!");
        }

        public async Task StartGame(PokeBotHubConfig config, CancellationToken token)
        {
            var timing = config.Timings;
            // Open game.
            await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            // Menus here can go in the order: Update Prompt -> Profile -> DLC check -> Unable to use DLC.
            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (timing.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

            await Click(A, 1_000 + timing.ExtraTimeCheckDLC, token).ConfigureAwait(false);
            // If they have DLC on the system and can't use it, requires an UP + A to start the game.
            // Should be harmless otherwise since they'll be in loading screen.
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 0_600, token).ConfigureAwait(false);

            Log("Restarting the game!");

            // Switch Logo lag, skip cutscene, game load screen
            await Task.Delay(10_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

            for (int i = 0; i < 4; i++)
                await Click(A, 1_000, token).ConfigureAwait(false);

            var timer = 60_000;
            while (!await IsOnOverworld(config, token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false))
            {
                await Task.Delay(0_200, token).ConfigureAwait(false);
                timer -= 0_250;
                // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
                // Don't risk it if hub is set to avoid updates.
                if (timer <= 0 && !timing.AvoidSystemUpdate)
                {
                    Log("Still not in the game, initiating rescue protocol!");
                    while (!await IsOnOverworld(config, token).ConfigureAwait(false) && !await IsInBattle(token).ConfigureAwait(false))
                        await Click(A, 6_000, token).ConfigureAwait(false);
                    break;
                }
            }

            Log("Back in the overworld!");
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

        public async Task<bool> IsInBox(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
            var dataint = BitConverter.ToUInt32(data, 0);
            return dataint is CurrentScreen_Box1 or CurrentScreen_Box2;
        }

        public async Task<bool> IsOnOverworld(PokeBotHubConfig config, CancellationToken token)
        {
            // Uses CurrentScreenOffset and compares the value to CurrentScreen_Overworld.
            if (config.ScreenDetection == ScreenDetectionMode.Original)
            {
                var data = await Connection.ReadBytesAsync(CurrentScreenOffset, 4, token).ConfigureAwait(false);
                var dataint = BitConverter.ToUInt32(data, 0);
                return dataint is CurrentScreen_Overworld1 or CurrentScreen_Overworld2;
            }
            // Uses an appropriate OverworldOffset for the console language.
            if (config.ScreenDetection == ScreenDetectionMode.ConsoleLanguageSpecific)
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
        public async Task FleeToOverworld(CancellationToken token)
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

        public async Task<bool> IsInLairWait(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(LairWait, 1, token).ConfigureAwait(false))[0] == 0;
        public async Task<bool> IsInLairEndList(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(LairRewards, 1, token).ConfigureAwait(false))[0] != 0;

        public async Task<bool> IsArticunoPresent(CancellationToken token) => (await Connection.ReadBytesAsync(IsArticunoInSnowslide, 1, token).ConfigureAwait(false))[0] == 1;
        public async Task<byte[]> ReadKCoordinates(CancellationToken token) => await SwitchConnection.ReadBytesAsync(KCoordinatesBlock, 24592, token).ConfigureAwait(false);
        public async Task<List<PK8>> ReadOwPokemonFromBlock(byte[] KCoordinates, SAV8SWSH sav, CancellationToken token)
        {
            var PK8s = new List<PK8>();

            var i = 8;
            var j = 0;
            var count = 0;
            var last_index = i;

            while (!token.IsCancellationRequested && i < KCoordinates.Length)
            {
                if (j == 12)
                {
                    if (KCoordinates[i - 68] != 0 && KCoordinates[i - 68] != 255)
                    {
                        var bytes = KCoordinates.Slice(i - 68, 56);
                        j = 0;
                        i = last_index + 8;
                        last_index = i;
                        count++;
                        var pkm = await ReadOwPokemon(0, 0, bytes, sav, token).ConfigureAwait(false);
                        if (pkm != null)
                            PK8s.Add(pkm);
                    }
                }

                if (KCoordinates[i] == 0xFF)
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
        public async Task<PK8?> ReadOwPokemon(Species target, uint startoffset, byte[]? mondata, SAV8SWSH TrainerData, CancellationToken token)
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
                var pk = new PK8
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

                pk = RNG8.CalculateFromSeed(pk, shinyness, ivs, seed);
                return pk;
            }
            else
                return null;
        }
    }
}
