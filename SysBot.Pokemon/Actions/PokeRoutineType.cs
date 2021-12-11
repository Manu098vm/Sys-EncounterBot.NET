namespace SysBot.Pokemon
{
    /// <summary>
    /// Type of routine the Bot carries out.
    /// </summary>
    public enum PokeRoutineType
    {
        Idle = 0,
        LGPE_OverworldScan = 1,
        LGPE_EncounterBot = 2,
        SWSH_OverworldScan = 3,
        SWSH_EncounterBot = 4,
        SWSH_MaxLair = 5,
        SWSH_EggBot = 6,
        SWSH_FossilBot = 7,
        BDSP_RNG = 8,
        RemoteControl = 9,
    }
}
