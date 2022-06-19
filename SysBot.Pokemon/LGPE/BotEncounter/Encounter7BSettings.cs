using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class Encounter7BSettings : IBotStateSettings
    {
        private const string Counts = nameof(Counts);
        private const string Encounter = nameof(Encounter);
        public override string ToString() => "LGPE Encounter Bot Settings";

        [Category(Encounter), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        [Category(Encounter), Description("The method by which the bot will encounter Pokémon In Let's Go.")]
        public LetsGoMode EncounteringType { get; set; } = LetsGoMode.LiveStatsChecking;

        [Category(Encounter), Description("Set the fortune teller nature. All the wild and stationary Pokémon will have this nature. Ignored if random.")]
        public PKHeX.Core.Nature SetFortuneTellerNature { get; set; } = PKHeX.Core.Nature.Random;

        [Category(Encounter), Description("Set the Lure type. The Lure will be reactivated once the effect ends. Only useful for LiveStatsChecking. Ignored if None.")]
        public Lure SetLure { get; set; } = Lure.None;


        private int _completedWild;
        private int _completedLegend;

        [Category(Counts), Description("Encountered Wild Pokémon")]
        public int CompletedEncounters
        {
            get => _completedWild;
            set => _completedWild = value;
        }

        [Category(Counts), Description("Encountered Legendary Pokémon")]
        public int CompletedLegends
        {
            get => _completedLegend;
            set => _completedLegend = value;
        }

        public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);
        public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (CompletedEncounters != 0)
                yield return $"Wild Encounters: {CompletedEncounters}";
            if (CompletedLegends != 0)
                yield return $"Legendary Encounters: {CompletedLegends}";
        }
    }
}