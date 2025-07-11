using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets7B;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Executor for LGPE games.
    /// </summary>
    public abstract class PokeRoutineExecutor7B : PokeRoutineExecutor<PB7>
    {
        protected PokeRoutineExecutor7B(PokeBotState cfg) : base(cfg) { }

        public override async Task<PB7> ReadPokemon(ulong offset, CancellationToken token)
		{
            var data = await Connection.ReadBytesAsync((uint)offset, BoxFormatSlotSize, token).ConfigureAwait(false);
            return new PB7(data);
        }

        public override async Task<PB7> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesMainAsync((uint)offset, size, token).ConfigureAwait(false);
            return new PB7(data);
        }

        public override async Task<PB7> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
            if (!valid)
                return new PB7();
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            return new PB7(data);
        }

        public override async Task<PB7> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            await SetDeviceType(HidDeviceType.JoyRight1, token).ConfigureAwait(false);
            return new PB7();
        }

        public async Task<PB7?> ReadUntilPresent(uint offset, int waitms, int waitInterval, CancellationToken token)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await ReadPokemon(offset, token).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }

        public async Task<PB7?> ReadUntilPresentMain(uint offset, int waitms, int waitInterval, CancellationToken token, int size = BoxFormatSlotSize)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await ReadPokemon(offset, size, token).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }

        public async Task<PB7?> ReadMainPokeData(CancellationToken token) => await ReadUntilPresentMain(MainPokeData, 2_000, 0_200, token).ConfigureAwait(false);
        public async Task<PB7?> ReadGift(CancellationToken token) => await ReadUntilPresent(GiftPokeData, 2_000, 0_200, token).ConfigureAwait(false);
        public async Task<PB7?> ReadTrade(CancellationToken token) => await ReadMainPokeData(token).ConfigureAwait(false);
        public async Task<PB7?> ReadWild(CancellationToken token) => await ReadUntilPresent(WildPokeData, 2_000, 0_200, token).ConfigureAwait(false);
        public async Task<PB7?> ReadGoEntity(CancellationToken token) => await ReadUntilPresent(GoPokeData, 2_000, 0_200, token).ConfigureAwait(false);
        public async Task<PB7?> ReadStationary(CancellationToken token) => await ReadUntilPresent(StationaryPokeData, 2_000, 0_200, token).ConfigureAwait(false);
        public async Task<PB7?> ReadFossil(CancellationToken token) => await ReadUntilPresentPointer(PokeDataPointers7B.FossilPokeData, 1_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);

        public async Task<PB7?> ReadGiftOrFossil(CancellationToken token)
        {
            var pk = await ReadGift(token).ConfigureAwait(false);
            pk ??= await ReadFossil(token).ConfigureAwait(false);
            return pk;
        }

        public async Task<PB7?> ReadWildOrGo(CancellationToken token)
        {
            var pk = await ReadWild(token).ConfigureAwait(false);
            pk ??= await ReadGoEntity(token).ConfigureAwait(false);
            return pk;
        }

        public async Task<bool> IsInTitleScreen(CancellationToken token) => !((await SwitchConnection.ReadBytesMainAsync(PokeDataOffsets7B.IsInTitleScreen, 1, token).ConfigureAwait(false))[0] == 1);

        public async Task<bool> IsInBattle(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInBattleScenario, 1, token).ConfigureAwait(false))[0] > 0;

        public async Task<bool> IsInCatchScreen(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInOverworld, 1, token).ConfigureAwait(false))[0] != 0;

        public async Task<bool> IsInConfirmDialog(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInConfirmationDialog, 1, token).ConfigureAwait(false))[0] != 0;

        public async Task<bool> IsInTrade(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(PokeDataOffsets7B.IsInTrade, 1, token).ConfigureAwait(false))[0] != 0;

        public async Task<bool> IsGiftFound(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(PokeDataOffsets7B.IsGiftFound, 1, token).ConfigureAwait(false))[0] > 0;

        public async Task<uint> EncounteredWild(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(CatchingSpecies, 2, token).ConfigureAwait(false), 0);

        public async Task<bool> IsNatureTellerEnabled(CancellationToken token) => (await Connection.ReadBytesAsync(NatureTellerEnabled, 1, token).ConfigureAwait(false))[0] == 0x04;

        public async Task<Nature> ReadWildNature(CancellationToken token) => (Nature)BitConverter.ToUInt16(await Connection.ReadBytesAsync(WildNature, 2, token).ConfigureAwait(false), 0);

        public async Task EnableNatureTeller(CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes(0x04), NatureTellerEnabled, token).ConfigureAwait(false);

        public async Task EditWildNature(Nature target, CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes((uint)target), WildNature, token).ConfigureAwait(false);

        public async Task<Lure> ReadLureType(CancellationToken token) => (Lure)BitConverter.ToUInt16(await Connection.ReadBytesAsync(LureType, 2, token).ConfigureAwait(false), 0);

        public async Task<uint> ReadLureCounter(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(LureCounter, 2, token).ConfigureAwait(false), 0);

        public async Task EditLureType(uint type, CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes(type), LureType, token).ConfigureAwait(false);

        public async Task EditLureCounter(uint counter, CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes(counter), LureCounter, token).ConfigureAwait(false);

        public async Task<uint> ReadSpeciesCombo(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(SpeciesCombo, 2, token).ConfigureAwait(false), 0);

        public async Task<uint> ReadComboCount(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(CatchCombo, 2, token).ConfigureAwait(false), 0);

        public async Task EditSpeciesCombo(uint species, CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes(species), SpeciesCombo, token).ConfigureAwait(false);

        public async Task EditComboCount(uint count, CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes(count), CatchCombo, token).ConfigureAwait(false);

        public async Task<int> ReadLastSpawn(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawn, 2, token).ConfigureAwait(false), 0);

        public async Task WipeLastSpawn(CancellationToken token) => await Connection.WriteBytesAsync(new byte[] { 0x0, 0x0 }, LastSpawn, token).ConfigureAwait(false);

        public async Task<uint> ReadSpawnFlags(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawnFlags, 2, token).ConfigureAwait(false), 0);

        public async Task<TextSpeed> ReadTextSpeed(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
            return (TextSpeed)(data[0] & 3);
        }

        public async Task EditTextSpeed(TextSpeed speed, CancellationToken token)
        {
            var textSpeedByte = await Connection.ReadBytesAsync(TextSpeedOffset, 1, token).ConfigureAwait(false);
            var data = new[] { (byte)((textSpeedByte[0] & 0xFC) | (int)speed) };
            await Connection.WriteBytesAsync(data, TextSpeedOffset, token).ConfigureAwait(false);
        }

        public async Task FleeToOverworld(CancellationToken token)
        {
            while (!await IsInConfirmDialog(token).ConfigureAwait(false) && !token.IsCancellationRequested)
                await Click(B, 1_200, token).ConfigureAwait(false);
            await Click(A, 1_000, token).ConfigureAwait(false);
            while (await IsInCatchScreen(token).ConfigureAwait(false) && !token.IsCancellationRequested) { }
            Log($"Exited wild encounter.");
        }

        public async Task OpenGame(PokeBotHubConfig config, CancellationToken token)
        {
            // Open game.
            await Click(A, 1_500 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (config.Timings.AvoidSystemUpdate)
                await Click(DUP, 0_600, token).ConfigureAwait(false);

            await Click(A, 2_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            while (await IsInTitleScreen(token).ConfigureAwait(false))
            {
                if (stopwatch.ElapsedMilliseconds > 6000)
                    await DetachController(token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);
            }
            Log("Game started.");
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

        public async Task<GameVersion> CheckGameVersion(CancellationToken token)
        {
            var data = await Connection.ReadBytesAsync(LGGameVersion, 1, token).ConfigureAwait(false);
            if (data[0] == 0x01)
                return GameVersion.GP;
            else if (data[0] == 0x02)
                return GameVersion.GE;
            else
                return GameVersion.Invalid;
        }

        public async Task<SAV7b> IdentifyTrainer(CancellationToken token)
        {
            Log("Grabbing trainer data of host console...");
            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            InitSaveData(sav);

            return sav;
        }

        public async Task<SAV7b> GetFakeTrainerSAV(CancellationToken token)
        {
            var sav = new SAV7b();
            byte[] dest = sav.Blocks.Status.Data;
            int startofs = sav.Blocks.Status.Offset;
            byte[]? data = await Connection.ReadBytesAsync(TrainerData, TrainerSize, token).ConfigureAwait(false);
            data.CopyTo(dest, startofs);
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
            await SetDeviceType(HidDeviceType.JoyRight1, token).ConfigureAwait(false);
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
    }
}
