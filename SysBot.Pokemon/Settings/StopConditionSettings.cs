﻿using PKHeX.Core;
using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;

namespace SysBot.Pokemon
{
    public class StopConditionSettings
    {
        private const string StopConditions = nameof(StopConditions);
        public override string ToString() => "Stop Condition Settings";

        [Category(StopConditions), Description("Stops only on Pokémon of this species. No restrictions if set to \"None\".")]
        public Species StopOnSpecies { get; set; }

        [Category(StopConditions), Description("Stops only on Pokémon with this FormID. No restrictions if left blank.")]
        public int? StopOnForm { get; set; }

        [Category(StopConditions), Description("Stop only on Pokémon with one of the following Natures, separated by commas. Ignored if empty.")]
        public string TargetNatures { get; set; } = String.Empty;

        [Category(StopConditions), Description("Minimum accepted IVs in the format HP/Atk/Def/SpA/SpD/Spe. Use \"x\" for unchecked IVs and \"/\" as a separator.")]
        public string TargetMinIVs { get; set; } = "";

        [Category(StopConditions), Description("Maximum accepted IVs in the format HP/Atk/Def/SpA/SpD/Spe. Use \"x\" for unchecked IVs and \"/\" as a separator.")]
        public string TargetMaxIVs { get; set; } = "";

        [Category(StopConditions), Description("Selects the shiny type to stop on.")]
        public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

        [Category(StopConditions), Description("Stop only on Pokémon that have a mark.")]
        public bool MarkOnly { get; set; } = false;

        [Category(StopConditions), Description("List of marks to ignore separated by commas. Use the code name, e.g. \"MarkUncommon, MarkDawn, MarkPrideful\".")]
        public string UnwantedMarks { get; set; } = "";

        [Category(StopConditions), Description("Holds Capture button to record a 30 second clip when a matching Pokémon is found by EncounterBot or Fossilbot.")]
        public bool CaptureVideoClip { get; set; }

        [Category(StopConditions), Description("Extra time in milliseconds to wait after an encounter is matched before pressing Capture for EncounterBot or Fossilbot.")]
        public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

        [Category(StopConditions), Description("If set to TRUE, matches both ShinyTarget and TargetIVs settings. Otherwise, looks for either ShinyTarget or TargetIVs match.")]
        public bool MatchShinyAndIV { get; set; } = true;

        [Category(StopConditions), Description("If not empty, the provided string will be prepended to the result found log message to alert for whomever you specify. For Discord, use <@userIDnumber> to mention.")]
        public string MatchFoundEchoMention { get; set; } = string.Empty;

        public static bool EncounterFound<T>(T pk, int[] targetminIVs, int[] targetmaxIVs, StopConditionSettings settings, IReadOnlyList<string>? naturelist, IReadOnlyList<string>? marklist) where T : PKM
        {
            // Match Nature and Species if they were specified.
            if (settings.StopOnSpecies != Species.None && settings.StopOnSpecies != (Species)pk.Species)
                return false;

            if (settings.StopOnForm.HasValue && settings.StopOnForm != pk.Form)
                return false;

            if(naturelist is not null && naturelist.Count > 0)
			{
                var nat_check = false;
                foreach (var nature in naturelist)
				{
                    Nature nat = (Nature)Enum.Parse(typeof(Nature), nature, true);
                    if (nat == (Nature)pk.Nature)
                        nat_check = true;
                }
                if(!nat_check)
                    return false;
			}

            // Return if it doesn't have a mark or it has an unwanted mark.
            var unmarked = pk is IRibbonIndex m && !HasMark(m);
            var unwanted = marklist is not null && pk is IRibbonIndex m2 && settings.IsUnwantedMark(GetMarkName(m2), marklist);
            if (settings.MarkOnly && (unmarked || unwanted)) return false;

            if (settings.ShinyTarget != TargetShinyType.DisableOption)
            {
                bool shinymatch = settings.ShinyTarget switch
                {
                    TargetShinyType.AnyShiny => pk.IsShiny,
                    TargetShinyType.NonShiny => !pk.IsShiny,
                    TargetShinyType.StarOnly => pk.IsShiny && pk.ShinyXor != 0,
                    TargetShinyType.SquareOnly => pk.ShinyXor == 0,
                    TargetShinyType.DisableOption => true,
                    _ => throw new ArgumentException(nameof(TargetShinyType)),
                };

                // If we only needed to match one of the criteria and it shinymatch'd, return true.
                // If we needed to match both criteria and it didn't shinymatch, return false.
                if (!settings.MatchShinyAndIV && shinymatch)
                    return true;
                if (settings.MatchShinyAndIV && !shinymatch)
                    return false;
            }


            Span<int> pkIVList = stackalloc int[6];
            pk.GetIVs(pkIVList);
            (pkIVList[5], pkIVList[3], pkIVList[4]) = (pkIVList[3], pkIVList[4], pkIVList[5]);

            for (int i = 0; i < 6; i++)
            {
                if (targetminIVs[i] > pkIVList[i] || targetmaxIVs[i] < pkIVList[i])
                    return false;
            }
            return true;
        }

