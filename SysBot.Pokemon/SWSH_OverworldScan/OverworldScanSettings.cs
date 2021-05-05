using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class OverworldScanSettings
    {
        private const string OverworldScan = nameof(OverworldScan);
        public override string ToString() => "Overworld Bot Settings";

        [Category(OverworldScan), Description("The method by which the bot scan encounter Pokémon in the overworld.")]
        public ScanMode EncounteringType { get; set; } = ScanMode.OverworldSpawn;

        [Category(OverworldScan), Description("Get on the bike and get off immediately afterwards. Useful for resetting fishing areas. ")]
        public bool GetOnOffBike { get; set; } = false;

        [Category(OverworldScan), Description("Milliseconds to wait before every save game. The overworld will be scanned after the game save.")]
        public int WaitMsBeforeSave { get; set; } = 5000;

        [Category(OverworldScan), Description("Examples: \"UP, RIGHT\", \"RIGHT, LEFT, UP\", etc. Every movement MUST be separated with a comma (\",\")")]
        public string MovementOrder { get; set; } = string.Empty;

        [Category(OverworldScan), Description("Indicates how long the character will move north before every scan.")]
        public int MoveUpMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move east before every scan.")]
        public int MoveRightMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move south before every scan.")]
        public int MoveDownMs { get; set; } = 5000;

        [Category(OverworldScan), Description("Indicates how long the character will move west before every scan.")]
        public int MoveLeftMs { get; set; } = 5000;
    }
}