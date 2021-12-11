﻿using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using SysBot.Base;

namespace SysBot.Pokemon
{
    public class FossilSettings : IBotStateSettings, ICountSettings
    {
        private const string Fossil = nameof(Fossil);
        private const string Counts = nameof(Counts);
        public override string ToString() => "Fossil Bot Settings";

        [Category(Fossil), Description("Species of fossil Pokémon to hunt for.")]
        public FossilSpecies Species { get; set; } = FossilSpecies.Dracozolt;

        [Category(Fossil), Description("When enabled, the screen will be turned off during normal bot loop operation to save power.")]
        public bool ScreenOff { get; set; } = false;

        private int _completedFossils;

        [Category(Counts), Description("Fossil Pokémon Revived")]
        public int CompletedFossils
        {
            get => _completedFossils;
            set => _completedFossils = value;
        }

        [Category(Counts), Description("When enabled, the counts will be emitted when a status check is requested.")]
        public bool EmitCountsOnStatusCheck { get; set; }

        public int AddCompletedFossils() => Interlocked.Increment(ref _completedFossils);

        public IEnumerable<string> GetNonZeroCounts()
        {
            if (!EmitCountsOnStatusCheck)
                yield break;
            if (CompletedFossils != 0)
                yield return $"Completed Fossils: {CompletedFossils}";
        }
    }
}