﻿using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;
using PKHeX.Core;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public sealed class EncounterBotLair : EncounterBot
    {
        public EncounterBotLair(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg, hub)
        {
        }

        protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
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

            Log($"Ending {nameof(EncounterBotLair)} loop.");
            await HardStop().ConfigureAwait(false);
        }

        private int completedAdventures;
        private const uint standardDemage = 0x7900E808;
        private const uint alteredDemage = 0x7900E81F;

        private async Task InnerLoop(CancellationToken token)
        {
            var target = Settings.MaxLairSettings.EditLairPath;
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
                //Talk to the Lady
                Log("Talking to lair lady...");
                while (!await IsInLairWait(token).ConfigureAwait(false))
                    await Click(A, 0_500, token).ConfigureAwait(false);

                await Task.Delay(2_000, token).ConfigureAwait(false);

                //Select Solo Adventure
                Log("Select Solo...");
                await Click(DDOWN, 1_800, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);

                //MAIN LOOP
                var raidCount = 1;
                var inBattle = false;
                var lost = false;

                //Allows 1HKO
                if (Settings.MaxLairSettings.InstantKill)
                {
                    Log("1HKO enabled.");
                    var demageTemporalState = await SwitchConnection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                    if (BitConverter.GetBytes(standardDemage).SequenceEqual(demageTemporalState))
                        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(alteredDemage), demageOutputOffset, token).ConfigureAwait(false);
                }

                Log("Main loop started.");
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
                            Log($"Raid Battle {raidCount}: ({pk.Species}) {pk.Nickname}");
                        else
                            Log($"Raid Battle {raidCount}.{Environment.NewLine}RAM probably shifted. It is suggested to reboot the game or console.");

                        inBattle = true;
                        raidCount++;
                        stopwatch.Restart();
                    }
                    else if (await IsInBattle(token).ConfigureAwait(false) && inBattle)
                    {
                        if (stopwatch.ElapsedMilliseconds > 120_000)
                        {
                            Log("Stuck in a battle, trying to change move.");
                            for (int j = 0; j < 50; j++)
                                await Click(B, 0_100, token).ConfigureAwait(false);
                            await Click(A, 0_500, token).ConfigureAwait(false);
                            await Click(DDOWN, 0_500, token).ConfigureAwait(false);
                            stopwatch.Restart();
                        }
                    }
                }

                //Disable 1HKO
                if (Settings.MaxLairSettings.InstantKill)
                {
                    Log("1HKO disabled.");
                    var demageTemporalState = await SwitchConnection.ReadBytesMainAsync(demageOutputOffset, 4, token).ConfigureAwait(false);
                    if (BitConverter.GetBytes(alteredDemage).SequenceEqual(demageTemporalState))
                        await SwitchConnection.WriteBytesMainAsync(BitConverter.GetBytes(standardDemage), demageOutputOffset, token).ConfigureAwait(false);
                }

                if (!lost)
                {
                    //Check for shinies, check all the StopConditions for the Legendary
                    (var selection, var legendary_defeated, var stopConditions_match) = await IsAdventureHuntFound(token).ConfigureAwait(false);

                    completedAdventures++;
                    if (raidCount < 5)
                        Log($"Lost at battle n. {(raidCount - 1)}, adventure n. {completedAdventures}.");
                    else if (!legendary_defeated)
                        Log($"Lost at battle n. 4, adventure n. {completedAdventures}.");
                    else
                        Log($"Adventure n. {completedAdventures} completed.");

                    if (selection > 0)
                    {
                        var pk = await ReadLairResult(selection-1, token).ConfigureAwait(false);

                        await Task.Delay(1_500, token).ConfigureAwait(false);
                        for (int i = 1; i < selection; i++)
                            await Click(DDOWN, 1_000, token).ConfigureAwait(false);
                        await Click(A, 0_900, token).ConfigureAwait(false);
                        await Click(DDOWN, 0_800, token).ConfigureAwait(false);
                        await Click(A, 2_300, token).ConfigureAwait(false);
                        if (Hub.Config.StopConditions.CaptureVideoClip == true)
                            await PressAndHold(CAPTURE, 2_000, 10_000, token).ConfigureAwait(false);

                        if (pk != null && stopConditions_match)
                        {
                            Log($"Found match in result n. {selection}: {(Species)pk.Species}");
                            return;
                        }
                        else
                        {
                            if(pk != null)
                                Log($"Found shiny in result n. {selection}: {(Species)pk.Species}");

                            await Task.Delay(1_500, token).ConfigureAwait(false);
                            await Click(B, 1_500, token).ConfigureAwait(false);
                            while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                                await Click(A, 1_000, token).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        Log("No result found, starting again.");
                        await Task.Delay(1_500, token).ConfigureAwait(false);
                        await Click(B, 1_000, token).ConfigureAwait(false);
                        while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                            await Click(A, 1_000, token).ConfigureAwait(false);
                        Log("Back to overworld. Restarting the routine...");
                    }
                }
            }
        }

        private async Task<(int, bool, bool)> IsAdventureHuntFound(CancellationToken token)
        {
            var selection = 0;
            var legendary_defeated = false;
            var stopConditions_match = false;
            int i = 0;

            while (i < 4)
            {
                var pkm = await ReadLairResult(i, token).ConfigureAwait(false);
                if (pkm != null)
                {
                    if (i == 3)
                        legendary_defeated = true;

                    if(await HandleEncounter(pkm, token, true).ConfigureAwait(false))
					{
                        selection = i + 1;
                        stopConditions_match = true;
                    }
                    else if (pkm.IsShiny && Hub.Config.SWSH_Encounter.MaxLairSettings.KeepShinies && !stopConditions_match)
                        selection = i + 1;
                }
                i++;
            }
            return (selection, legendary_defeated, stopConditions_match);
        }

        private async Task<PK8?> ReadLairResult(int slot, CancellationToken token)
        {
            var pointer = new long[] { 0x28F4060, 0x1B0, 0x68, 0x58 + 0x08 * slot, 0x58, 0x0 };
            var pkm = await ReadUntilPresentPointer(pointer, 2_000, 0_200, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (pkm is not null && pkm.Species == 0)
                return null;
            return pkm;
        }

        public override async Task HardStop()
        {
            await CleanExit(Hub.Config.SWSH_Encounter, CancellationToken.None).ConfigureAwait(false);
        }
    }
}
