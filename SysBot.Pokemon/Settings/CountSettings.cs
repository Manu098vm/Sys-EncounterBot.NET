using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class CountSettings
    {
        private const string Trades = nameof(Trades);
        private const string Received = nameof(Received);
        public override string ToString() => "Completed Counts Storage";

        [Category(Trades), Description("Completed Dump Trades (Specific User)")]
        public int CompletedDumps { get; set; }

        // Received

        [Category(Received), Description("Eggs Retrieved")]
        public int CompletedEggs { get; set; }

        [Category(Received), Description("Fossil Pokémon Revived")]
        public int CompletedFossils { get; set; }

        [Category(Received), Description("Encountered Wild Pokémon")]
        public int CompletedEncounters { get; set; }

        [Category(Received), Description("Encountered Legendary Pokémon")]
        public int CompletedLegends { get; set; }

        [Category(Received), Description("Raids Started")]
        public int CompletedRaids { get; set; }
    }
}