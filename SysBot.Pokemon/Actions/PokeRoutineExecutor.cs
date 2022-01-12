using System;
using System.Collections.Generic;
using PKHeX.Core;
using SysBot.Base;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutor<T> : PokeRoutineExecutorBase where T : PKM, new()
    {
        protected PokeRoutineExecutor(IConsoleBotManaged<IConsoleConnection, IConsoleConnectionAsync> cfg) : base(cfg)
        {
        }

        public abstract Task<T> ReadPokemon(ulong offset, CancellationToken token);
        public abstract Task<T> ReadPokemon(ulong offset, int size, CancellationToken token);
        public abstract Task<T> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token);
        public abstract Task<T> ReadBoxPokemon(int box, int slot, CancellationToken token);

        public async Task<T?> ReadUntilPresent(ulong offset, int waitms, int waitInterval, int size, CancellationToken token)
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

        public async Task<T?> ReadUntilPresentPointer(IReadOnlyList<long> jumps, int waitms, int waitInterval, int size, CancellationToken token)
        {
            int msWaited = 0;
            while (msWaited < waitms)
            {
                var pk = await ReadPokemonPointer(jumps, size, token).ConfigureAwait(false);
                if (pk.Species != 0 && pk.ChecksumValid)
                    return pk;
                await Task.Delay(waitInterval, token).ConfigureAwait(false);
                msWaited += waitInterval;
            }
            return null;
        }

        public async Task<uint> GetAddressFromPointer(IReadOnlyList<long> jumps, CancellationToken token)
		{
            ulong absoluteaddress = await SwitchConnection.PointerAll(jumps, token).ConfigureAwait(false);
            ulong heapaddress = await SwitchConnection.GetHeapBaseAsync(token).ConfigureAwait(false);
            return (uint)(absoluteaddress - heapaddress);
        }

        protected async Task<(bool, ulong)> ValidatePointerAll(IEnumerable<long> jumps, CancellationToken token)
        {
            var solved = await SwitchConnection.PointerAll(jumps, token).ConfigureAwait(false);
            return (solved != 0, solved);
        }

        protected async Task<ulong> PointerAll(IEnumerable<long> jumps, CancellationToken token)
        {
            byte[] command = Encoding.UTF8.GetBytes($"pointerAll{string.Concat(jumps.Select(z => $" {z}"))}\r\n");
            byte[] socketReturn = await SwitchConnection.ReadRaw(command, (sizeof(ulong) * 2) + 1, token).ConfigureAwait(false);
            var bytes = Base.Decoder.ConvertHexByteStringToBytes(socketReturn);
            Array.Reverse(bytes);

            return BitConverter.ToUInt64(bytes, 0);
        }

        public static void DumpPokemon(string folder, string subfolder, T pk)
        {
            if (!Directory.Exists(folder))
                return;
            var dir = Path.Combine(folder, subfolder);
            Directory.CreateDirectory(dir);
            var fn = Path.Combine(dir, Util.CleanFileName(pk.FileName));
            File.WriteAllBytes(fn, pk.DecryptedPartyData);
            LogUtil.LogInfo($"Saved file: {fn}", "Dump");
        }

        public List<int[]> ParseMovements(string moves, int up_ms, int right_ms, int down_ms, int left_ms)
        {
            var buttons = new List<int[]>();
            var movements = moves.ToUpper() + ",";
            var index = 0;
            var word = "";

            while (index < movements.Length - 1)
            {
                if ((movements.Length > 1 && (movements[index + 1] == ',' || movements[index + 1] == '.')) || movements.Length == 1)
                {
                    word += movements[index];
                    if (word.Equals("UP"))
                        buttons.Add(new int[] { 0, 30_000, up_ms });
                    else if (word.Equals("RIGHT"))
                        buttons.Add(new int[] { 30_000, 0, right_ms });
                    else if (word.Equals("DOWN"))
                        buttons.Add(new int[] { 0, -30_000, down_ms });
                    else if (word.Equals("LEFT"))
                        buttons.Add(new int[] { -30_000, 0, left_ms });
                    movements.Remove(0, 1);
                    word = "";
                }
                else if (movements[index] == ',' || movements[index] == '.' || movements[index] == ' ' || movements[index] == '\n' || movements[index] == '\t' || movements[index] == '\0')
                    movements.Remove(0, 1);
                else
                {
                    word += movements[index];
                    movements.Remove(0, 1);
                }
                index++;
            }

            return buttons;
        }
    }
}