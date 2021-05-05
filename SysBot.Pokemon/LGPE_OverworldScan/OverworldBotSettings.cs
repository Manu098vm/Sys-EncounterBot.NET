using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class OverworldBotSettings
    {
        private const string LetsGoScan = nameof(LetsGoScan);
        public override string ToString() => "Encounter Bot Settings";

        [Category(LetsGoScan), Description("The method by which the bot will scan for Pokémon. Overworld and WildBirds method can only check for Species and (any) Shinyness. Level, IVs, Nature and Square Shinies cannot be verified.")]
        public LGPEOverworldMode Routine { get; set; } = LGPEOverworldMode.OverworldSpawn;

        [Category(LetsGoScan), Description("Standard StopConditions cannot be checked with LGPE overworld bot. Please select here the Species you want to hunt. Ignored if set to \"None\" or if the routine is set to \"WildBirds\"")]
        public LGPESpecies StopOnSpecies { get; set; }

        [Category(LetsGoScan), Description("Standard StopConditions cannot be checked with LGPE overworld bot. Only stop the routine if the wanted species is Shiny. Ignored if false.")]
        public bool OnlyShiny { get; set; } = true;

        [Category(LetsGoScan), Description("Force the current chain to be on the selected Species. Ignored if None.")]
        public LGPESpecies ChainSpecies { get; set; }

        [Category(LetsGoScan), Description("Force the current chain to be at the entered value. Ignored if 0.")]
        public int ChainCount { get; set; } = 0;

        [Category(LetsGoScan), Description("Examples: \"UP, RIGHT\", \"RIGHT, LEFT, UP\", etc. Every movement MUST be separated with a comma (\",\"). Ignored if empty.")]
        public string MovementOrder { get; set; } = string.Empty;

        [Category(LetsGoScan), Description("Indicates how long the character will move north during the scans.")]
        public int MoveUpMs { get; set; } = 5000;

        [Category(LetsGoScan), Description("Indicates how long the character will move east during the scans.")]
        public int MoveRightMs { get; set; } = 5000;

        [Category(LetsGoScan), Description("Indicates how long the character will move south during the scans.")]
        public int MoveDownMs { get; set; } = 5000;

        [Category(LetsGoScan), Description("Indicates how long the character will move west during the scans.")]
        public int MoveLeftMs { get; set; } = 5000;

        [Category(LetsGoScan), Description("Set the test you want to attempt. Ignore unless the routine is set to \"TestRoutine\".")]
        public LetsGoTest TestRoutine { get; set; } = LetsGoTest.Unfreeze;

        [Category(LetsGoScan), Description("Set how many attempt the bot will do to check the freezing values. Infinite checking if 0.")]
        public int FreezingTestCount { get; set; } = 10;

        [Category(LetsGoScan), Description("Edit this value in case you have false report of a Shiny appearing in the overworld. You can find the correct value for your console through TestOffsets Method.")]
        public long MaxMs { get; set; } = 2500;
    }
}