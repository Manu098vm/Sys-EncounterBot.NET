using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class OverworldScanSettings
    {
        private const string OverworldScan = nameof(OverworldScan);
        public override string ToString() => "Encounter Bot Settings";

        [Category(OverworldScan), Description("The method by which the bot scan encounter Pokémon in the overworld.")]
        public ScanMode EncounteringType { get; set; } = ScanMode.OverworldSpawn;
    }
}