using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public class PokeDataOffsetsBS_BD : BasePokeDataOffsetsBS
    {
        public override IReadOnlyList<long> MainRNGState { get; } = new long[] { 0x4FD43D0, 0x0 };
        public override IReadOnlyList<long> R1_SpeciesPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x2A0, 0x2C };
        public override IReadOnlyList<long> R2_SpeciesPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x2A0, 0x4C };
        public override IReadOnlyList<long> R1_SeedPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x2A0, 0x24 };
        public override IReadOnlyList<long> R2_SeedPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x2A0, 0x44 };
        public override IReadOnlyList<long> EggSeedPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x450 };
        public override IReadOnlyList<long> EggStepPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x458 };
        public override IReadOnlyList<long> LocationPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x40 };
        public override IReadOnlyList<long> PartyStartPokemonPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x808, 0x10, 0x20, 0x20, 0x18, 0x20 };
        public override IReadOnlyList<long> PartySlot2PokemonPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x808, 0x10, 0x28, 0x20, 0x18, 0x20 };
        public override IReadOnlyList<long> BoxStartPokemonPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0xA0, 0x20, 0x20, 0x20 };
        public override IReadOnlyList<long> OpponentPokemonPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0x800, 0x58, 0x28, 0x10, 0x20, 0x20, 0x18, 0x20 };

        public override IReadOnlyList<long> SceneIDPointer { get; } = new long[] { 0x4C59B50, 0xB8, 0x18 };
        public override IReadOnlyList<long> DayTimePointer { get; } = new long[] { 0x4C59B50, 0xB8, 0x0, 0x60, 0x100 };

        public override IReadOnlyList<long> MyStatusTrainerPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0xE0, 0x0 };
        public override IReadOnlyList<long> MyStatusTIDPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0xE8 };
        public override IReadOnlyList<long> ConfigTextSpeedPointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0xA8 };
        public override IReadOnlyList<long> ConfigLanguagePointer { get; } = new long[] { 0x4C64DC0, 0xB8, 0x10, 0xAC };
    }
}