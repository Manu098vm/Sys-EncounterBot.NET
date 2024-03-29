﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class EncounterSettings : IBotStateSettings
    {
        private const string Counts = nameof(Counts);
        private const string Encounter = nameof(Encounter);
        public override string ToString() => "SWSH Encounter Bot Settings";

        [Category(Encounter), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        [Category(Encounter), Description("The method by which the SWSH bots will encounter Pokémon.")]
        public EncounterMode EncounteringType { get; set; } = EncounterMode.LiveStatsChecking;

        private int _completedWild;
        private int _completedLegend;

        [Category(Encounter)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public FossilSettings FossilSettings { get; set; } = new();

        [Category(Encounter)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public MaxLairSettings MaxLairSettings { get; set; } = new();

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