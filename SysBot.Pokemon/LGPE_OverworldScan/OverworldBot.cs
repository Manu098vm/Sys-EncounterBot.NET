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
    public class OverworldBot : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;

        public OverworldBot(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
        }

        public override async Task MainLoop(CancellationToken token)
        {
            //TODO: IdentifyTrainer routine for let's go instead of SwSh
            Log("Identifying trainer data of the host console.");
            await LGIdentifyTrainer(token).ConfigureAwait(false);

            Log("Starting main EncounterBot loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);

            var task = Hub.Config.LGPE_OverworldScanBot.Routine switch
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
            uint prev = 0;
            uint newspawn;
            uint catchcombo;
            uint speciescombo;
            int i = 0;
            bool freeze = false;
            bool searchforshiny = Hub.Config.LGPE_OverworldScanBot.OnlyShiny;
            bool found = false;

            //Catch combo to increment spawn quality and shiny rate (Thanks to Lincoln-LM for the offsets)
            if ((int)Hub.Config.LGPE_OverworldScanBot.ChainSpecies > 0)
            {
                speciescombo = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                if ((speciescombo != (uint)Hub.Config.LGPE_OverworldScanBot.ChainSpecies) && (Hub.Config.LGPE_OverworldScanBot.ChainSpecies != 0))
                {
                    Log($"Current catch combo being on {(speciescombo == 0 ? "None" : SpeciesName.GetSpeciesName((int)speciescombo, 2))}, changing to {Hub.Config.LGPE_OverworldScanBot.ChainSpecies}.");
                    await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes((uint)Hub.Config.LGPE_OverworldScanBot.ChainSpecies), await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), token).ConfigureAwait(false);
                    speciescombo = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                    Log($"Current catch combo being now on {(speciescombo == 0 ? "None" : SpeciesName.GetSpeciesName((int)speciescombo, 2))}.");
                }
            }
            if (Hub.Config.LGPE_OverworldScanBot.ChainCount > 0)
            {
                catchcombo = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                if (catchcombo < (uint)Hub.Config.LGPE_OverworldScanBot.ChainCount)
                {
                    Log($"Current catch combo being {catchcombo}, incrementing to {Hub.Config.LGPE_OverworldScanBot.ChainCount}.");
                    await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes((uint)Hub.Config.LGPE_OverworldScanBot.ChainCount), await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), token).ConfigureAwait(false);
                    catchcombo = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                    Log($"Current catch combo being now {catchcombo}.");
                }
            }

            while (!token.IsCancellationRequested)
            {
                if (searchforshiny)
                    await LGZaksabeast(token, version).ConfigureAwait(false);
                while (freeze == false && !token.IsCancellationRequested && !found)
                {
                    if (await LGCountMilliseconds(Hub.Config, token).ConfigureAwait(false) > 0 || !searchforshiny)
                    {
                        //Check is inside an unwanted encounter
                        if (await LGIsInCatchScreen(token).ConfigureAwait(false))
                        {
                            //TODO HANDLE ENCOUNTER
                            Log($"Unwanted encounter detected!");
                            int j = 0;
                            while (await LGIsInCatchScreen(token).ConfigureAwait(false) && !token.IsCancellationRequested)
                            {
                                j++;
                                await Task.Delay(8_000, token).ConfigureAwait(false);
                                if (j > 2)
                                    await Click(B, 1_200, token).ConfigureAwait(false);
                                await Click(B, 1_200, token).ConfigureAwait(false);
                                await Click(A, 1_000, token).ConfigureAwait(false);
                                await Task.Delay(6_500, token).ConfigureAwait(false);
                            }
                            Log($"Exited wild encounter.");
                        }

                        //Check new spawns
                        newspawn = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawn1, 2, token).ConfigureAwait(false), 0);
                        if (newspawn != prev)
                        {
                            if (newspawn != 0)
                            {
                                i++;
                                if (IsPKLegendary((int)newspawn))
                                    Counts.AddCompletedLegends();
                                else
                                    Counts.AddCompletedEncounters();
                                Log($"New spawn ({i}): {newspawn} {SpeciesName.GetSpeciesName((int)newspawn, 4)}");
                            }
                            prev = newspawn;
                            if (!searchforshiny &&
                                ((!birds && (int)newspawn == (int)Hub.Config.LGPE_OverworldScanBot.StopOnSpecies) ||
                                (!birds && (int)Hub.Config.LGPE_OverworldScanBot.StopOnSpecies == 0) ||
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

                if (searchforshiny && !token.IsCancellationRequested)
                    Log("A Shiny has been detected.");

                //Unfreeze to restart the routine, or log the Shiny species.
                await LGUnfreeze(token, version).ConfigureAwait(false);
                newspawn = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawn1, 2, token).ConfigureAwait(false), 0);

                //Stop Conditions logic
                if (birds && ((int)newspawn == 144 || (int)newspawn == 145 || (int)newspawn == 146) && !token.IsCancellationRequested)
                    found = true;
                else if ((!birds && (int)Hub.Config.LGPE_OverworldScanBot.StopOnSpecies > 0 && (int)newspawn == (int)Hub.Config.LGPE_OverworldScanBot.StopOnSpecies) ||
                        (!birds && (int)Hub.Config.LGPE_OverworldScanBot.StopOnSpecies == 0))
                    found = true;
                else
                    found = false;

                if (!found && !token.IsCancellationRequested)
                {
                    freeze = false;
                    Log($"Shiny {SpeciesName.GetSpeciesName((int)newspawn, 4)} is not the target, the routine will continue.");
                }
                else if (!token.IsCancellationRequested)
                {
                    if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                        Log($"<@{Hub.Config.Discord.UserTag}> Shiny {SpeciesName.GetSpeciesName((int)newspawn, 4)} found!");
                    else
                        Log($"Shiny {SpeciesName.GetSpeciesName((int)newspawn, 4)} found!");
                    await Click(X, 1_000, token).ConfigureAwait(false);
                    await Click(HOME, 1_000, token).ConfigureAwait(false);
                    return;
                }

            }
            await LGUnfreeze(token, version).ConfigureAwait(false);
        }

        private async Task Test(CancellationToken token)
        {

            var task = Hub.Config.LGPE_OverworldScanBot.TestRoutine switch
            {
                LetsGoTest.Unfreeze => LGUnfreeze(token, await LGWhichGameVersion(token).ConfigureAwait(false)),
                LetsGoTest.TestOffsets => TestOffsets(token),
                LetsGoTest.CatchComboTest => TestCatchCombo(token),
                LetsGoTest.CheckGameOpen => TestGameReady(token),
                LetsGoTest.CheckIsInBattle => TestBattle(token),
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
                if (i >= Hub.Config.LGPE_OverworldScanBot.FreezingTestCount)
                {
                    Log($"Test completed. MaxMS value: {maxms}");
                    return;
                }
            }
        }
        private async Task TestCatchCombo(CancellationToken token)
        {
            uint species;
            uint count;
            while (!token.IsCancellationRequested)
            {
                species = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                count = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                Log($"Current catch combo being on {(SpeciesName.GetSpeciesName((int)species, 4)).Replace("Uovo", "None")}, count is at {count}");
                //Log($"Editing to {Hub.Config.LGPE_OverworldScanBot.ChainSpecies}, at {Hub.Config.LGPE_OverworldScanBot.ChainCount}");
                //await Connection.WriteBytesAsync(BitConverter.GetBytes((uint)Hub.Config.LGPE_OverworldScanBot.ChainSpecies), SpeciesCombo, token).ConfigureAwait(false);
                //await SwitchConnection.WriteBytesAsync(BitConverter.GetBytes((uint)Hub.Config.LGPE_OverworldScanBot.ChainCount), CatchCombo, token).ConfigureAwait(false);
            }
        }
        private async Task TestGameReady(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (await LGIsInTitleScreen(token).ConfigureAwait(false))
                    Log("Game is Opened");
                else
                    Log("Game is Closed");
            }
        }
        private async Task TestBattle(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (await LGIsInBattle(token).ConfigureAwait(false))
                    Log("In Battle Scenario!");
                else
                    Log("Not in Battle Scenario!");
            }
        }
        private async Task TestEscape(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (await LGIsInCatchScreen(token).ConfigureAwait(false))
                {
                    //TODO HANDLE ENCOUNTER
                    Log($"Unwanted encounter detected!");
                    int j = 0;
                    while (await LGIsInCatchScreen(token).ConfigureAwait(false) && !token.IsCancellationRequested)
                    {
                        j++;
                        await Task.Delay(8_000, token).ConfigureAwait(false);
                        if (j > 2)
                            await Click(B, 1_200, token).ConfigureAwait(false);
                        await Click(B, 1_200, token).ConfigureAwait(false);
                        await Click(A, 1_000, token).ConfigureAwait(false);
                        await Task.Delay(6_500, token).ConfigureAwait(false);
                    }
                    Log($"Exited wild encounter.");
                }
            }
        }
        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }
    }
}
