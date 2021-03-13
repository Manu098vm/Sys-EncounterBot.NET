using PKHeX.Core;
using System;
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
                EncounterMode.Regis => DoRestartingEncounter(token, (EncounterType)1),
                EncounterMode.Regigigas => DoRestartingEncounter(token, (EncounterType)2),
                EncounterMode.Spiritomb => DoRestartingEncounter(token, (EncounterType)3),
                EncounterMode.SwordsJustice => DoRestartingEncounter(token, (EncounterType)4),
                EncounterMode.Eternatus => DoRestartingEncounter(token, (EncounterType)5),
                EncounterMode.Dogs_or_Calyrex => DoDogEncounter(token),
                EncounterMode.Keldeo => DoKeldeoEncounter(token),
                EncounterMode.Zapdos => DoSeededEncounter(token, (EncounterType)8),
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

        private async Task DoRestartingEncounter(CancellationToken token, EncounterType type)
        {
            uint encounterOffset = (type == (EncounterType)2 || type == (EncounterType)5) ? RaidPokemonOffset : WildPokemonOffset;
            bool isLegendary = (type == (EncounterType)3);
            bool skipRoutine = (type == (EncounterType)3 || type == (EncounterType)4);

            while (!token.IsCancellationRequested)
            {
                if (!skipRoutine)
                {
                    Log($"Looking for {type}...");

                    if (type == (EncounterType)5)
                    {
                        await SetStick(LEFT, 0, 20_000, 1_000, token).ConfigureAwait(false);
                        await ResetStick(token).ConfigureAwait(false);
                    }

                    //Click through all the menus until the encounter.
                    while (!await IsInBattle(token).ConfigureAwait(false))
                        await Click(A, 1_000, token).ConfigureAwait(false);

                    Log("An encounter has started! Checking details...");

                    var pk = await ReadUntilPresent(encounterOffset, 2_000, 0_200, token).ConfigureAwait(false);
                    if (pk != null)
                    {
                        if (await HandleEncounter(pk, isLegendary, token).ConfigureAwait(false))
                            return;
                    }

                    Log($"Resetting {type} by restarting the game");
                }

                skipRoutine = false;
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private PK8 CalculateFromSeed(uint seed, PK8 pkm)
        {
            PK8 pk = pkm;

            int UNSET = -1;
            var xoro = new Sysbot.Pokemon.Xoroshiro128Plus(seed);

            Log("Calculating from seed " + String.Format("{0:X}", seed));

            // EC & PID
            uint EC = (uint)xoro.NextInt(uint.MaxValue);
            uint PID = (uint)xoro.NextInt(uint.MaxValue);

            //IVS
            var ivs = new[] { UNSET, UNSET, UNSET, UNSET, UNSET, UNSET };
            const int MAX = 31;
            for (int i = 0; i < 3; i++)
            {
                int index;
                do { index = (int)xoro.NextInt(6); }
                while (ivs[index] != UNSET);

                ivs[index] = MAX;
            }

            for (int i = 0; i < ivs.Length; i++)
            {
                if (ivs[i] == UNSET)
                    ivs[i] = (int)xoro.NextInt(32);
            }

            pk.EncryptionConstant = EC;
            pk.PID = PID;
            pk.IV_HP = ivs[0];
            pk.IV_ATK = ivs[1];
            pk.IV_DEF = ivs[2];
            pk.IV_SPA = ivs[3];
            pk.IV_SPD = ivs[4];
            pk.IV_SPE = ivs[5];

            return pk;
        }

        private async Task RerollSeedEncounter(CancellationToken token)
        {
            Log("Reroll");
            await Click(X, 2_000, token).ConfigureAwait(false);
            Log("Premuto X");
            await Click(A, 5_000, token).ConfigureAwait(false);
            for (int i = 0; i < 6; i++)
                await Click(A, 0_250, token).ConfigureAwait(false);
            Log("Premuto A x6");
            await Task.Delay(2_000, token).ConfigureAwait(false);
            await Click(X, 2_000, token).ConfigureAwait(false);
            Log("Premuto X");
            for (int i = 0; i < 6; i++)
                await Click(R, 0_250, token).ConfigureAwait(false);
            Log("Premuto R x3");
            await Click(A, 5_000, token).ConfigureAwait(false);
            Log("Premuto A");
        }

        private async Task DoSeededEncounter(CancellationToken token, EncounterType type)
        {
            while (!token.IsCancellationRequested)
            {
                uint seed = BitConverter.ToUInt32(await Connection.ReadBytesAsync(ZECPIDIV, 4, token).ConfigureAwait(false), 0);
                Log($"RAM SEED: {seed}");
                int nature = (await Connection.ReadBytesAsync(ZNature, 1, token).ConfigureAwait(false))[0];
                int mark = (await Connection.ReadBytesAsync(ZMark, 1, token).ConfigureAwait(false))[0];

                SAV8 sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
                PK8 zapdos = new PK8
                {
                    Species = 145,
                    Form = 1,
                    Ability = 1,
                    CurrentLevel = 70,
                    Met_Level = 70,
                    Gender = 2,
                    TrainerID7 = sav.TrainerID7,
                    TrainerSID7 = sav.TrainerSID7,
                    TID = sav.TID,
                    SID = sav.SID,
                    OT_Name = sav.OT,
                    Language = sav.Language,
                };
                if (mark != 255) zapdos.SetRibbon(mark, true);
                zapdos.SetNature(nature);

                if (seed == 0 || seed == 1)
                    await RerollSeedEncounter(token).ConfigureAwait(false);
                else
                {
                    if (await HandleEncounter(CalculateFromSeed(seed, zapdos), true, token).ConfigureAwait(false))
                        return;
                    else
                        await RerollSeedEncounter(token).ConfigureAwait(false);
                }
            }
        }

        private async Task DoDogEncounter(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Log("Looking for a new legendary...");

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

        private async Task DoKeldeoEncounter(CancellationToken token)
        {
            int tries = 0;
            while (!token.IsCancellationRequested)
            {
                await ResetStick(token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 30_000, 1_000, token).ConfigureAwait(false);
                while (!await IsInBattle(token).ConfigureAwait(false) && tries < 15)
                {
                    await Click(LSTICK, 0_000, token);
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                    tries++;
                }
                    
                    
                await ResetStick(token).ConfigureAwait(false);

                if (await IsInBattle(token).ConfigureAwait(false))
                {
                    tries = 0;
                    Log("Encounter started! Checking details...");
                    var pk = await ReadUntilPresent(WildPokemonOffset, 2_000, 0_200, token).ConfigureAwait(false);
                    if (pk == null)
                    {
                        // Flee and continue looping.
                        while (await IsInBattle(token).ConfigureAwait(false))
                            await FleeToOverworld(token).ConfigureAwait(false);
                        continue;
                    }

                    if (await HandleEncounter(pk, true, token).ConfigureAwait(false))
                        return;

                }
                else if(tries >= 15)
                {
                    Log("The starting position is probably wrong. If you see this message more than one time consider change your starting position and save the game again.");
                    tries = 0;
                }

                Log("Restarting game...");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await StartGame(Hub.Config, token).ConfigureAwait(false);
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
            var showdowntext = ShowdownParsing.GetShowdownText(pk);
            if (pk.IsShiny && pk.ShinyXor == 0)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
            else if(pk.IsShiny)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");

            Log($"Encounter: {encounterCount}{Environment.NewLine}{Environment.NewLine}{showdowntext}{Environment.NewLine}{GetRibbonsList(pk)}{Environment.NewLine}");
            if (legends)
                Counts.AddCompletedLegends();
            else
                Counts.AddCompletedEncounters();

            if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
                DumpPokemon(DumpSetting.DumpFolder, legends ? "legends" : "encounters", pk);

            if (StopConditionSettings.EncounterFound(pk, DesiredIVs, Hub.Config.StopConditions))
            {
                if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                    Log($"<@{Hub.Config.Discord.UserTag}> result found! Stopping routine execution; restart the bot(s) to search again.");
                else
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
            // This routine will always escape a battle.
            await Task.Delay(1_000, token).ConfigureAwait(false);
            await Click(DUP, 0_400, token).ConfigureAwait(false);
            await Click(A, 0_400, token).ConfigureAwait(false);
            await Click(B, 0_400, token).ConfigureAwait(false);
            await Click(B, 0_400, token).ConfigureAwait(false);
        }
    }
}
