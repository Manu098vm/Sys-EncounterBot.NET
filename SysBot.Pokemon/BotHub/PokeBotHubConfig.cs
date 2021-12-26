using System.ComponentModel;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon
{
    public sealed class PokeBotHubConfig : BaseConfig
    {
        private const string Bots = nameof(Bots);
        private const string Integration = nameof(Integration);

        [Browsable(false)]
        public override bool Shuffled => false;

        [Category(FeatureToggle), Description("Method for detecting the overworld. Original method may not work consistently for some users, while ConsoleLanguageSpecific method requires your Switch console language.")]
        public ScreenDetectionMode ScreenDetection { get; set; } = ScreenDetectionMode.ConsoleLanguageSpecific;

        [Category(FeatureToggle), Description("ConsoleLanguageSpecific screen detection method only. Set your Switch console language here for bots to work properly. All consoles should be using the same language.")]
        public ConsoleLanguageParameter ConsoleLanguage { get; set; }

        [Category(Operation), Description("Stop conditions.")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public StopConditionSettings StopConditions { get; set; } = new();

        [Category(Operation), Description("Add extra time for slower Switches.")]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public TimingSettings Timings { get; set; } = new();

        // Bots
        [Category(Bots)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public RNGSettings BDSP_RNG { get; set; } = new();

        [Category(Bots)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public Overworld7BSettings LGPE_OverworldScan { get; set; } = new();

        [Category(Bots)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public Encounter7BSettings LGPE_Encounter { get; set; } = new();

        [Category(Bots)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public OverworldScanSettings SWSH_OverworldScan { get; set; } = new();

        [Category(Bots)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public EggSettings SWSH_Eggs { get; set; } = new();

        [Category(Bots)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public EncounterSettings SWSH_Encounter { get; set; } = new();

        // Integration

        [Category(Integration)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public DiscordSettings Discord { get; set; } = new();
    }
}