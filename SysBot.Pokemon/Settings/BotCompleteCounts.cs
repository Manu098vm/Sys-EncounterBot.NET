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
        private int CompletedDynamaxAdventures;
        private int SWSHOverworldScans;
        private int SWSHLegendaryScans;
        private int LGPEOverworldScans;
        private int LGPELegendaryScans;
        private int ShinyEncounters;

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
            CompletedDynamaxAdventures = Config.CompletedDynamaxAdventures;
            SWSHOverworldScans = Config.SWSHOverworldScans;
            SWSHLegendaryScans = Config.SWSHLegendaryScans;
            LGPEOverworldScans = Config.LGPEOverworldScans;
            LGPELegendaryScans = Config.LGPELegendaryScans;
            ShinyEncounters = Config.ShinyEncounters;
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

        public void AddCompletedDumps()
        {
            Interlocked.Increment(ref CompletedDumps);
            Config.CompletedDumps = CompletedDumps;
        }

        public void AddCompletedDynamaxAdventures()
        {
            Interlocked.Increment(ref CompletedDynamaxAdventures);
            Config.CompletedDynamaxAdventures = CompletedDynamaxAdventures;
        }

        public void AddSWSHOverworldScans()
        {
            Interlocked.Increment(ref SWSHOverworldScans);
            Config.SWSHOverworldScans = SWSHOverworldScans;
        }

        public void AddSWSHLegendaryScans()
        {
            Interlocked.Increment(ref SWSHLegendaryScans);
            Config.SWSHLegendaryScans = SWSHLegendaryScans;
        }

        public void AddLGPEOverworldScans()
        {
            Interlocked.Increment(ref LGPEOverworldScans);
            Config.LGPEOverworldScans = LGPEOverworldScans;
        }

        public void AddLGPELegendaryScans()
        {
            Interlocked.Increment(ref LGPELegendaryScans);
            Config.LGPELegendaryScans = LGPELegendaryScans;
        }

        public void AddShinyEncounters()
        {
            Interlocked.Increment(ref ShinyEncounters);
            Config.ShinyEncounters = ShinyEncounters;
        }

        public IEnumerable<string> Summary()
        {
            if (CompletedDumps != 0)
                yield return $"Dump Trades: {CompletedDumps}";
            if (CompletedEggs != 0)
                yield return $"Eggs Received: {CompletedEggs}";
            if (CompletedFossils != 0)
                yield return $"Completed Fossils: {CompletedFossils}";
            if (CompletedEncounters != 0)
                yield return $"Wild Encounters: {CompletedEncounters}";
            if (CompletedLegends != 0)
                yield return $"Legendary Encounters: {CompletedLegends}";
            if (SWSHOverworldScans != 0)
                yield return $"SWSH Overworld Scans: {SWSHOverworldScans}";
            if (SWSHLegendaryScans != 0)
                yield return $"SWSH Legendary Scans: {SWSHLegendaryScans}";
            if (LGPEOverworldScans != 0)
                yield return $"LGPE Overworld Scans: {LGPEOverworldScans}";
            if (LGPELegendaryScans != 0)
                yield return $"LGPE Legendary Scans: {LGPELegendaryScans}";
            if (ShinyEncounters != 0)
                yield return $"Total count of Shiny Pokémon encountered through the bot routines: {ShinyEncounters}";
        }
    }
}