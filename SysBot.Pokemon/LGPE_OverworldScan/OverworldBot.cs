﻿using PKHeX.Core;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class OverworldBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;

        public OverworldBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
        }

        private int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await LGIdentifyTrainer(token).ConfigureAwait(false);

            Log("Starting main EncounterBot loop.");
            Config.IterateNextRoutine();

            await ResetStick(token).ConfigureAwait(false);

            var task = Hub.Config.LGPE_OverworldScan.Routine switch
            {
                LGPEOverworldMode.OverworldSpawn => Overworld(token),
                LGPEOverworldMode.WildBirds => Overworld(token, true),
                LGPEOverworldMode.TestRoutine => Test(token),
                _ => Test(token),
            };
            await task.ConfigureAwait(false);

            await ResetStick(token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }
        private async Task Overworld(CancellationToken token, bool birds = false)
        {
            GameVersion version = await LGWhichGameVersion(token).ConfigureAwait(false);
            List<int[]> movementslist = ParseMovements();
            bool firstrun = movementslist.Count > 0;
            Stopwatch stopwatch = new Stopwatch();
            uint prev = 0;
            uint newspawn;
            uint catchcombo;
            uint speciescombo;
            int i = 0;
            bool freeze = false;
            bool searchforshiny = Hub.Config.LGPE_OverworldScan.OnlyShiny;
            bool found;

            if (movementslist.Count > 0)
                Log($"{Environment.NewLine}----------------------------------------{Environment.NewLine}" +
                    $"ATTENTION{Environment.NewLine}Any wild battles will broke the movement routine, resulting in the pg moving to unwanted areas!{Environment.NewLine}" +
                    $"----------------------------------------{Environment.NewLine}" +
                    $"ATTENTION{Environment.NewLine}Unexpected behaviour can occur if a Pokémon is detected while changing area. It is higlhy recommended to avoid that.{Environment.NewLine}" +
                    $"-----------------------------------------{Environment.NewLine}");

            //Catch combo to increment spawn quality and shiny rate (Thanks to Lincoln-LM for the offsets)
            if ((int)Hub.Config.LGPE_OverworldScan.ChainSpecies > 0)
            {
                speciescombo = await LGReadSpeciesCombo(token).ConfigureAwait(false);
                if ((speciescombo != (uint)Hub.Config.LGPE_OverworldScan.ChainSpecies) && (Hub.Config.LGPE_OverworldScan.ChainSpecies != 0))
                {
                    Log($"Current catch combo being on {(speciescombo == 0 ? "None" : SpeciesName.GetSpeciesName((int)speciescombo, 2))}, changing to {Hub.Config.LGPE_OverworldScan.ChainSpecies}.");
                    await LGEditSpeciesCombo((uint)Hub.Config.LGPE_OverworldScan.ChainSpecies, token).ConfigureAwait(false);
                    speciescombo = await LGReadSpeciesCombo(token).ConfigureAwait(false);
                    Log($"Current catch combo being now on {(speciescombo == 0 ? "None" : SpeciesName.GetSpeciesName((int)speciescombo, 2))}.");
                }
            }
                
            if (Hub.Config.LGPE_OverworldScan.ChainCount > 0)
            {
                catchcombo = await LGReadComboCount(token).ConfigureAwait(false);
                if (catchcombo < (uint)Hub.Config.LGPE_OverworldScan.ChainCount)
                {
                    Log($"Current catch combo being {catchcombo}, incrementing to {Hub.Config.LGPE_OverworldScan.ChainCount}.");
                    await LGEditComboCount((uint)Hub.Config.LGPE_OverworldScan.ChainCount, token).ConfigureAwait(false);
                    catchcombo = await LGReadComboCount(token).ConfigureAwait(false);
                    Log($"Current catch combo being now {catchcombo}.");
                }
            }

            //Main Loop
            while (!token.IsCancellationRequested)
            {
                if (searchforshiny)
                    await LGZaksabeast(token, version).ConfigureAwait(false);

                //Main Loop
                while (!freeze && !token.IsCancellationRequested)
                {
                    if (await LGCountMilliseconds(Hub.Config, token).ConfigureAwait(false) > 0 || !searchforshiny)
                    {
                        //Force the Fortune Teller Nature value, value is reset at the end of the day
                        if (Hub.Config.LGPE_OverworldScan.SetFortuneTellerNature != Nature.Random && !await LGIsNatureTellerEnabled(token).ConfigureAwait(false))
                        {
                            await LGEnableNatureTeller(token).ConfigureAwait(false);
                            await LGEditWildNature(Hub.Config.LGPE_OverworldScan.SetFortuneTellerNature, token).ConfigureAwait(false);
                            Log($"Fortune Teller enabled, Nature set to {await LGReadWildNature(token).ConfigureAwait(false)}.");
                        }

                        //PG Movements. The routine need to continue and check the overworld spawns, cannot be stuck at changing stick position.
                        if (movementslist.Count > 0)
                        {
                            if (stopwatch.ElapsedMilliseconds >= movementslist.ElementAt(i)[2] || firstrun)
                            {
                                if (firstrun)
                                    firstrun = false;
                                await ResetStick(token).ConfigureAwait(false);
                                await SetStick(RIGHT, (short)(movementslist.ElementAt(i)[0]), (short)(movementslist.ElementAt(i)[1]), 0_001, token).ConfigureAwait(false);
                                i++;
                                if (i == movementslist.Count)
                                    i = 0;
                                stopwatch.Restart();
                            }
                        }

                        //Check is inside an unwanted encounter
                        if (await LGIsInCatchScreen(token).ConfigureAwait(false))
                            await EscapeWildEncounter(token).ConfigureAwait(false);

                        //Check new spawns
                        newspawn = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawn, 2, token).ConfigureAwait(false), 0);
                        if (newspawn != prev)
                        {
                            if (newspawn != 0)
                            {
                                encounterCount++;
                                if (IsPKLegendary((int)newspawn))
                                    Counts.AddLGPELegendaryScans();
                                else
                                    Counts.AddLGPEOverworldScans();
                                Log($"New spawn ({encounterCount}): {newspawn} {SpeciesName.GetSpeciesName((int)newspawn, 4)}");
                            }
                            prev = newspawn;
                            if (!searchforshiny &&
                                ((!birds && (int)newspawn == (int)Hub.Config.LGPE_OverworldScan.StopOnSpecies) ||
                                (!birds && (int)Hub.Config.LGPE_OverworldScan.StopOnSpecies == 0) ||
                                (birds && ((int)newspawn == 144 || (int)newspawn == 145 || (int)newspawn == 146))))
                            {
                                await Click(X, 1_000, token).ConfigureAwait(false);
                                await Click(HOME, 1_000, token).ConfigureAwait(false);
                                if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                                    Log($"<@{Hub.Config.Discord.UserTag}> stop conditions met, restart the bot(s) to search again.");
                                else
                                    Log("Stop conditions met, restart the bot(s) to search again.");
                                return;
                            }
                        }
                    }
                    else if (searchforshiny)
                        freeze = true;
                }

                await LGUnfreeze(token, version).ConfigureAwait(false);
                freeze = false;
                newspawn = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawn, 2, token).ConfigureAwait(false), 0);

                //Stop Conditions
                if (birds && ((int)newspawn == 144 || (int)newspawn == 145 || (int)newspawn == 146) && !token.IsCancellationRequested)
                    found = true;
                else if ((!birds && (int)Hub.Config.LGPE_OverworldScan.StopOnSpecies > 0 && (int)newspawn == (int)Hub.Config.LGPE_OverworldScan.StopOnSpecies) ||
                        (!birds && (int)Hub.Config.LGPE_OverworldScan.StopOnSpecies == 0))
                    found = true;
                else
                    found = false;

                encounterCount++;
                Counts.AddShinyEncounters();
                if (IsPKLegendary((int)newspawn))
                    Counts.AddLGPELegendaryScans();
                else
                    Counts.AddLGPEOverworldScans();

                if (!found && !token.IsCancellationRequested)
                    Log($"New spawn ({encounterCount}): {newspawn} Shiny {SpeciesName.GetSpeciesName((int)newspawn, 4)}");
                else if (found && !token.IsCancellationRequested)
                {
                    await ResetStick(token).ConfigureAwait(false);
                    if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                        Log($"<@{Hub.Config.Discord.UserTag}> Shiny Target {SpeciesName.GetSpeciesName((int)newspawn, 4)} found!");
                    else
                        Log($"Shiny Target {SpeciesName.GetSpeciesName((int)newspawn, 4)} found!");
                    await Click(X, 1_000, token).ConfigureAwait(false);
                    await Click(HOME, 1_000, token).ConfigureAwait(false);
                    return;
                }

            }
            await ResetStick(token).ConfigureAwait(false);
            if (searchforshiny)
                await LGUnfreeze(token, version).ConfigureAwait(false);
        }

        private async Task Test(CancellationToken token)
        {
            var task = Hub.Config.LGPE_OverworldScan.TestRoutine switch
            {
                LetsGoTest.Unfreeze => LGUnfreeze(token, await LGWhichGameVersion(token).ConfigureAwait(false)),
                LetsGoTest.TestOffsets => TestOffsets(token),
                LetsGoTest.EscapeFromBattle => TestEscape(token),
                _ => TestOffsets(token),
            };
            await task.ConfigureAwait(false);
            Log("Done.");
            return;
        }
        private async Task TestOffsets(CancellationToken token)
        {
            GameVersion version = await LGWhichGameVersion(token).ConfigureAwait(false);
            long waitms;
            long maxms = 0;
            int i = 0;

            Log("Testing Game Version...");
            if (version == GameVersion.GP)
                Log("OK: Let's Go Pikachu.");
            else if (version == GameVersion.GE)
                Log("OK: Let's Go Eevee.");
            else
                Log("FAILED: Incompatible game or update.");

            Log("Testing Shiny Value...");
            var data = await SwitchConnection.ReadBytesMainAsync(version == GameVersion.GP ? PShinyValue : EShinyValue, 4, token).ConfigureAwait(false);
            byte[] compare = new byte[] { 0xE0, 0x02, 0x00, 0x54 };

            if (data.SequenceEqual(compare))
                Log($"OK: {BitConverter.ToString(data)}");
            else
                Log($"FAILED: {BitConverter.ToString(data)} should be {BitConverter.ToString(compare)}.");

            Log("Testing generating function...");
            data = await SwitchConnection.ReadBytesMainAsync(version == GameVersion.GP ? PGeneratingFunction1 : EGeneratingFunction1, 4, token).ConfigureAwait(false);
            compare = new byte[] { 0xE8, 0x03, 0x00, 0x2A };
            byte[] zak = new byte[] { 0xE9, 0x03, 0x00, 0x2A };
            if (data.SequenceEqual(compare) || data.SequenceEqual(zak))
                Log($"OK: {BitConverter.ToString(data)}");
            else
                Log($"FAILED: {BitConverter.ToString(data)} should be {BitConverter.ToString(compare)}.");

            while (!token.IsCancellationRequested)
            {
                i++;
                Log($"Checking freezing value, attempt n.{i}...");
                waitms = await LGCountMilliseconds(Hub.Config, token).ConfigureAwait(false);
                if (waitms > 0)
                {
                    if (waitms > maxms)
                        maxms = waitms;
                    Log($"OK: 0x1610EE0 changed after {waitms}ms");
                }
                else
                    Log("FAILED: 0x1610EE0 not changed.");
                if (i >= Hub.Config.LGPE_OverworldScan.FreezingTestCount)
                {
                    Log($"Test completed. MaxMS value: {maxms}");
                    return;
                }
            }
        }
        private async Task TestEscape(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
                if (await LGIsInCatchScreen(token).ConfigureAwait(false))
                    await EscapeWildEncounter(token).ConfigureAwait(false);
        }

        private async Task EscapeWildEncounter(CancellationToken token)
        {
            await ResetStick(token).ConfigureAwait(false);
            Log($"Unwanted encounter detected!");
            int i = 0;
            while (await LGIsInCatchScreen(token).ConfigureAwait(false) && !token.IsCancellationRequested)
            {
                i++;
                await Task.Delay(8_000, token).ConfigureAwait(false);
                if (i > 2)
                    await Click(B, 1_200, token).ConfigureAwait(false);
                await Click(B, 1_200, token).ConfigureAwait(false);
                await Click(A, 1_000, token).ConfigureAwait(false);
                await Task.Delay(6_500, token).ConfigureAwait(false);
            }
            Log($"Exited wild encounter.");
        }
        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }

        private List<int[]> ParseMovements()
        {
            List<int[]> buttons = new List<int[]>();
            string movements = Hub.Config.LGPE_OverworldScan.MovementOrder.ToUpper() + ",";
            int index = 0;
            string word = "";

            while (index < movements.Length - 1)
            {
                if ((movements.Length > 1 && (movements[index + 1] == ',' || movements[index + 1] == '.')) || movements.Length == 1)
                {
                    word += movements[index];
                    if (word.Equals("UP"))
                        buttons.Add(new int[] { 0, 30_000, Hub.Config.LGPE_OverworldScan.MoveUpMs });
                    else if (word.Equals("RIGHT"))
                        buttons.Add(new int[] { 30_000, 0, Hub.Config.LGPE_OverworldScan.MoveRightMs });
                    else if (word.Equals("DOWN"))
                        buttons.Add(new int[] { 0, -30_000, Hub.Config.LGPE_OverworldScan.MoveDownMs });
                    else if (word.Equals("LEFT"))
                        buttons.Add(new int[] { -30_000, 0, Hub.Config.LGPE_OverworldScan.MoveLeftMs });
                    movements.Remove(0, 1);
                    word = "";
                }
                else if (movements[index] == ',' || movements[index] == '.' || movements[index] == ' ' || movements[index] == '\n' || movements[index] == '\t' || movements[index] == '\0')
                    movements.Remove(0, 1);
                else
                {
                    word += movements[index];
                    movements.Remove(0, 1);
                }
                index++;
            }
            return buttons;
        }
    }
}
