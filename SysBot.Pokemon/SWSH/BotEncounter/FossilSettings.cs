using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class FossilSettings
    {
        private const string Fossil = nameof(Fossil);
        public override string ToString() => "Fossil Bot Settings";

        [Category(Fossil), Description("Species of fossil Pokémon to hunt for.")]
        public FossilSpecies Species { get; set; } = FossilSpecies.Dracozolt;

        [Category(Fossil), Description("Max revivals. Useful if you have limited box spaces. Ignored if 0.")]
        public int MaxRevivals { get; set; } = 0;
    }
}