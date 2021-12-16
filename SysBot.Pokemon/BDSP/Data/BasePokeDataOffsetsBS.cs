using System.Collections.Generic;

namespace SysBot.Pokemon
{
    public abstract class BasePokeDataOffsetsBS : IPokeDataOffsetsBS
    {
        public const string ShiningPearlID = "010018E011D92000";
        public const string BrilliantDiamondID = "0100000011D90000";
        public abstract IReadOnlyList<long> MainRNGState { get; }
        public abstract IReadOnlyList<long> RoamerSeedPointer { get; }
        public abstract IReadOnlyList<long> EggSeedPointer { get; }
        public abstract IReadOnlyList<long> EggStepPointer { get; }
        public abstract IReadOnlyList<long> LocationPointer { get; }
        public abstract IReadOnlyList<long> DayTimePointer { get; }
        public abstract IReadOnlyList<long> PartyStartPokemonPointer { get; }
        public abstract IReadOnlyList<long> BoxStartPokemonPointer { get; }
        public abstract IReadOnlyList<long> OpponentPokemonPointer { get; }
        public abstract IReadOnlyList<long> SceneIDPointer { get; }
        public abstract IReadOnlyList<long> MainSavePointer { get; }
        public abstract IReadOnlyList<long> ConfigPointer { get; }

        // SceneID enums
        public const byte SceneID_Field = 0;
        public const byte SceneID_Room = 1;
        public const byte SceneID_Battle = 2;
        public const byte SceneID_Title = 3;
        public const byte SceneID_Opening = 4;
        public const byte SceneID_Contest = 5;
        public const byte SceneID_DigFossil = 6;
        public const byte SceneID_SealPreview = 7;
        public const byte SceneID_EvolveDemo = 8;
        public const byte SceneID_HatchDemo = 9;
        public const byte SceneID_GMS = 10;

        public const int BoxFormatSlotSize = 0x158;
    }
}
