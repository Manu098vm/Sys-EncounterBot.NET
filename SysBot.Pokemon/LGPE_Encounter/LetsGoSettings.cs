using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class LetsGoSettings
    {
        private const string LetsGo = nameof(LetsGo);
        public override string ToString() => "Encounter Bot Settings";

        [Category(LetsGo), Description("The method by which the bot will encounter Pokémon In Let's Go.")]
        public LetsGoMode EncounteringType { get; set; } = LetsGoMode.LiveStatsChecking;
    }
}