        public static void InitializeTargetIVs(PokeBotHub<PK8> hub, out int[] min, out int[] max)
        {
            min = ReadTargetIVs(hub.Config.StopConditions, true);
            max = ReadTargetIVs(hub.Config.StopConditions, false);
        }

        private static int[] ReadTargetIVs(StopConditionSettings settings, bool min)
        {
            int[] targetIVs = new int[6];
            char[] split = { '/' };

            string[] splitIVs = min
                ? settings.TargetMinIVs.Split(split, StringSplitOptions.RemoveEmptyEntries)
                : settings.TargetMaxIVs.Split(split, StringSplitOptions.RemoveEmptyEntries);

            // Only accept up to 6 values.  Fill it in with default values if they don't provide 6.
            // Anything that isn't an integer will be a wild card.
            for (int i = 0; i < 6; i++)
            {
                if (i < splitIVs.Length)
                {
                    var str = splitIVs[i];
                    if (int.TryParse(str, out var val))
                    {
                        targetIVs[i] = val;
                        continue;
                    }
                }
                targetIVs[i] = min ? 0 : 31;
            }
            return targetIVs;
        }

        private static bool HasMark(IRibbonIndex pk)
        {
            for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
            {
                if (pk.GetRibbon((int)mark))
                    return true;
            }
            return false;
        }

        public string GetPrintName(PKM pk)
        {
            var set = ShowdownParsing.GetShowdownText(pk);

            if (pk.IsShiny)
            {
                if (pk.ShinyXor == 0)
                    set = set.Replace("Shiny: Yes", "Shiny: Square");
                else
                    set = set.Replace("Shiny: Yes", "Shiny: Star");
            }

            if (pk is IRibbonIndex r)
                for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
                    if (r.GetRibbon((int)mark))
                        set += $"{Environment.NewLine}{mark}";

            return set;
        }

        public static void ReadUnwantedMarks(StopConditionSettings settings, out IReadOnlyList<string> marks) =>
            marks = settings.UnwantedMarks.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

        public static void ReadWantedNatures(StopConditionSettings settings, out IReadOnlyList<string> natures) =>
            natures = settings.TargetNatures.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

        public virtual bool IsUnwantedMark(string mark, IReadOnlyList<string> marklist) { 
            foreach(var el in marklist)
                if (string.Equals(el, mark, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        public static string GetMarkName(IRibbonIndex pk)
        {
            var str = "";
            if (pk is IRibbonIndex r)
                for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
                    if (r.GetRibbon((int)mark))
                        str = $"{mark}";

            return str;
        }
    }

    public enum TargetShinyType
    {
        DisableOption,  // Doesn't care
        NonShiny,       // Match nonshiny only
        AnyShiny,       // Match any shiny regardless of type
        StarOnly,       // Match star shiny only
        SquareOnly,     // Match square shiny only
    }
}
