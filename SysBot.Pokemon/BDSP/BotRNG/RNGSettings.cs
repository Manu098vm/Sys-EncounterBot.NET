using System.Collections.Generic;
using PKHeX.Core;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class RNGSettings : IBotStateSettings, ICountSettings
    {
        private const string RNG = nameof(RNG);
        private const string Counts = nameof(Counts);
        public override string ToString() => "Trade Bot Settings";

        [Category(RNG), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        [Category(RNG), Description("Bot RNG Routine.")]
        public RNGRoutine Routine { get; set; } = RNGRoutine.DelayCalc;

        [Category(RNG), Description("If not 0, auto click A at given target.")]
        public AutoRNGMode AutoRNGMode { get; set; } = AutoRNGMode.AutoCalc;

        [Category(RNG), Description("If not 0, auto click A at given target. Only working with the ExternalCalc mode for AutoRNG.")]
        public int Target { get; set; } = 0;

        [Category(RNG), Description("If not 0, the value will be considered when calculating the target. Only working with the AutoCalc mode for AutoRNG.")]
        public int Delay { get; set; } = 0;

        [Category(RNG), Description("Reboot the console if target is above this value. Ignored if 0.")]
        public int RebootIf { get; set; } = 0;

        [Category(RNG), Description("Scroll the Pokédex to advance frames if target is above this value. Ignored if 0.")]
        public int ScrollDexIf { get; set; } = 0;

        [Category(RNG), Description("Action to do once the target is hit. Available options: A, B, X, Y, DUP, DDOWN, DLEFT, DRIGHT, PLUS, MINUS, RSTICK, LSTICK, L, R, ZL, ZR, HOME, CAPTURE")]
        public string Actions { get; set; } = "A, A";

        [Category(RNG), Description("Scenario of the target encounter.")]
        public CheckMode CheckMode { get; set; } = CheckMode.Wild;

        [Category(RNG), Description("Select the RNG you want to perform or calculate.")]
        public RNGType RNGType { get; set; } = 0;

        [Category(RNG), Description("Log all advances details if true.")]
        public bool GeneratorVerbose { get; set; } = false;

        [Category(RNG), Description("Max calculations for the generator.")]
        public int GeneratorMaxResults { get; set; } = 5000;

        private int _completedRNGs;

        [Category(Counts), Description("Completed RNGs")]
        public int CompletedRNGs
        {
            get => _completedRNGs;
            set => _completedRNGs = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public void AddCompletedRNGs() => Interlocked.Increment(ref _completedRNGs);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedRNGs != 0)
                yield return $"Completed RNGs: {CompletedRNGs}";
        }
    }
}
