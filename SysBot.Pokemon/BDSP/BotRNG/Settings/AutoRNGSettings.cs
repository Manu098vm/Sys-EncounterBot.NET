using System.ComponentModel;

namespace SysBot.Pokemon
{
	public class AutoRNGSettings
	{
        private const string AutoRNG = nameof(AutoRNG);
        public override string ToString() => "AutoRNG Bot Settings";

        [Category(AutoRNG), Description("AutoRNG Mode.")]
        public AutoRNGMode AutoRNGMode { get; set; } = AutoRNGMode.AutoCalc;

        [Category(AutoRNG), Description("[AutoCalc] Reboot the console if target is above this value.")]
        public int RebootIf { get; set; } = 100000;

        [Category(AutoRNG), Description("Scroll the Pokédex to advance frames if target is above this value.")]
        public int ScrollDexIf { get; set; } = 500;

        [Category(AutoRNG), Description("[AutoCalc] The value will be considered when calculating the target in AutoCalc mode.")]
        public int Delay { get; set; } = 85;

        [Category(AutoRNG), Description("[ExternalCalc] Auto click A at given target when using the ExternalCalc mode.")]
        public int Target { get; set; } = 0;

        [Category(AutoRNG), Description("Time to wait between each actions, expressed in milliseconds. Eg. 1000 is One Sec, 1500 is One Sec and Half.")]
        public int ActionTimings { get; set; } = 2_000;
    }
}