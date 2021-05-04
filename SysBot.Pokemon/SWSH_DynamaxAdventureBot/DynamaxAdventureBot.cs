using PKHeX.Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class DynamaxAdventureBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        public DynamaxAdventureBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
        }

        private int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("Starting main EncounterBot loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);

            var task = DoDynamaxAdventure(token);
            await task.ConfigureAwait(false);

            await ResetStick(token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }

        private async Task DoDynamaxAdventure(CancellationToken token)
        {
            //Initialization
            int adventureCompleted = 0;
            LairSpecies mon = Hub.Config.SWSH_DynaAdventure.EditLairPath;
            byte[] demageStandardState = BitConverter.GetBytes(0x7900E808);
            byte[] demageAlteredState = BitConverter.GetBytes(0x7900E81F);
            byte[] demageTemporalState;
            bool wasVideoClipActive = Hub.Config.StopConditions.CaptureVideoClip;
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

            //Check/set target parameters
            uint pathoffset = LairSpeciesSelector;
            bool caneditspecies = true;
            stopwatch.Start();

            //Check what's the right offset for the first lair path
            byte[] current_try1 = await Connection.ReadBytesAsync(LairSpeciesSelector, 2, token).ConfigureAwait(false);
            byte[] current_try2 = await Connection.ReadBytesAsync(LairSpeciesSelector2, 2, token).ConfigureAwait(false);

            if (Enum.IsDefined(typeof(LairSpecies), (ushort)BitConverter.ToInt16(current_try1, 0)))
                pathoffset = LairSpeciesSelector;
            else if (Enum.IsDefined(typeof(LairSpecies), (ushort)BitConverter.ToInt16(current_try2, 0)))
                pathoffset = LairSpeciesSelector2;
            else
                caneditspecies = false;

            byte[] current = await Connection.ReadBytesAsync(pathoffset, 2, token).ConfigureAwait(false);
            byte[] wanted = BitConverter.GetBytes((ushort)mon);

            if ((ushort)mon != 0 && current != wanted && !Enum.IsDefined(typeof(LairSpecies), mon))
            {
                Log($"{mon} is not an available Lair Boss species. Check your configurations and restart the bot.");
                return;
            }
            else if ((ushort)mon != 0 && current != wanted && Enum.IsDefined(typeof(LairSpecies), mon))
            {
                if (caneditspecies)
                {
                    await Connection.WriteBytesAsync(wanted, pathoffset, token);
                    Log($"{mon} ready to be hunted.");
                }
                else {
                    Log($"________________________________{Environment.NewLine}ATTENTION!" +
                        $"{Environment.NewLine}{mon} may not be your first path pokemon. Ignore this message if the Pokémon on the Stop Condition matches the Pokémon on your current Lair Path." +
                        $"{Environment.NewLine}________________________________");
                }
            }
            else if ((ushort)mon == 0)
                Log("(Any) Legendary ready to be hunted.");

            //Check ShinyXOR
            if (Hub.Config.StopConditions.ShinyTarget.ToString() == "SquareOnly")
            {
                Log("Lair Pokémon cannot be Square Shiny! Forced XOR=1. Check your settings and restart the bot.");
                return;
            }

            while (!token.IsCancellationRequested)
            {
                //Capture video clip is menaged internally
                if (Hub.Config.StopConditions.CaptureVideoClip == true)
                    Hub.Config.StopConditions.CaptureVideoClip = false;

                //Talk to the Lady
                while (!await IsInLairWait(token).ConfigureAwait(false))
                    await Click(A, 0_200, token).ConfigureAwait(false);

                //Select Solo Adventure
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);

                //MAIN LOOP
                int raidCount = 1;
                bool inBattle = false;
                bool lost = false;

                //Allows 1HKO
                if (Hub.Config.SWSH_DynaAdventure.InstantKill)
                {
                    demageTemporalState = await SwitchConnection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                    if (demageStandardState.SequenceEqual(demageTemporalState))
                        await SwitchConnection.WriteBytesMainAsync(demageAlteredState, demageOutputOffset, token).ConfigureAwait(false);
                }

                while (!(await IsInLairEndList(token).ConfigureAwait(false) || lost || token.IsCancellationRequested))
                {
                    await Click(A, 0_200, token).ConfigureAwait(false);
                    if (await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                    {
                        lost = true;
                        Log("Lost at first raid. Starting again.");
                    }
                    else if (!await IsInBattle(token).ConfigureAwait(false) && inBattle)
                        inBattle = false;
                    else if (await IsInBattle(token).ConfigureAwait(false) && !inBattle)
                    {
                        var pk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                        if (pk != null)
                            Log($"Raid Battle {raidCount}: {pk.Species} {pk.Nickname}");
                        else
                            Log($"Raid Battle {raidCount}.{Environment.NewLine}RAM probably shifted. It is suggested to reboot the game or console.");

                        inBattle = true;
                        raidCount++;
                        stopwatch.Restart();
                    }
                    else if (await IsInBattle(token).ConfigureAwait(false) && inBattle)
                    {
                        if (stopwatch.ElapsedMilliseconds > 120000)
                        {
                            Log("Stuck in a battle, trying to change move.");
                            for (int j = 0; j < 10; j++)
                                await Click(B, 1_00, token).ConfigureAwait(false);
                            await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                            stopwatch.Restart();
                        }
                    }
                }

                //Disable 1HKO
                if (Hub.Config.SWSH_DynaAdventure.InstantKill)
                {
                    demageTemporalState = await SwitchConnection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                    if (demageAlteredState.SequenceEqual(demageTemporalState))
                        await SwitchConnection.WriteBytesMainAsync(demageStandardState, demageOutputOffset, token).ConfigureAwait(false);
                }

                if (!lost)
                {
                    //Check for shinies, check all the StopConditions for the Legendary
                    int[] found = await IsAdventureHuntFound(token).ConfigureAwait(false);

                    adventureCompleted++;
                    if (raidCount < 5)
                        Log($"Lost at battle n. {(raidCount - 1)}, adventure n. {adventureCompleted}.");
                    else if (found[1] == 0)
                        Log($"Lost at battle n. 4, adventure n. {adventureCompleted}.");
                    else
                        Log($"Adventure n. {adventureCompleted} completed.");

                    //Ending routine
                    if ((Hub.Config.SWSH_DynaAdventure.KeepShinies && found[0] > 0) || found[0] == 4)
                    {
                        await Task.Delay(1_500, token).ConfigureAwait(false);
                        for (int i = 1; i < found[0]; i++)
                            await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                        await Click(A, 0_900, token).ConfigureAwait(false);
                        await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                        await Click(A, 2_300, token).ConfigureAwait(false);
                        await PressAndHold(CAPTURE, 2_000, 10_000, token).ConfigureAwait(false);
                        if (wasVideoClipActive == true)
                            Hub.Config.StopConditions.CaptureVideoClip = true;
                        //If legendary Pokémon is found, stop the routine, else keep the shiny pokemon and restart the routine
                        if (found[0] == 4)
                            return;
                        else
                        {
                            await Task.Delay(1_500, token).ConfigureAwait(false);
                            await Click(B, 1_500, token).ConfigureAwait(false);
                            while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                            {
                                await Click(A, 0_500, token).ConfigureAwait(false);
                                await Task.Delay(2_000, token).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        Log("No result found, starting again");
                        await Task.Delay(1_500, token).ConfigureAwait(false);
                        await Click(B, 1_000, token).ConfigureAwait(false);
                        while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                        {
                            await Click(A, 0_500, token).ConfigureAwait(false);
                            await Task.Delay(2_000, token).ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private async Task<int[]> IsAdventureHuntFound(CancellationToken token)
        {
            int[] found = { 0, 0 };
            int i = 0;
            string pointer;

            while (i < 4)
            {
                pointer = $"[[[[main+28F4060]+1B0]+68]+{String.Format("{0:X}", 88 + (8 * i))}]+D0";
                var pkm = await ReadUntilPresent(await ParsePointer(pointer, token), 2_000, 0_200, token).ConfigureAwait(false);
                if (pkm != null)
                {
                    if (i == 3) found[1] = 1;
                    if ((HandleEncounter(pkm, i == 3) == true) || (i < 4 && pkm.IsShiny))
                    {
                        if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                            Log($"<@{Hub.Config.Discord.UserTag}> a {(pkm.IsShiny ? "Shiny " : "")}{pkm.Nickname} has been found!");
                        else
                            Log($"A {(pkm.IsShiny ? "Shiny " : "")}{pkm.Nickname} has been found!");
                        found[0] = i + 1;
                    }
                }
                i++;
            }
            return found;
        }

        private bool HandleEncounter(PK8 pk, bool legends)
        {
            encounterCount++;

            //Star/Square Shiny Recognition
            var showdowntext = ShowdownParsing.GetShowdownText(pk);
            if (pk.IsShiny && pk.ShinyXor == 0)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
            else if (pk.IsShiny)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");

            Log($"Encounter: {encounterCount}{Environment.NewLine}{Environment.NewLine}{showdowntext}{Environment.NewLine}");
            if (legends)
                Counts.AddCompletedLegends();
            else
                Counts.AddCompletedEncounters();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, legends ? "legends" : "encounters", pk);

            if (StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions))
            {
                if (legends)
                    Log("Result found! Stopping routine execution; restart the bot(s) to search again.");
                return true;
            }
            return false;
        }

        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }
    }
}
