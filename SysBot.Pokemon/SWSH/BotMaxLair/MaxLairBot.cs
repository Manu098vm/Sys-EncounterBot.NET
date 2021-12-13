using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;
using System.Linq;

namespace SysBot.Pokemon
{
    public class MaxLairBot : PokeRoutineExecutor8, IEncounterBot
    {
        private readonly PokeBotHub<PK8> Hub;
        private readonly BotCompleteCounts Count;
        private readonly MaxLairSettings Settings;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;
        protected SWSH.PokeDataPointers Pointers { get; private set; } = new();
        public ICountSettings Counts => Settings;

        public MaxLairBot(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.SWSH_MaxLair;
            Count = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
        }

        private int encounterCount;
        private int completedAdventures;
        private const uint standardDemage = 0x7900E808;
        private const uint alteredDemage = 0x7900E81F;

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);

            try
            {
                await InitializeHardware(Settings, token).ConfigureAwait(false);
                Log($"Starting main MaxLairBot loop.");
                Config.IterateNextRoutine();
                await InnerLoop(token).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {nameof(FossilBot)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        private async Task InnerLoop(CancellationToken token)
        {
            var target = Hub.Config.SWSH_MaxLair.EditLairPath;
            var wasVideoClipActive = Hub.Config.StopConditions.CaptureVideoClip;
            Stopwatch stopwatch = new();
            bool caneditspecies = true;
            uint pathoffset = LairSpeciesSelector;

            stopwatch.Start();

            //Check offset version
            var current_try1 = await Connection.ReadBytesAsync(LairSpeciesSelector, 2, token).ConfigureAwait(false);
            var current_try2 = await Connection.ReadBytesAsync(LairSpeciesSelector2, 2, token).ConfigureAwait(false);
            if (Enum.IsDefined(typeof(LairSpecies), (ushort)BitConverter.ToInt16(current_try1, 0)))
                pathoffset = LairSpeciesSelector;
            else if (Enum.IsDefined(typeof(LairSpecies), (ushort)BitConverter.ToInt16(current_try2, 0)))
                pathoffset = LairSpeciesSelector2;
            else
                caneditspecies = false;

            if (caneditspecies)
            {
                var currentpath = await Connection.ReadBytesAsync(pathoffset, 2, token).ConfigureAwait(false);
                var wantedpath = BitConverter.GetBytes((ushort)target);

                if ((ushort)target != 0 && currentpath != wantedpath && !Enum.IsDefined(typeof(LairSpecies), target))
                {
                    Log($"{target} is not an available Lair Boss species. Check your configurations and restart the bot.");
                    return;
                }
                else if ((ushort)target != 0 && currentpath != wantedpath && Enum.IsDefined(typeof(LairSpecies), target))
                {
                    await Connection.WriteBytesAsync(wantedpath, pathoffset, token);
                    Log($"{target} ready to be hunted.");
                }
                else if ((ushort)target == 0)
                    Log("(Any) Legendary ready to be hunted.");
            }
            else
            {
                Log($"{Environment.NewLine}________________________________{Environment.NewLine}ATTENTION!" +
                    $"{Environment.NewLine}{target} may not be your first path pokemon. Ignore this message if the Pokémon on the Stop Condition matches the Pokémon on your current Lair Path." +
                    $"{Environment.NewLine}________________________________");
            }

            while (!token.IsCancellationRequested)
			{
                //Capture video clip is menaged internally
                if (Hub.Config.StopConditions.CaptureVideoClip == true)
                    Hub.Config.StopConditions.CaptureVideoClip = false;

                //Talk to the Lady
                while (!await IsInLairWait(token).ConfigureAwait(false))
                    await Click(A, 0_500, token).ConfigureAwait(false);

                await Task.Delay(2_000, token).ConfigureAwait(false);

                //Select Solo Adventure
                await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);

                //MAIN LOOP
                var raidCount = 1;
                var inBattle = false;
                var lost = false;

                //Allows 1HKO
                if (Hub.Config.SWSH_MaxLair.InstantKill)
                {
                    var demageTemporalState = await SwitchConnection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                    if (BitConverter.GetBytes(standardDemage).SequenceEqual(demageTemporalState))
                        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(alteredDemage), demageOutputOffset, token).ConfigureAwait(false);
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
                        var pk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);
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
                            for (int j = 0; j < 20; j++)
                                await Click(B, 1_00, token).ConfigureAwait(false);
                            await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                            stopwatch.Restart();
                        }
                    }
                }

                //Disable 1HKO
                if (Hub.Config.SWSH_MaxLair.InstantKill)
                {
                    var demageTemporalState = await SwitchConnection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                    if (BitConverter.GetBytes(alteredDemage).SequenceEqual(demageTemporalState))
                        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(standardDemage), demageOutputOffset, token).ConfigureAwait(false);
                }

				if (!lost)
				{
                    //Check for shinies, check all the StopConditions for the Legendary
                    int[] found = await IsAdventureHuntFound(token).ConfigureAwait(false);

                    completedAdventures++;
                    if (raidCount < 5)
                        Log($"Lost at battle n. {(raidCount - 1)}, adventure n. {completedAdventures}.");
                    else if (found[1] == 0)
                        Log($"Lost at battle n. 4, adventure n. {completedAdventures}.");
                    else
                    {
                        Log($"Adventure n. {completedAdventures} completed.");
                        Count.AddCompletedDynamaxAdventures();
                        Settings.AddCompletedAdventures();
                    }

                    if ((Hub.Config.SWSH_MaxLair.KeepShinies && found[0] > 0) || found[0] == 4)
                    {
                        await Task.Delay(1_500, token).ConfigureAwait(false);
                        for (int i = 1; i < found[0]; i++)
                            await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                        await Click(A, 0_900, token).ConfigureAwait(false);
                        await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                        await Click(A, 2_300, token).ConfigureAwait(false);
                        if (wasVideoClipActive == true)
                        {
                            await PressAndHold(CAPTURE, 2_000, 10_000, token).ConfigureAwait(false);
                            Hub.Config.StopConditions.CaptureVideoClip = true;
                        }
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

            while (i < 4)
            {
                var pointer = new long[] { 0x28F4060, 0x1B0, 0x68, (0x58 + (0x08 * i)), 0xD0, 0x0 };
                var pkm = await ReadUntilPresentPointer(pointer, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);
                if (pkm != null)
                {
                    if (i == 3)
                        found[1] = 1;
                    if (await HandleEncounter(pkm, token).ConfigureAwait(false) || (i < 4 && pkm.IsShiny))
                    {
                        var msg = $"A {(pkm.IsShiny ? "Shiny " : "")}{pkm.Nickname} has been found!";
                        if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                            msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
                        Base.EchoUtil.Echo(msg);
                        Log(msg);
                        found[0] = i + 1;
                    }
                }
                i++;
            }
            return found;
        }

        private async Task<bool> HandleEncounter(PK8 pk, CancellationToken token)
        {
            if (pk == null)
                return false;

            encounterCount++;
            var print = Hub.Config.StopConditions.GetPrintName(pk);

            if (pk.IsShiny)
            {
                Count.AddShinyEncounters();
                if (pk.ShinyXor == 0)
                    print = print.Replace("Shiny: Yes", "Shiny: Square");
                else
                    print = print.Replace("Shiny: Yes", "Shiny: Star");
            }

            Log($"Encounter: {encounterCount}{Environment.NewLine}{print}{Environment.NewLine}");

            var legendary = Legal.Legends.Contains(pk.Species) || Legal.SubLegends.Contains(pk.Species);
            if (legendary)
                Count.AddCompletedLegends();
            else
                Count.AddCompletedEncounters();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
            {
                DumpPokemon(DumpSetting.DumpFolder, legendary ? "legends" : "encounters", pk);
                Count.AddCompletedDumps();
            }

            if (!StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions, null))
                return false;

            var msg = $"Result found!\n{print}";

            if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
            Base.EchoUtil.Echo(msg);
            Log(msg);

            IsWaiting = true;
            while (IsWaiting)
                await Task.Delay(1_000, token).ConfigureAwait(false);
            return false;
        }

        public override async Task HardStop() => await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);

        private bool IsWaiting;
        public void Acknowledge() => IsWaiting = false;
    }
}
