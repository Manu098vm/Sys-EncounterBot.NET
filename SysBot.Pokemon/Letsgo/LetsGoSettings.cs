using System.ComponentModel;
using PKHeX.Core;

namespace SysBot.Pokemon
{
    public class LetsGoSettings
    {
        private const string LetsGo = nameof(LetsGo);
        public override string ToString() => "Encounter Bot Settings";

        [Category(LetsGo), Description("The method by which the bot will encounter Pokémon In Let's Go. Overworld and WildBirds method can only check for Species and (any) Shinyness. Level, IVs, Nature and Square Shinies cannot be verified.")]
        public LetsGoMode EncounteringType { get; set; } = LetsGoMode.TestRoutine;

        [Category(LetsGo), Description("Force the current chain to be on the selected Species. Ignored if None.")]
        public Species ChainSpecies { get; set; }

        [Category(LetsGo), Description("Force the current chain to be at the entered value. Ignored if 0.")]
        public int ChainCount { get; set; } = 0;

        [Category(LetsGo), Description("Set the test you want to attempt.")]
        public LetsGoTest TestRoutine { get; set; } = LetsGoTest.TestOffsets;

        [Category(LetsGo), Description("Set how many attempt the bot will do to check the freezing values. Infinite checking if 0.")]
        public int FreezingTestCount { get; set; } = 0;

    }
}