/* Special thanks to the RNG Researchers in the PokémonRNG community.
 * Particular credits for the RNG researches to
 * Zaksabeast, Real.96, EzPzStreamz, ShinySylveon, AdmiralFish, Lincoln-LM, Kaphotics.
 * Thanks to SciresM that allowed us to make these researches in the
 * first place with his awesome tools and knowledge.
 */

using PKHeX.Core;
using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public class Xorshift
    {
        private readonly uint[] state;

        public Xorshift(ulong seed0, ulong seed1)
        {
            uint s0 = (uint)(seed0 >> 32);
            uint s1 = (uint)(seed0 & 0xFFFFFFFF);
            uint s2 = (uint)(seed1 >> 32);
            uint s3 = (uint)(seed1 & 0xFFFFFFFF);
            this.state = new uint[] { s0, s1, s2, s3 };
        }
        public Xorshift(uint s0, uint s1, uint s2, uint s3)
		{
            this.state = new uint[] { s0, s1, s2, s3 };
        }
        public ulong[] GetU64State()
        {
            ulong s_0 = ((this.state[0] | 0xFFFFFFFF00000000) << 32) | this.state[1];
            ulong s_1 = ((this.state[2] | 0xFFFFFFFF00000000) << 32) | this.state[3];
            return new ulong[] { s_0, s_1 };
        }
        public uint[] GetU32State()
		{
            return new uint[] { this.state[0], this.state[1], this.state[2], this.state[3] };
		}
        public uint Advance(int advances)
		{
            uint seed = 0x0;
            for (int i = 0; i < advances; i++)
                seed = this.Next();
            return seed;
		}
        public uint Next()
        {
            uint t = this.state[0];
            uint s = this.state[3];

            t ^= t << 11;
            t ^= t >> 8;
            t ^= s ^ (s >> 19);

            this.state[0] = this.state[1];
            this.state[1] = this.state[2];
            this.state[2] = this.state[3];
            this.state[3] = t;

            return (t % 0xffffffff) + 0x80000000;
        }

        public byte Range(byte min = 0, byte max = 100)
		{
            var s0 = this.state[0];
            this.state[0] = this.state[1];
            this.state[1] = this.state[2];
            this.state[2] = this.state[3];

            uint tmp = s0 ^ s0 << 11;
            tmp = tmp ^ tmp >> 8 ^ this.state[2] ^ this.state[2] >> 19;

            this.state[3] = tmp;

            var diff = (byte)(max - min);

            return (byte)((tmp % diff) + min);

        }
    }
    public class RNG8b
    {
        private const int UNSET = -1;
        private const int MAX = 31;
        private const int N_IV = 6;
        private const int N_ABILITY = 1;
        private const int N_GENDER = 253;
        private const int N_NATURE = 25;

        public PB8 CalculateFromSeed(PB8 pk, Shiny shiny, RNGType type, uint seed)
        {
            var xoro = new Xoroshiro128Plus8b(seed);

            var flawless = GetFlawless(type);

            pk.EncryptionConstant = seed;

            var fakeTID = xoro.NextUInt(); // fakeTID
            var pid = xoro.NextUInt();
            pid = GetRevisedPID(fakeTID, pid, pk);
            if (shiny == Shiny.Never)
            {
                if (GetIsShiny(pk.TID, pk.SID, pid))
                    pid ^= 0x1000_0000;
            }
            pk.PID = pid;

            int[] ivs = { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
            var determined = 0;
            while (determined < flawless)
            {
                var idx = xoro.NextUInt(N_IV);
                if (ivs[idx] != UNSET)
                    continue;
                ivs[idx] = MAX;
                determined++;
            }

            for (var i = 0; i < ivs.Length; i++)
            {
                if (ivs[i] == UNSET)
                    ivs[i] = (int)xoro.NextUInt(MAX + 1);
            }

            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            pk.SetAbilityIndex((int)xoro.NextUInt(2));

            pk.Nature = (int)xoro.NextUInt(N_NATURE);

            return pk;
        }

        public PB8 CalculateFromStates(PB8 pk, Shiny shiny, RNGType type, Xorshift seed, WildMode mode = WildMode.None, List<int>? slots = null)
        {
            var xoro = new Xorshift(seed.GetU64State()[0], seed.GetU64State()[1]);

            var flawless = GetFlawless(type);

            if(type is RNGType.Wild && mode != WildMode.None && slots != null)
			{
                var calc = xoro.Range(0, 100);
                var slot = CalcSlot(mode, calc);
                pk.Species = slots[(int)slot];
                //Save the slot in some available structure space
                pk.Move1 = slot; 
                xoro.Advance(84);

                if(mode is not WildMode.Grass or WildMode.Swarm)
                    xoro.Next(); //Lvl Range(0,1000)
			}

            if (type is RNGType.MysteryGift)
                xoro.Next();

            pk.EncryptionConstant = xoro.Next();

            var fakeTID = type switch
            {
                RNGType.MysteryGift => (uint)0x0,
                _ => xoro.Next(),
            };

            var pid = xoro.Next();

            if (type is not RNGType.MysteryGift)
                pid = GetRevisedPID(fakeTID, pid, pk);
            
            if (shiny == Shiny.Never)
            {
                if (GetIsShiny(pk.TID, pk.SID, pid))
                    pid ^= 0x1000_0000;
            }
            pk.PID = pid;

            int[] ivs = { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
            
            var determined = 0;
            while (determined < flawless)
            {
                var idx = xoro.Next()%N_IV;
                if (ivs[idx] != UNSET)
                    continue;
                ivs[idx] = MAX;
                determined++;
            }

            for (var i = 0; i < ivs.Length; i++)
            {
                if (ivs[i] == UNSET)
                    ivs[i] = (int)(xoro.Next()&MAX);
            }

            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            if (type is not RNGType.MysteryGift)
            {

                pk.SetAbilityIndex((int)(xoro.Next()&N_ABILITY));

                //If unown Next()%28

                var genderRatio = PersonalTable.BDSP.GetFormEntry(pk.Species, pk.Form).Gender;
                if (genderRatio == PersonalInfo.RatioMagicGenderless)
                    pk.Gender = (int)Gender.Genderless;
                else if (genderRatio == PersonalInfo.RatioMagicMale)
                    pk.Gender = (int)Gender.Male;
                else if (genderRatio == PersonalInfo.RatioMagicFemale)
                    pk.Gender = (int)Gender.Female;
                else
                    pk.Gender = (((int)(xoro.Next()%N_GENDER)) + 1 < genderRatio) ? 1 : 0;
            }

            pk.Nature = (int)(Nature)(xoro.Next()%N_NATURE);

            return pk;
        }
        private static uint GetRevisedPID(uint fakeTID, uint pid, ITrainerID tr)
        {
            var xor = GetShinyXor(pid, fakeTID);
            var newXor = GetShinyXor(pid, (uint)(tr.TID | (tr.SID << 16)));

            var fakeRare = GetRareType(xor);
            var newRare = GetRareType(newXor);

            if (fakeRare == newRare)
                return pid;

            var isShiny = xor < 16;
            if (isShiny)
                return (((uint)(tr.TID ^ tr.SID) ^ (pid & 0xFFFF) ^ (xor == 0 ? 0u : 1u)) << 16) | (pid & 0xFFFF);
            return pid ^ 0x1000_0000;
        }

        private static Shiny GetRareType(uint xor) => xor switch
        {
            0 => Shiny.AlwaysSquare,
            < 16 => Shiny.AlwaysStar,
            _ => Shiny.Never,
        };

        private static int GetFlawless(RNGType type)
		{
            return type switch
            {
                RNGType.Legendary or RNGType.MysteryGift or RNGType.Roamer => 3,
                _ => 0,
            };  
		}

        private static bool GetIsShiny(int tid, int sid, uint pid)
        {
            return GetIsShiny(pid, (uint)((sid << 16) | tid));
        }

        private static bool GetIsShiny(uint pid, uint oid) => GetShinyXor(pid, oid) < 16;

        private static uint GetShinyXor(uint pid, uint oid)
        {
            var xor = pid ^ oid;
            return (xor ^ (xor >> 16)) & 0xFFFF;
        }

        //Thanks to Admiral Fish and his Pokéfinder
        private byte CalcSlot(WildMode mode, byte rand)
		{
            var calc = mode switch
            {
                WildMode.GoodRod or WildMode.SuperRod => new byte[5] { 40, 80, 95, 99, 100 },
                WildMode.OldRod or WildMode.Surf => new byte[5] { 60, 90, 95, 99, 100 },
                _ => new byte[12] { 20, 40, 50, 60, 70, 80, 85, 90, 94, 98, 99, 100 },
            };
            byte i;
            for (i = 0; i < calc.Length; i++)
                if (rand < calc[i])
                    return i;
            return 255;
		}
    }
}