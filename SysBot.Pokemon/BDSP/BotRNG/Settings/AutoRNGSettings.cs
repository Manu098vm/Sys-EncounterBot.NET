using System.ComponentModel;

namespace SysBot.Pokemon
{
	public class AutoRNGSettings
	{
        private const string AutoRNG = nameof(AutoRNG);
        public override string ToString() => "AutoRNG Bot Settings";

        [Category(AutoRNG), Description("AutoRNG Mode.")]
        public AutoRNGMode AutoRNGMode { get; set; } = AutoRNGMode.AutoCalc;

        [Category(AutoRNG), Description("[AutoCalc] Reboot the console if target is missed/skipeped or if the calculated target is greater than the value below.")]
        public bool RebootIfFailed { get; set; } = true;

        [Category(AutoRNG), Description("[AutoCalc] Reboot the console if target is above this value. Ignored if RebootIfFailed is set to False.")]
        public int RebootValue { get; set; } = 100000;

        [Category(AutoRNG), Description("[AutoCalc] The value will be considered when calculating the target in AutoCalc mode. You can calculate proper delay with the DelayCalc routine.")]
        public int Delay { get; set; } = 87;

        [Category(AutoRNG), Description("[ExternalCalc] Auto click A at given target when using the ExternalCalc mode.")]
        public int Target { get; set; } = 0;

        [Category(AutoRNG), Description("Scroll the Pokédex to advance frames if target is above this value. Pokédex skipping will not be used if set to 0.")]
        public int ScrollDexUntil { get; set; } = 500;

        [Category(AutoRNG), Description("Actions to perform to hit the target. Available options: A, B, X, Y, DUP, DDOWN, DLEFT, DRIGHT, PLUS, MINUS, RSTICK, LSTICK, L, R, ZL, ZR, HOME.")]
        public string Actions { get; set; } = "A, A";

        [Category(AutoRNG), Description("Time to wait between each actions, expressed in milliseconds. Eg. 1000 is One Sec, 1500 is One Sec and Half.")]
        public int ActionTimings { get; set; } = 2_000;
    }
}