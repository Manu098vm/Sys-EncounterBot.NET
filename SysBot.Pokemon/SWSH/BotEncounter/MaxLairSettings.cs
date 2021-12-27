using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class MaxLairSettings
    {
        private const string SWSH_MaxLair = nameof(SWSH_MaxLair);
        public override string ToString() => "Max Lair Settings";

        [Category(SWSH_MaxLair), Description("Edit the lair path to the selected species. Ignored if None.")]
        public LairSpecies EditLairPath { get; set; } = LairSpecies.None;

        [Category(SWSH_MaxLair), Description("Inject 1HKO cheat to rush the enemies. It is unlikely to be able to complete an adventure without this cheat enabled.")]
        public bool InstantKill { get; set; } = true;

        [Category(SWSH_MaxLair), Description("Only keep Shiny Legendaries if set to False. Keep all random shiny found during the adventure otherwise.")]
        public bool KeepShinies { get; set; } = true;
    }
}