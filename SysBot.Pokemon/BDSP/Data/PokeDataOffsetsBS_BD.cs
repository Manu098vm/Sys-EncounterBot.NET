using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public class PokeDataOffsetsBS_BD : BasePokeDataOffsetsBS
    {
        public override IReadOnlyList<long> MainRNGState { get; } = new long[] { 0x4F8CCD0, 0x0};
        public override IReadOnlyList<long> PlayerPrefsProvider { get; } = new long[] { 0x4E34DD0, 0xB8, 0x10 };
        public override IReadOnlyList<long> EggSeedPointer { get; } = new long[] { 0x4E34DD0, 0xB8, 0x10, 0x460 };
        public override IReadOnlyList<long> EggStepPointer { get; } = new long[] { 0x4E34DD0, 0xB8, 0x10, 0x468 };
        public override IReadOnlyList<long> LocationPointer { get; } = new long[] { 0x4E34DD0, 0xB8, 0x10, 0x40 };
        public override IReadOnlyList<long> PartyStartPokemonPointer { get; } = new long[] { 0x4E34DD0, 0xB8, 0x10, 0x7F0, 0x10, 0x20, 0x20, 0x18, 0x20 };
        public override IReadOnlyList<long> BoxStartPokemonPointer { get; } = new long[] { 0x4E34DD0, 0xB8, 0x10, 0xA0, 0x20, 0x20, 0x20 };
        public override IReadOnlyList<long> OpponentPokemonPointer { get; } = new long[] { 0x4E34DD0, 0xB8, 0x10, 0x7E8, 0x58, 0x28, 0x10, 0x20, 0x20, 0x18, 0x20 };

        public override IReadOnlyList<long> SceneIDPointer { get; } = new long[] { 0x4E29C48, 0xB8, 0x18 };

        public override IReadOnlyList<long> MainSavePointer { get; } = new long[] { 0x4C964E8, 0x20 };
        public override IReadOnlyList<long> ConfigPointer { get; } = new long[] { 0x4E34DD0, 0xB8, 0x10, 0xA8 };
    }
}
