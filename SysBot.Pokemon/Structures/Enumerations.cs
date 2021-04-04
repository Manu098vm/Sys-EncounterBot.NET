namespace SysBot.Pokemon
{
    public enum Enumerations
    {
        WildArea,
        Route5,
    }

    public enum EncounterType
    {
        None = 0,
        Regis = 1,
        Regigigas = 2,
        Spiritomb = 3,
        SoJ = 4,
        Eternatus = 5,
        Keldeo = 6,
        Articuno = 7,
        Zapdos = 8,
        Moltres = 9,
        Wailord = 10,
        OverworldAny = 11,
    }

    public enum TargetShinyType
    {
        DisableOption,  // Doesn't care
        NonShiny,       // Match nonshiny only
        AnyShiny,       // Match any shiny regardless of type
        StarOnly,       // Match star shiny only
        SquareOnly,     // Match square shiny only
    }

    public enum MarkIndex
    {
        None = 0,   //Doesn't care
        Any = 1,    //Match any Mark regardless of which
        MarkLunchtime = 53,
        MarkSleepyTime = 54,
        MarkDusk = 55,
        MarkDawn = 56,
        MarkCloudy = 57,
        MarkRainy = 58,
        MarkStormy = 59,
        MarkSnowy = 60,
        MarkBlizzard = 61,
        MarkDry = 62,
        MarkSandstorm = 63,
        MarkMisty = 64,
        MarkDestiny = 65,
        MarkFishing = 66,
        MarkCurry = 67,
        MarkUncommon = 68,
        MarkRare = 69,
        MarkRowdy = 70,
        MarkAbsentMinded = 71,
        MarkJittery = 72,
        MarkExcited = 73,
        MarkCharismatic = 74,
        MarkCalmness = 75,
        MarkIntense = 76,
        MarkZonedOut = 77,
        MarkJoyful = 78,
        MarkAngry = 79,
        MarkSmiley = 80,
        MarkTeary = 81,
        MarkUpbeat = 82,
        MarkPeeved = 83,
        MarkIntellectual = 84,
        MarkFerocious = 85,
        MarkCrafty = 86,
        MarkScowling = 87,
        MarkKindly = 88,
        MarkFlustered = 89,
        MarkPumpedUp = 90,
        MarkZeroEnergy = 91,
        MarkPrideful = 92,
        MarkUnsure = 93,
        MarkHumble = 94,
        MarkThorny = 95,
        MarkVigor = 96,
        MarkSlump = 97
    }
}
