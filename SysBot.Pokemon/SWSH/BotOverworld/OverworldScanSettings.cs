using System.ComponentModel;
using System.Threading;
using System.Collections.Generic;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class OverworldScanSettings : IBotStateSettings, ICountSettings
    {
        private const string Counts = nameof(Counts);
        private const string OverworldScan = nameof(OverworldScan);
        public override string ToString() => "Overworld Scan Settings";

        [Category(OverworldScan), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

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

        private int _completedScans;

        [Category(Counts), Description("Completed overworld Scans.")]
        public int CompletedScans
        {
            get => _completedScans;
            set => _completedScans = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public int AddCompletedScans() => Interlocked.Increment(ref _completedScans);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedScans != 0)
                yield return $"Total overworld scans: {CompletedScans}";
        }
    }
}