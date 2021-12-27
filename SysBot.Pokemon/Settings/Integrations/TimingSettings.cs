using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class TimingSettings
    {
        private const string OpenGame = nameof(OpenGame);
        private const string CloseGame = nameof(CloseGame);
        private const string Misc = nameof(Misc);
        public override string ToString() => "Extra Time Settings";

        // Opening the game.
        [Category(OpenGame), Description("Extra time in milliseconds to wait for profiles to load when starting the game.")]
        public int ExtraTimeLoadProfile { get; set; } = 0;

        [Category(OpenGame), Description("Extra time in milliseconds to wait to check if DLC is usable.")]
        public int ExtraTimeCheckDLC { get; set; } = 0;

        [Category(OpenGame), Description("Extra time in milliseconds to wait before clicking A in title screen.")]
        public int ExtraTimeLoadGame { get; set; } = 5000;

        [Category(OpenGame), Description("[BDSP] Extra time in milliseconds to wait for the overworld to load after the title screen.")]
        public int ExtraTimeLoadOverworld { get; set; } = 3000;

        // Closing the game.
        [Category(CloseGame), Description("Extra time in milliseconds to wait after pressing HOME to minimize the game.")]
        public int ExtraTimeReturnHome { get; set; } = 0;

        [Category(CloseGame), Description("Extra time in milliseconds to wait after clicking to close the game.")]
        public int ExtraTimeCloseGame { get; set; } = 0;

        // Miscellaneous settings.

        [Category(Misc), Description("Time to wait after each keypress when navigating Switch menus.")]
        public int KeypressTime { get; set; } = 200;

        [Category(Misc), Description("Enable this to decline incoming system updates.")]
        public bool AvoidSystemUpdate { get; set; } = false;
    }
}
