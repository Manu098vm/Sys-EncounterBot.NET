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
        SWSH_EggBot = 5,
        BDSP_RNG = 6,
        RemoteControl = 7,
    }
}
