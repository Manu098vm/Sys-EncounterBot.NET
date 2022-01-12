using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class RNGSettings : IBotStateSettings
    {
        private const string RNG = nameof(RNG);
        private const string Counts = nameof(Counts);
        public override string ToString() => "RNG Bot Settings";

        [Category(RNG), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        [Category(RNG), Description("Bot RNG Routine.")]
        public RNGRoutine Routine { get; set; } = RNGRoutine.DelayCalc;

        [Category(RNG), Description("RNG Generator Settings.")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public GeneratorSettings GeneratorSettings { get; set; } = new();

        [Category(RNG), Description("DelayCalc Settings.")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public DelaySettings DelayCalcSettings { get; set; } = new();

        [Category(RNG), Description("AutoRNG Settings.")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public AutoRNGSettings AutoRNGSettings { get; set; } = new();

        [Category(RNG), Description("Select the RNG you want to perform or calculate.")]
        public RNGType RNGType { get; set; } = 0;

        [Category(RNG), Description("Scenario of the target encounter.")]
        public CheckMode CheckMode { get; set; } = CheckMode.Encounter;

        [Category(RNG), Description("Select the wild encounter you want to calculate.")]
        public WildMode WildMode { get; set; } = WildMode.None;

        [Category(RNG), Description("The event Pokémon you want to RNG. Select None if events are not your target.")]
        public PokeEvents Event { get; set; } = PokeEvents.None;

        private int _completedRNGs;

        [Category(Counts), Description("Completed RNGs")]
        public int CompletedRNGs
        {
            get => _completedRNGs;
            set => _completedRNGs = value;
        }

        public void AddCompletedRNGs() => Interlocked.Increment(ref _completedRNGs);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (CompletedRNGs != 0)
                yield return $"Completed RNGs: {CompletedRNGs}";
        }
    }
}
