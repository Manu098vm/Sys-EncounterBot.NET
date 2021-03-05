using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class GiftBotSettings
    {
        private const string Encounter = nameof(Encounter);
        public override string ToString() => "GiftBot Settings";

        [Category(Encounter), Description("Select 'true' if the Pokémon you're hunting is a legendary. Select 'false' otherwise.")]
        public bool IsLegendary { get; set; } = false;
    }
}