﻿using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public interface IPokeDataOffsetsBS
    {
        public IReadOnlyList<long> MainRNGState { get; }
        public IReadOnlyList<long> PartyStartPokemonPointer { get; }
        public IReadOnlyList<long> BoxStartPokemonPointer { get; }
        public IReadOnlyList<long> OpponentPokemonPointer { get; }
        public IReadOnlyList<long> SceneIDPointer { get; }
        public IReadOnlyList<long> MainSavePointer { get; }
        public IReadOnlyList<long> ConfigPointer { get; }
    }
}
