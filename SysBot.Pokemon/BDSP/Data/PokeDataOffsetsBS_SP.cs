using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public class PokeDataOffsetsBS_SP : BasePokeDataOffsetsBS
    {
        public override IReadOnlyList<long> MainRNGState { get; } = new long[] { 0x4FB2050, 0x0 };
        public override IReadOnlyList<long> RoamerSeedPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0x2A0, 0x24 };
        public override IReadOnlyList<long> EggSeedPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0x460 };
        public override IReadOnlyList<long> EggStepPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0x468 };
        public override IReadOnlyList<long> LocationPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0x40 };
        public override IReadOnlyList<long> PartyStartPokemonPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0x7F0, 0x10, 0x20, 0x20, 0x18, 0x20 };
        public override IReadOnlyList<long> BoxStartPokemonPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0xA0, 0x20, 0x20, 0x20 };
        public override IReadOnlyList<long> OpponentPokemonPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0x7E8, 0x58, 0x28, 0x10, 0x20, 0x20, 0x18, 0x20 };

        public override IReadOnlyList<long> SceneIDPointer { get; } = new long[] { 0x4E4EC50, 0xB8, 0x18 };
        public override IReadOnlyList<long> DayTimePointer { get; } = new long[] { 0x4E4EC50, 0xB8, 0x0, 0x60, 0x100 };

        public override IReadOnlyList<long> MyStatusTrainerPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0xE0, 0x0 };
        public override IReadOnlyList<long> MyStatusTIDPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0xE8 };
        public override IReadOnlyList<long> ConfigTextSpeedPointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0xA8 };
        public override IReadOnlyList<long> ConfigLanguagePointer { get; } = new long[] { 0x4E59E60, 0xB8, 0x10, 0xAC };
    }
}
