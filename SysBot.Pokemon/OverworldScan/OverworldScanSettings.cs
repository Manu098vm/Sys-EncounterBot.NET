using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class OverworldScanSettings
    {
        private const string OverworldScan = nameof(OverworldScan);
        public override string ToString() => "Encounter Bot Settings";

        [Category(OverworldScan), Description("The method by which the bot scan encounter Pokémon in the overworld.")]
        public ScanMode EncounteringType { get; set; } = ScanMode.OverworldSpawn;

        [Category(OverworldScan), Description("Milliseconds to wait before every save game. The overworld will be scanned after the game save.")]
        public int WaitMsBeforeSave { get; set; } = 5000;

        [Category(OverworldScan), Description("Examples: \"UP, RIGHT\", \"RIGHT, LEFT, UP\", etc. Every Movement MUST be separated with a comma (\",\")")]
        public string MoveOrder { get; set; } = "UP, RIGHT, DOWN, LEFT";

        [Category(OverworldScan), Description("Indicates how long the character will move north before every scan.")]
        public int MoveUpMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move north before every scan.")]
        public int MoveRightMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move north before every scan.")]
        public int MoveDownMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move north before every scan.")]
        public int MoveLeftMs { get; set; } = 5000;
    }
}