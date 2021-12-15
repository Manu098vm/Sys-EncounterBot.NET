using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class GeneratorSettings
    {
        private const string Generator = nameof(Generator);
        public override string ToString() => "RNG Generator Settings";

        [Category(Generator), Description("Log all advances details if true.")]
        public bool GeneratorVerbose { get; set; } = false;

        [Category(Generator), Description("Max calculations for the generator.")]
        public int GeneratorMaxResults { get; set; } = 1000000;
    }
}