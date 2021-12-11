using System.ComponentModel;
using System.Threading;
using System.Collections.Generic;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class MaxLairSettings : IBotStateSettings, ICountSettings
    {
        private const string SWSH_MaxLair = nameof(SWSH_MaxLair);
        private const string Counts = nameof(Counts);
        public override string ToString() => "Max Lair Settings";

        [Category(SWSH_MaxLair), Description("Edit the lair path to the selected species. Ignored if None.")]
        public LairSpecies EditLairPath { get; set; } = LairSpecies.None;

        [Category(SWSH_MaxLair), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        [Category(SWSH_MaxLair), Description("Inject 1HKO cheat to rush the enemies. It is unlikely to be able to complete an adventure without this cheat enabled.")]
        public bool InstantKill { get; set; } = true;

        [Category(SWSH_MaxLair), Description("Discard any random shinies found during the Adventures if set to False. Keep all of them otherwise.")]
        public bool KeepShinies { get; set; } = true;

        private int _completedAdventures;

        [Category(Counts), Description("Max Adventure Completed")]
        public int CompletedAdventures
        {
            get => _completedAdventures;
            set => _completedAdventures = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public int AddCompletedAdventures() => Interlocked.Increment(ref _completedAdventures);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedAdventures != 0)
                yield return $"Completed Fossils: {CompletedAdventures}";
        }
    }
}