using System.Collections.Generic;
using System.Threading;

namespace SysBot.Pokemon
{
    public class BotCompleteCounts
    {
        private readonly CountSettings Config;

        private int CompletedEggs;
        private int CompletedFossils;
        private int CompletedEncounters;
        private int CompletedLegends;
        private int CompletedDumps;
        private int CompletedRaids;

        public BotCompleteCounts(CountSettings config)
        {
            Config = config;
            LoadCountsFromConfig();
        }

        public void LoadCountsFromConfig()
        {
            CompletedEggs = Config.CompletedEggs;
            CompletedFossils = Config.CompletedFossils;
            CompletedEncounters = Config.CompletedEncounters;
            CompletedLegends = Config.CompletedLegends;
            CompletedRaids = Config.CompletedRaids;
        }

        public void AddCompletedEggs()
        {
            Interlocked.Increment(ref CompletedEggs);
            Config.CompletedEggs = CompletedEggs;
        }

        public void AddCompletedFossils()
        {
            Interlocked.Increment(ref CompletedFossils);
            Config.CompletedFossils = CompletedFossils;
        }

        public void AddCompletedEncounters()
        {
            Interlocked.Increment(ref CompletedEncounters);
            Config.CompletedEncounters = CompletedEncounters;
        }
        public void AddCompletedLegends()
        {
            Interlocked.Increment(ref CompletedLegends);
            Config.CompletedLegends = CompletedLegends;
        }

        public void AddCompletedRaids()
        {
            Interlocked.Increment(ref CompletedRaids);
            Config.CompletedRaids = CompletedRaids;
        }

        public void AddCompletedDumps()
        {
            Interlocked.Increment(ref CompletedDumps);
            Config.CompletedDumps = CompletedDumps;
        }

        public IEnumerable<string> Summary()
        {
            if (CompletedDumps != 0)
                yield return $"Dump Trades: {CompletedDumps}";
            if (CompletedEggs != 0)
                yield return $"Eggs Received: {CompletedEggs}";
            if (CompletedRaids != 0)
                yield return $"Completed Raids: {CompletedRaids}";
            if (CompletedFossils != 0)
                yield return $"Completed Fossils: {CompletedFossils}";
            if (CompletedEncounters != 0)
                yield return $"Wild Encounters: {CompletedEncounters}";
            if (CompletedLegends != 0)
                yield return $"Legendary Encounters: {CompletedLegends}";
        }
    }
}