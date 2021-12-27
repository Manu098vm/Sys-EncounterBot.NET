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
        private readonly PokeDataPointers7B Pointers = new();
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

        public async Task<bool> IsInTitleScreen(CancellationToken token) => !((await SwitchConnection.ReadBytesMainAsync(PokeDataOffsets7B.IsInTitleScreen, 1, token).ConfigureAwait(false))[0] == 1);

        public async Task<bool> IsInBattle(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInBattleScenario, 1, token).ConfigureAwait(false))[0] > 0;

        public async Task<bool> IsInCatchScreen(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(IsInOverworld, 1, token).ConfigureAwait(false))[0] != 0;

        public async Task<bool> IsInTrade(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(PokeDataOffsets7B.IsInTrade, 1, token).ConfigureAwait(false))[0] != 0;

        public async Task<bool> IsGiftFound(CancellationToken token) => (await SwitchConnection.ReadBytesMainAsync(PokeDataOffsets7B.IsGiftFound, 1, token).ConfigureAwait(false))[0] > 0;

        public async Task<uint> EncounteredWild(CancellationToken token) => BitConverter.ToUInt16(await Connection.ReadBytesAsync(CatchingSpecies, 2, token).ConfigureAwait(false), 0);

        public async Task<bool> IsNatureTellerEnabled(CancellationToken token) => (await Connection.ReadBytesAsync(NatureTellerEnabled, 1, token).ConfigureAwait(false))[0] == 0x04;

        public async Task<Nature> ReadWildNature(CancellationToken token) => (Nature)BitConverter.ToUInt16(await Connection.ReadBytesAsync(WildNature, 2, token).ConfigureAwait(false), 0);

        public async Task EnableNatureTeller(CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes(0x04), NatureTellerEnabled, token).ConfigureAwait(false);

        public async Task EditWildNature(Nature target, CancellationToken token) => await Connection.WriteBytesAsync(BitConverter.GetBytes((uint)target), WildNature, token).ConfigureAwait(false);

        public async Task<uint> ReadSpeciesCombo(CancellationToken token) =>
            BitConverter.ToUInt16(await SwitchConnection.PointerPeek(2, Pointers.SpeciesComboPointer, token).ConfigureAwait(false), 0);

        public async Task<uint> ReadComboCount(CancellationToken token) =>
            BitConverter.ToUInt16(await SwitchConnection.PointerPeek(2, Pointers.CatchComboPointer, token).ConfigureAwait(false), 0);

        public async Task EditSpeciesCombo(uint species, CancellationToken token) =>
            await SwitchConnection.PointerPoke(BitConverter.GetBytes(species), Pointers.SpeciesComboPointer, token).ConfigureAwait(false);

        public async Task EditComboCount(uint count, CancellationToken token) =>
            await SwitchConnection.PointerPoke(BitConverter.GetBytes(count), Pointers.CatchComboPointer, token).ConfigureAwait(false);

        public async Task<long> CountMilliseconds(PokeBotHubConfig config, CancellationToken token)
        {
            var WaitMS = config.LGPE_OverworldScan.MaxMs;
            var stuck = false;
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var data = await SwitchConnection.ReadBytesMainAsync(FreezedValue, 1, token).ConfigureAwait(false);
            var comparison = data;
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
        public async Task Zaksabeast(CancellationToken token, GameVersion version)
        {
            var offset = version == GameVersion.GP ? PGeneratingFunction1 : EGeneratingFunction1;
            //This is basically the Zaksabeast code ported for the newest Let's game version. 
            var inject = new byte[] { 0xE9, 0x03, 0x00, 0x2A, 0x60, 0x12, 0x40, 0xB9, 0xE1, 0x03, 0x09, 0x2A, 0x69, 0x06, 0x00, 0xF9, 0xDC, 0xFD, 0xFF, 0x97, 0x40, 0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(inject, offset, token).ConfigureAwait(false);
        }
        public async Task Unfreeze(CancellationToken token, GameVersion version)
        {
            var offset = version == GameVersion.GP ? PGeneratingFunction7 : EGeneratingFunction7;
            var data = new byte[] { 0x0C, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(data, offset, token).ConfigureAwait(false);
        }
        public async Task ForceShiny(CancellationToken token, GameVersion version)
        {
            var offset = version == GameVersion.GP ? PShinyValue : EShinyValue;
            //100% Shiny Odds
            var inject = new byte[] { 0x27, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(inject, offset, token).ConfigureAwait(false);
        }
        public async Task NormalShiny(CancellationToken token, GameVersion version)
        {
            var offset = version == GameVersion.GP ? PShinyValue : EShinyValue;
            //Standard shiny odds
            var inject = new byte[] { 0xE0, 0x02, 0x00, 0x54 };
            await SwitchConnection.WriteBytesMainAsync(inject, offset, token).ConfigureAwait(false);
        }

        public async Task OpenGame(PokeBotHubConfig config, CancellationToken token)
        {
            // Open game.
            await Click(A, 1_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (config.Timings.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000 + config.Timings.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

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
