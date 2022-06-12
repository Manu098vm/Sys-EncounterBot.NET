﻿namespace SysBot.Pokemon
{
    public static class PokeDataOffsets7B
    {
        public const int BoxFormatSlotSize = 0x104;
        public const int TrainerSize = 0x168;
        public const uint FreezedValue = 0x1610EE0; //1 byte
        public const uint IsInOverworld = 0x163F694; //main
        public const uint IsInBattleScenario = 0x1EE067C; //main
        public const uint IsInTitleScreen = 0x160D4E0; //main
        public const uint IsInTrade = 0x1614F28; //main
        public const uint IsGiftFound = 0x1615928; //main
        public const uint StationaryBattleData = 0x9A118D68; //heap
        public const uint PokeData = 0x163EDC0; //main
        public const uint LastSpawn = 0x5E12B148; //heap
        public const uint EShinyValue = 0x7398C4; //main
        public const uint PShinyValue = 0x739864; //main
        public const uint PGeneratingFunction = 0x7398D0; //main
        public const uint EGeneratingFunction = 0x739930; //main
        public const uint TrainerData = 0x53582030; //heap
        public const uint BoxSlot1 = 0x533675B0; //heap
        public const uint Money = 0x53324108; //heap
        public const uint NatureTellerEnabled = 0x53405CF8; //heap, 0 random nature, 4 set nature
        public const uint WildNature = 0x53404C10; //heap
        public const uint LGGameVersion = 0x53321DA8; //heap 0x1 = pika, 0x2 = eevee - Thanks Lincoln-LM!
        public const uint CatchingSpecies = 0x9A264598; //heap - Thanks Lincoln-LM!
        public const uint CatchCombo = 0x5E1CF500; //heap - Thanks Lincoln-LM!
        public const uint SpeciesCombo = 0x5E1CF4F8; //heap - Thanks Lincoln-LM!
        public const uint LureType = 0x53405D28; //Thanks Anubis!
        public const uint LureCounter = 0x53405D2A; //Thanks Anubis!
        public const uint TextSpeedOffset = 0x53321EDC; //Thanks Anubis!
    }
}