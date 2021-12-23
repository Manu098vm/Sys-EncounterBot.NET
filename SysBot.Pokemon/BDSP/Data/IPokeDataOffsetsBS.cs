using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public interface IPokeDataOffsetsBS
    {
        public IReadOnlyList<long> MainRNGState { get; }
        public IReadOnlyList<long> RoamerSeedPointer { get; }
        public IReadOnlyList<long> EggSeedPointer { get; }
        public IReadOnlyList<long> EggStepPointer { get; }
        public IReadOnlyList<long> LocationPointer { get; }
        public IReadOnlyList<long> PartyStartPokemonPointer { get; }
        public IReadOnlyList<long> BoxStartPokemonPointer { get; }
        public IReadOnlyList<long> OpponentPokemonPointer { get; }
        public IReadOnlyList<long> SceneIDPointer { get; }
        public IReadOnlyList<long> DayTimePointer { get; }
        public IReadOnlyList<long> MyStatusTrainerPointer { get; }
        public IReadOnlyList<long> MyStatusTIDPointer { get; }
        public IReadOnlyList<long> ConfigTextSpeedPointer { get; }
        public IReadOnlyList<long> ConfigLanguagePointer { get; }
    }
}
