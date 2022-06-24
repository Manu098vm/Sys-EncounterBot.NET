using System.ComponentModel;
using System.Threading;
using System.Collections.Generic;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class Overworld7BSettings : IBotStateSettings
    {
        private const string Counts = nameof(Counts);
        private const string OverworldScan = nameof(OverworldScan);
        public override string ToString() => "Overworld Scan Settings";

        [Category(OverworldScan), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        [Category(OverworldScan), Description("The method by which the bot will scan for Pokémon. Overworld and WildBirds method can only check for Species and (any) Shinyness. Level, IVs, Nature and Square Shinies cannot be verified.")]
        public LGPEOverworldMode Routine { get; set; } = LGPEOverworldMode.OverworldSpawn;

        [Category(OverworldScan), Description("Standard StopConditions cannot be checked with LGPE overworld bot. Please select here the Species you want to hunt. Ignored if set to \"None\" or if the routine is set to \"WildBirds\"")]
        public LGPESpecies StopOnSpecies { get; set; }

        [Category(OverworldScan), Description("Standard StopConditions cannot be checked with LGPE overworld bot. Only stop the routine if the wanted species is Shiny. Ignored if false.")]
        public bool OnlyShiny { get; set; } = true;

        [Category(OverworldScan), Description("Force the current chain to be on the selected Species. Ignored if None.")]
        public LGPESpecies ChainSpecies { get; set; }

        [Category(OverworldScan), Description("Force the current chain to be at the entered value. Ignored if 0.")]
        public int ChainCount { get; set; } = 0;

        [Category(OverworldScan), Description("Set the fortune teller nature. All the wild and stationary Pokémon will have this nature. Ignored if random.")]
        public PKHeX.Core.Nature SetFortuneTellerNature { get; set; } = PKHeX.Core.Nature.Random;

        [Category(OverworldScan), Description("Set the Lure type. The Lure will be reactivated once the effect ends. Ignored if None.")]
        public Lure SetLure { get; set; } = Lure.None;

        [Category(OverworldScan), Description("Example: \"UP, RIGHT\". Every movement MUST be separated with a comma. Ignored if empty. Unexpected behaviour can occur if a shiny is detected while changing area. It his recommended to avoid that.")]
        public string MovementOrder { get; set; } = string.Empty;

        [Category(OverworldScan), Description("Indicates how long the character will move north during the scans.")]
        public int MoveUpMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move east during the scans.")]
        public int MoveRightMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move south during the scans.")]
        public int MoveDownMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move west during the scans.")]
        public int MoveLeftMs { get; set; } = 5000;

        private int _completedScans;
        private int _shinyspawn;

        [Category(Counts), Description("Completed overworld Scans.")]
        public int CompletedScans
        {
            get => _completedScans;
            set => _completedScans = value;
        }

        [Category(Counts), Description("Shiny spawn count.")]
        public int ShinySpawn
        {
            get => _shinyspawn;
            set => _shinyspawn = value;
        }

        public int AddCompletedScans() => Interlocked.Increment(ref _completedScans);
        public int AddCompletedShiny() => Interlocked.Increment(ref _shinyspawn);

        public int RemoveCompletedScans() => Interlocked.Decrement(ref _completedScans);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (CompletedScans != 0)
                yield return $"Total overworld scans: {CompletedScans}";
            if (ShinySpawn != 0)
                yield return $"Total Shiny spawns: {ShinySpawn}";
        }
    }
}