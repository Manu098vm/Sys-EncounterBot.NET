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
    public class EncounterBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredIVs;
        private readonly byte[] BattleMenuReady = { 0, 0, 0, 255 };

        public EncounterBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            DesiredIVs = StopConditionSettings.InitializeTargetIVs(Hub);
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

            var task = Hub.Config.Encounter.EncounteringType switch
            {
                EncounterMode.VerticalLine => WalkInLine(token),
                EncounterMode.HorizontalLine => WalkInLine(token),
                EncounterMode.Eternatus => DoEternatusEncounter(token),
                EncounterMode.Regigigas => DoRegigigasEncounter(token),
                EncounterMode.Regis => DoRegiEncounter(token),
                EncounterMode.LegendaryDogs => DoDogEncounter(token),
                EncounterMode.StatsLiveChecking => LiveStatsChecking(token),
                //SoJ and Spirittomb uses the same routine
                EncounterMode.SwordsJustice => DoJusticeEncounter(token,"Sword of Justice"),
                EncounterMode.Spiritomb => DoJusticeEncounter(token,"Spiritomb"),
                EncounterMode.DynamaxAdventure => DoDynamaxAdventure(token),
                _ => WalkInLine(token),
            };
            await task.ConfigureAwait(false);

            await ResetStick(token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }

        private async Task WalkInLine(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var attempts = await StepUntilEncounter(token).ConfigureAwait(false);
                if (attempts < 0) // aborted
                    continue;

                Log($"Encounter found after {attempts} attempts! Checking details...");

                // Reset stick while we wait for the encounter to load.
                await ResetStick(token).ConfigureAwait(false);

                var pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    Log("Invalid data detected. Restarting loop.");

                    // Flee and continue looping.
                    while (await IsInBattle(token).ConfigureAwait(false))
                        await FleeToOverworld(token).ConfigureAwait(false);
                    continue;
                }

                // Offsets are flickery so make sure we see it 3 times.
                for (int i = 0; i < 3; i++)
                    await ReadUntilChanged(BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);

                if (await HandleEncounter(pk, false, token).ConfigureAwait(false))
                    return;

                Log("Running away...");
                while (await IsInBattle(token).ConfigureAwait(false))
                    await FleeToOverworld(token).ConfigureAwait(false);
            }
        }

        private async Task DoEternatusEncounter(CancellationToken token)
        {
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.EncounterBot)
            {
                await SetStick(LEFT, 0, 20_000, 1_000, token).ConfigureAwait(false);
                await ResetStick(token).ConfigureAwait(false);

                var pk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk != null)
                {
                    if (await HandleEncounter(pk, true, token).ConfigureAwait(false))
                        return;
                }

                Connection.Log("Resetting Eternatus by restarting the game");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task DoRegigigasEncounter(CancellationToken token)
        {
            Log("Reminder: LDN-MITM SYSMODULE IS REQUIRED IN ORDER FOR THIS BOT TO WORK!");
            while (!token.IsCancellationRequested)
            {
                Log("Looking for Gigas...");

                //Click through all the menus until the encounter.
                while (!await IsInBattle(token).ConfigureAwait(false))
                    await Click(A, 1_000, token).ConfigureAwait(false);

                Log("An encounter has started!");

                var pk = await ReadUntilPresent(RaidPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk != null)
                {
                    if (await HandleEncounter(pk, true, token).ConfigureAwait(false))
                        return;
                }

                Connection.Log("Resetting Regigigas by restarting the game");

                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task DoRegiEncounter(CancellationToken token)
        {
            Log("Reminder: LDN-MITM SYSMODULE IS REQUIRED IN ORDER FOR THIS BOT TO WORK!");
            while (!token.IsCancellationRequested)
            {
                Log("Looking for a titan...");

                // Click through all the menus untill the encounter.
                while (!await IsInBattle(token).ConfigureAwait(false))
                    await Click(A, 1_000, token).ConfigureAwait(false);

                Log("Encounter started! Checking details...");
                var pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    // Flee and continue looping.
                    while (await IsInBattle(token).ConfigureAwait(false))
                        await FleeToOverworld(token).ConfigureAwait(false);
                    continue;
                }

                // Offsets are flickery so make sure we see it 3 times.
                for (int i = 0; i < 3; i++)
                    await ReadUntilChanged(BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);

                if (await HandleEncounter(pk, true, token).ConfigureAwait(false))
                    return;

                Log("Restarting game...");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task DoDogEncounter(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Log("Looking for a new dog...");

                // At the start of each loop, an A press is needed to exit out of a prompt.
                await Click(A, 0_200, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 30000, 1_000, token).ConfigureAwait(false);

                // Encounters Zacian/Zamazenta and clicks through all the menus.
                while (!await IsInBattle(token).ConfigureAwait(false))
                    await Click(A, 0_300, token).ConfigureAwait(false);

                Log("Encounter started! Checking details...");
                var pk = await ReadUntilPresent(LegendaryPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    Log("Invalid data detected. Restarting loop.");
                    continue;
                }

                // Get rid of any stick stuff left over so we can flee properly.
                await ResetStick(token).ConfigureAwait(false);

                // Wait for the entire cutscene.
                await Task.Delay(15_000, token).ConfigureAwait(false);

                // Offsets are flickery so make sure we see it 3 times.
                for (int i = 0; i < 3; i++)
                    await ReadUntilChanged(BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);

                if (await HandleEncounter(pk, true, token).ConfigureAwait(false))
                    return;

                Log("Running away...");
                while (await IsInBattle(token).ConfigureAwait(false))
                    await FleeToOverworld(token).ConfigureAwait(false);

                // Extra delay to be sure we're fully out of the battle.
                await Task.Delay(0_250, token).ConfigureAwait(false);
            }
        }

        private async Task LiveStatsChecking(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (await IsInBattle(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                while (!await IsInBattle(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                Log("Encounter started! Checking details...");
                var pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);

                // Offsets are flickery so make sure we see it 3 times.
                for (int i = 0; i < 3; i++)
                    await ReadUntilChanged(BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);

                if (pk == null)
                    Log("Pokémon Check error!");
                else
                    await HandleEncounter(pk, false, token).ConfigureAwait(false);
            }
        }

        private async Task DoJusticeEncounter(CancellationToken token, String name)
        {
            Log("Reminder: LDN-MITM SYSMODULE IS REQUIRED IN ORDER FOR THIS BOT TO WORK!");
            while (!token.IsCancellationRequested)
            {
                Log("Restarting game...");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);

                Log("Looking for a " + name);
                // Click through all the menus untill the encounter.
                while (!await IsInBattle(token).ConfigureAwait(false))
                    await Click(A, 1_000, token).ConfigureAwait(false);

                Log("Encounter started! Checking details...");
                var pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk == null)
                {
                    // Flee and continue looping.
                    while (await IsInBattle(token).ConfigureAwait(false))
                        await FleeToOverworld(token).ConfigureAwait(false);
                    continue;
                }

                // Offsets are flickery so make sure we see it 3 times.
                for (int i = 0; i < 3; i++)
                    await ReadUntilChanged(BattleMenuOffset, BattleMenuReady, 5_000, 0_100, true, token).ConfigureAwait(false);

                if (await HandleEncounter(pk, true, token).ConfigureAwait(false))
                    return;
            }
        }

        private async Task DoDynamaxAdventure(CancellationToken token)
        {
            Log("EXPERIMENTAL!!!!!");
            //Initialization
            string mon = Hub.Config.StopConditions.StopOnSpecies.ToString();
            ushort searchmon = (ushort)Enum.Parse(typeof(LairSpecies), "Articuno");
            byte[] demageStandardState = BitConverter.GetBytes(0x7900E808);
            byte[] demageAlteredState = BitConverter.GetBytes(0x7900E81F);
            byte[] demageTemporalState;
            ulong mainbase = await Connection.GetMainNsoBaseAsync(token).ConfigureAwait(false);
            bool wasVideoClipActive = Hub.Config.StopConditions.CaptureVideoClip;

            //Set Lair Species to Hunt
            if (Enum.IsDefined(typeof(LairSpecies), mon))
                searchmon = (ushort)Enum.Parse(typeof(LairSpecies), mon);
            else
            {
                Log(mon + " is not an available Species as Lair Boss. StopConditions settings will be reloaded to Articuno as Default. If you want to hunt another Pokémon, please Stop the bot and check your settings.");
                Hub.Config.StopConditions.StopOnSpecies = (Species)144;
            }
            await Connection.WriteBytesAsync(BitConverter.GetBytes(searchmon), LairSpeciesSelector, token);
            Log(mon + " Lair Boss ready to be hunted.");

            while (!token.IsCancellationRequested)
            {
                //Capture video clip is menaged internally
                if (Hub.Config.StopConditions.CaptureVideoClip == true)
                    Hub.Config.StopConditions.CaptureVideoClip = false;

                //Edgecase note: If in a Raid Battle, sometimes the isInLairWait return a wrong statement. The Edgecase is handled here, as result the raidcount for the current streak is broken.
                /*Log("OOOO SONO QUI");
                if (await IsInBattle(token).ConfigureAwait(false))
                    Log("OOOOO SONO DENTRO ALL'IF 1");
                else
                    Log("OOOO SONO DENTRO ALL'IF 2");*/

                //Talk to the Lady
                while (!(await IsInLairWait(token).ConfigureAwait(false) || await IsInBattle(token).ConfigureAwait(false)))
                    await Click(A, 1_000, token).ConfigureAwait(false);

                //Select Solo Adventure
                if (!await IsInBattle(token).ConfigureAwait(false))
                {
                    await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                    await Click(A, 1_000, token).ConfigureAwait(false);
                }

                //MAIN LOOP
                int raidCount = 1;
                bool inBattle = false;
                bool lost = false;
                while (!(await IsInLairEndList(token, 0).ConfigureAwait(false) || lost))
                {
                    await Click(A, 1_000, token).ConfigureAwait(false);
                    if (!await IsInBattle(token).ConfigureAwait(false) && inBattle)
                        inBattle = false;
                    else if (await IsInBattle(token).ConfigureAwait(false) && !inBattle)
                    {
                        //Allows 1HKO
                        demageTemporalState = await Connection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                        if (demageStandardState.SequenceEqual(demageTemporalState))
                        {
                            await Connection.WriteBytesAbsoluteAsync(demageAlteredState, mainbase + demageOutputOffset, token).ConfigureAwait(false);
                            Log("Entered battle, 1HKO Enabled.");
                        }
                        Log("Raid Battle: " + raidCount);
                        inBattle = true;
                        raidCount++;
                    }
                    else if (!await IsInBattle(token).ConfigureAwait(false) && !inBattle)
                    {
                        //Disable 1HKO
                        demageTemporalState = await Connection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                        if (demageAlteredState.SequenceEqual(demageTemporalState))
                        {
                            await Connection.WriteBytesAbsoluteAsync(demageStandardState, mainbase + demageOutputOffset, token).ConfigureAwait(false);
                            Log("Out of battle, 1HKO Disabled.");
                        }
                    }
                    else if (await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                    {
                        lost = true;
                        Log("Lost at first raid.");
                    }
                }

                //Disable 1HKO
                demageTemporalState = await Connection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                if (demageAlteredState.SequenceEqual(demageTemporalState))
                {
                    await Connection.WriteBytesAbsoluteAsync(demageStandardState, mainbase + demageOutputOffset, token).ConfigureAwait(false);
                    Log("End Loop, 1HKO Disabled.");
                }

                //Fucking offsets are different every time the game is rebooted or significant actions are made during gameplay.
                var pk1 = await ReadUntilPresent(0xAF29AA40, 2_000, 0_200, token).ConfigureAwait(false);
                var pk2 = await ReadUntilPresent(0xAF29AC70, 2_000, 0_200, token).ConfigureAwait(false);
                var pk3 = await ReadUntilPresent(0xAF29AEC8, 2_000, 0_200, token).ConfigureAwait(false);
                var pk4 = await ReadUntilPresent(0xAF29B158, 2_000, 0_200, token).ConfigureAwait(false);

                int found = 0;
                //Log(pk1.Species + " " + pk2.Species + " " + pk3.Species + " " + pk4.Species);
                if (pk1 != null) {
                    if(await HandleEncounter(pk1, true, token).ConfigureAwait(false))
                        found = 1;
                }
                if (pk2 != null)
                {
                    if (await HandleEncounter(pk2, true, token).ConfigureAwait(false))
                        found = 2;
                }
                if (pk3 != null)
                {
                    if (await HandleEncounter(pk3, true, token).ConfigureAwait(false))
                        found = 3;
                }
                if (pk4 != null)
                {
                    if (await HandleEncounter(pk4, true, token).ConfigureAwait(false))
                        found = 4;
                }
                if (found > 0)
                {
                    Log("Shiny!!!!!!!!!!!!!!!!!!!!!!!!!");
                    for (int y = 1; y < found; y++)
                        await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                    await Click(A, 0_800, token).ConfigureAwait(false);
                    await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                    await Click(A, 0_800, token).ConfigureAwait(false);
                    await PressAndHold(CAPTURE, 2_000, 1_000, token).ConfigureAwait(false);
                    if (wasVideoClipActive == true)
                        Hub.Config.StopConditions.CaptureVideoClip = true;
                }
                else
                {
                    Log("No result found, starting again");
                    await Click(B, 1_000, token).ConfigureAwait(false);
                    while(!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                        await Click(A, 0_800, token).ConfigureAwait(false);
                }
            }
        }

        private async Task<int> StepUntilEncounter(CancellationToken token)
        {
            Log("Walking around until an encounter...");
            int attempts = 0;
            while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.EncounterBot)
            {
                if (!await IsInBattle(token).ConfigureAwait(false))
                {
                    switch (Hub.Config.Encounter.EncounteringType)
                    {
                        case EncounterMode.VerticalLine:
                            await SetStick(LEFT, 0, -30000, 2_400, token).ConfigureAwait(false);
                            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset

                            // Quit early if we found an encounter on first sweep.
                            if (await IsInBattle(token).ConfigureAwait(false))
                                break;

                            await SetStick(LEFT, 0, 30000, 2_400, token).ConfigureAwait(false);
                            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset
                            break;
                        case EncounterMode.HorizontalLine:
                            await SetStick(LEFT, -30000, 0, 2_400, token).ConfigureAwait(false);
                            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset

                            // Quit early if we found an encounter on first sweep.
                            if (await IsInBattle(token).ConfigureAwait(false))
                                break;

                            await SetStick(LEFT, 30000, 0, 2_400, token).ConfigureAwait(false);
                            await SetStick(LEFT, 0, 0, 0_100, token).ConfigureAwait(false); // reset
                            break;
                    }

                    attempts++;
                    if (attempts % 10 == 0)
                        Log($"Tried {attempts} times, still no encounters.");
                }

                if (await IsInBattle(token).ConfigureAwait(false))
                    return attempts;
            }

            return -1; // aborted
        }

        private async Task<bool> HandleEncounter(PK8 pk, bool legends, CancellationToken token)
        {
            encounterCount++;

            //Star/Square Shiny Recognition
            var showdowntext = ShowdownSet.GetShowdownText(pk);
            if (pk.IsShiny && pk.ShinyXor == 0)
                showdowntext = showdowntext.Replace("Yes", "Square");
            else if(pk.IsShiny)
                showdowntext = showdowntext.Replace("Yes", "Star");

            Log($"Encounter: {encounterCount}{Environment.NewLine}{Environment.NewLine}{showdowntext}{Environment.NewLine}{GetRibbonsList(pk)}{Environment.NewLine}");
            if (legends)
                Counts.AddCompletedLegends();
            else
                Counts.AddCompletedEncounters();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, legends ? "legends" : "encounters", pk);

            if (StopConditionSettings.EncounterFound(pk, DesiredIVs, Hub.Config.StopConditions))
            {
                Log("Result found! Stopping routine execution; restart the bot(s) to search again.");
                if (Hub.Config.StopConditions.CaptureVideoClip)
                {
                    await Task.Delay(Hub.Config.StopConditions.ExtraTimeWaitCaptureVideo, token).ConfigureAwait(false);
                    await PressAndHold(CAPTURE, 2_000, 1_000, token).ConfigureAwait(false);
                }
                return true;
            }
            return false;
        }

        private string GetRibbonsList(PK8 pk)
        {
            string ribbonsList = "Ribbons: ";
            for (var mark = MarkIndex.MarkLunchtime; mark <= MarkIndex.MarkSlump; mark++)
                if (pk.GetRibbon((int)mark))
                    ribbonsList += mark;

            if (ribbonsList.Equals("Ribbons: "))
                ribbonsList += "[]";

            return ribbonsList;
        }

        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }

        private async Task FleeToOverworld(CancellationToken token)
        {
            try
            {
                Log("Start flee");
                // This routine will always escape a battle.
                await Task.Delay(1_000, token).ConfigureAwait(false);
                await Click(DUP, 0_400, token).ConfigureAwait(false);
                await Click(A, 0_400, token).ConfigureAwait(false);
                await Click(B, 0_400, token).ConfigureAwait(false);
                await Click(B, 0_400, token).ConfigureAwait(false);
                Log("End flee");
            } catch (Exception)
            {
                Log("Stuck in there!");
            }
        }

        public enum LairSpecies : ushort
        {
            Articuno = 144,
            Zapdos = 145,
            Moltres = 146,
            Mewtwo = 150,
            Raikou = 243,
            Entei = 244,
            Suicune = 245,
            Lugia = 249,
            HoOh = 250,
            Latias = 380,
            Latios = 381,
            Kyogre = 382,
            Groudon = 383,
            Rayquaza = 384,
            Uxie = 480,
            Mesprit = 481,
            Azelf = 482,
            Dialga = 483,
            Palkia = 484,
            Heatran = 485,
            Giratina = 487,
            Cresselia = 488,
            Tornadus = 641,
            Thundurus = 642,
            Landorus = 645,
            Reshiram = 643,
            Zekrom = 644,
            Kyurem = 646,
            Xerneas = 716,
            Yveltal = 717,
            Zygarde = 718,
            TapuKoko = 785,
            TapuLele = 786,
            TapuBulu = 787,
            TapuFIni = 788,
            Solgaleo = 791,
            Lunala = 792,
            Nihilego = 793,
            Buzzwole = 794,
            Pheromosa = 795,
            Xurkitree = 796,
            Celesteela = 797,
            Kartana = 798,
            Guzzlord = 799,
            Necrozma = 800,
            Stakataka = 805,
            Blacephalon = 806
        }
    }
}
