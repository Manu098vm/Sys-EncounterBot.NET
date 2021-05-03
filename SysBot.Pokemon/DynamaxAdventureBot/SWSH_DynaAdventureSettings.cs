using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class SWSH_DynaAdventureSettings
    {
        private const string SWSH_DynaAdventure = nameof(SWSH_DynaAdventure);
        public override string ToString() => "Dynamax Adventure Settings";

        [Category(SWSH_DynaAdventure), Description("Edit the lair path to the selected species. Ignored if None.")]
        public LairSpecies EditLairPath { get; set; } = LairSpecies.None;

        [Category(SWSH_DynaAdventure), Description("Inject 1HKO cheat to rush the enemies. It is unlikely to be able to complete an adventure without this cheat enabled.")]
        public bool InstantKill { get; set; } = true;
    }
}