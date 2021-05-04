using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class CountSettings
    {
        private const string Dumps = nameof(Dumps);
        private const string Count = nameof(Count);
        public override string ToString() => "Completed Counts Storage";

        [Category(Dumps), Description("Completed Dump Trades (Specific User)")]
        public int CompletedDumps { get; set; }

        [Category(Count), Description("Eggs Retrieved")]
        public int CompletedEggs { get; set; }

        [Category(Count), Description("Fossil Pokémon Revived")]
        public int CompletedFossils { get; set; }

        [Category(Count), Description("Encountered Wild Pokémon")]
        public int CompletedEncounters { get; set; }

        [Category(Count), Description("Encountered Legendary Pokémon")]
        public int CompletedLegends { get; set; }
    }
}