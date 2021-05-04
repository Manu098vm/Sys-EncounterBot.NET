using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class FossilSettings
    {
        private const string Fossil = nameof(Fossil);
        public override string ToString() => "Fossil Bot Settings";

        [Category(Fossil), Description("Species of fossil Pokémon to hunt for.")]
        public FossilSpecies Species { get; set; } = FossilSpecies.Dracozolt;
    }
}