﻿using PKHeX.Core;

namespace Sysbot.Pokemon
{
    /// <summary>
    /// Contains logic for the Generation 8 (SW/SH) overworld spawns that walk around the overworld.
    /// </summary>
    /// <remarks>
    /// Entities spawned into the overworld that can be encountered are assigned a 32bit seed, which can be immediately derived from the <see cref="PKM.EncryptionConstant"/>.
    /// </remarks>
    public static class RNG8
    {
        private const int UNSET = -1;
        public static PK8 CalculateFromSeed(PK8 pk, SysBot.Pokemon.Shiny shiny, int flawless, uint seed)
        {
            var xoro = new Xoroshiro128Plus(seed);

            // Encryption Constant
            pk.EncryptionConstant = (uint)xoro.NextInt(uint.MaxValue);

            // PID
            var pid = (uint)xoro.NextInt(uint.MaxValue);
            if (shiny == SysBot.Pokemon.Shiny.Never)
            {
                if (GetIsShiny(pk.TID, pk.SID, pid))
                    pid ^= 0x1000_0000;
            }
            else if (shiny != SysBot.Pokemon.Shiny.Random)
            {
                if (!GetIsShiny(pk.TID, pk.SID, pid))
                    pid = GetShinyPID(pk.TID, pk.SID, pid, 0);
            }

            pk.PID = pid;

            // IVs
            var ivs = new[] { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
            const int MAX = 31;
            for (int i = 0; i < flawless; i++)
            {
                int index;
                do { index = (int)xoro.NextInt(6); }
                while (ivs[index] != UNSET);

                ivs[index] = MAX;
            }

            for (int i = 0; i < ivs.Length; i++)
            {
                if (ivs[i] == UNSET)
                    ivs[i] = (int)xoro.NextInt(32);
            }

            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            return pk;
        }

        private static uint GetShinyPID(int tid, int sid, uint pid, int type)
        {
            return (uint)(((tid ^ sid ^ (pid & 0xFFFF) ^ type) << 16) | (pid & 0xFFFF));
        }

        private static bool GetIsShiny(int tid, int sid, uint pid)
        {
            return GetShinyXor(pid, (uint)((sid << 16) | tid)) < 16;
        }

        private static uint GetShinyXor(uint pid, uint oid)
        {
            var xor = pid ^ oid;
            return (xor ^ (xor >> 16)) & 0xFFFF;
        }
    }
}
