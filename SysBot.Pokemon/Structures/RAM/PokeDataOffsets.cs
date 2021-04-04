using System;

namespace SysBot.Pokemon
{
    public static class PokeDataOffsets
    {
        public const uint BoxStartOffset = 0x45075880;
        public const uint CurrentBoxOffset = 0x450C680E;
        public const uint TrainerDataOffset = 0x45068F18;
        public const uint IsConnectedOffset = 0x30C7CCA8;
        public const uint TextSpeedOffset = 0x450690A0;
        public const uint ItemTreasureAddress = 0x45068970;
        public const uint demageOutputOffset = 0x007E37F0;
        public const uint LairSpeciesSelector = 0x50B129A0;
        public const uint LairSpeciesSelector2 = 0x50B12278;
        public const uint LairRewardsScreenBytes = 0xFFAE2FC6; //Thanks Koi!
        public const uint KCoordinatesBlock = 0x4505B3C0;
        public const uint WildAreaMotostokeSpawns = 0x4505C3C0;
        public const uint IsleOfArmorStationSpaws = 0x4505C9C0;
        public const uint CrownTundraSnowslideSlopeSpawns = 0x4505EE80;
        public const uint IsArticunoInSnowslide = 0x50B0EE68;
        public const uint IsArticunoInSnowslide2 = 0x72B64CE4;
        public const uint IsArticunoInSnowslide3 = 0x72B65044;

        // Raid Offsets
        // The dex number of the Pokémon the host currently has chosen. 
        // Details for each player span 0x30, so add 0x30 to get to the next offset.
        public const uint RaidP0PokemonOffset = 0x8398A294;
        // Add to each Pokémon offset.  AltForm used.
        public const uint RaidAltFormInc = 0x4;
        // Add to each Pokémon offset.  0 = male, 1 = female, 2 = genderless.
        public const uint RaidGenderIncr = 0x8;
        // Add to each Pokémon offset.  Bool for whether the Pokémon is shiny.
        public const uint RaidShinyIncr = 0xC;
        // Add to each Pokémon offset.  Bool for whether they have locked in their Pokémon.
        public const uint RaidLockedInIncr = 0x1C;
        public const uint RaidBossOffset = 0x8398A25C;

        // 0 when not in a battle or raid, 0x40 or 0x41 otherwise.
        public const uint InBattleRaidOffsetSW = 0x3F128624;
        public const uint InBattleRaidOffsetSH = 0x3F128626;

        // Pokémon Encounter Offsets
        public const uint WildPokemonOffset = 0x8FEA3648;
        public const uint RaidPokemonOffset = 0x886A95B8;
        public const uint LegendaryPokemonOffset = 0x886BC348;

        /* Wild Area Daycare */
        public const uint DayCare_Wildarea_Step_Counter = 0x4511FC54;
        public const uint DayCare_Wildarea_Egg_Is_Ready = 0x4511FC60;

        /* Route 5 Daycare */
        public const uint DayCare_Route5_Step_Counter = 0x4511F99C;
        public const uint DayCare_Route5_Egg_Is_Ready = 0x4511F9A8;

        public const int BoxFormatSlotSize = 0x158;
        public const int TrainerDataLength = 0x110;

        #region ScreenDetection
        // CurrentScreenOffset can be unreliable for Overworld; this one is 1 on Overworld and 0 otherwise.
        // Varies based on console language which is configured in Hub.
        // Default setting works for English, Dutch, Portuguese, and Russian
        public const uint OverworldOffset = 0x2F770638;
        public const uint OverworldOffsetFrench = 0x2F770828;
        public const uint OverworldOffsetGerman = 0x2F770908;
        public const uint OverworldOffsetSpanish = 0x2F7707F8;
        public const uint OverworldOffsetItalian = 0x2F7705B8;
        public const uint OverworldOffsetJapanese = 0x2F770798;
        public const uint OverworldOffsetChineseT = 0x2F76F7D8;
        public const uint OverworldOffsetChineseS = 0x2F76F838;
        public const uint OverworldOffsetKorean = 0x2F76FC38;
        public const uint CurrentScreenLairOffset = 0x6B582760; //Thanks Koi!

        // For detecting when we're on the in-battle menu. 
        public const uint BattleMenuOffset = 0x6B578EDC;

        // Original screen detection offset.
        public const uint CurrentScreenOffset = 0x6B30FA00;
        public const uint CurrentLairScreenOffset = 0x6B30FAC0;

        // Value goes between either of these; not game or area specific.
        public const uint CurrentScreen_Overworld1 = 0xFFFF5127;
        public const uint CurrentScreen_Overworld2 = 0xFFFFFFFF;

        public const uint CurrentScreen_Box1 = 0xFF00D59B;
        public const uint CurrentScreen_Box2 = 0xFF000000;
        public const uint CurrentScreen_Box_WaitingForOffer = 0xC800B483;
        public const uint CurrentScreen_Box_ConfirmOffer = 0xFF00B483;

        public const uint CurrentScreen_Softban = 0xFF000000;

        //public const uint CurrentScreen_YMenu = 0xFFFF7983;
        public const uint CurrentScreen_RaidParty = 0xFF1461DB;

        public const uint CurrentScreen_LairMenu = 0xFFAC2CC4;

        //Pointers
        public const string LairReward = "[[[[main+28F4060]+1B0]+68]+58]+D0";
        public static string PokeGift = "[[[[main+28F4060]+208]+08]+58]";

        #endregion

        public static uint GetDaycareStepCounterOffset(Enumerations daycare)
        {
            return daycare switch
            {
                Enumerations.WildArea => DayCare_Wildarea_Step_Counter,
                Enumerations.Route5 => DayCare_Route5_Step_Counter,
                _ => throw new ArgumentException(nameof(daycare)),
            };
        }

        public static uint GetDaycareEggIsReadyOffset(Enumerations daycare)
        {
            return daycare switch
            {
                Enumerations.WildArea => DayCare_Wildarea_Egg_Is_Ready,
                Enumerations.Route5 => DayCare_Route5_Egg_Is_Ready,
                _ => throw new ArgumentException(nameof(daycare)),
            };
        }
    }
}
