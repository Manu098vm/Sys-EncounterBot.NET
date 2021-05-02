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
    public class Letsgo : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly BotCompleteCounts Counts;
        private readonly IDumper DumpSetting;
        private readonly int[] DesiredIVs;

        public Letsgo(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            DesiredIVs = StopConditionSettings.InitializeTargetIVs(Hub);
        }

        private int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            //TODO: IdentifyTrainer routine for let's go instead of SwSh
            Log("Identifying trainer data of the host console.");
            await LGIdentifyTrainer(token).ConfigureAwait(false);

            Log("Starting main EncounterBot loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);

            var task = Hub.Config.LetsGoSettings.EncounteringType switch
            {
                LetsGoMode.OverworldSpawn => Overworld(token),
                LetsGoMode.WildBirds => Overworld(token, true),
                LetsGoMode.Trades => Trade(token),
                LetsGoMode.Stationary => Static(token),
                LetsGoMode.Gifts => Gift(token),
                LetsGoMode.TestRoutine => Test(token),
                _ => Test(token),
            };
            await task.ConfigureAwait(false);

            await ResetStick(token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }
        private async Task Overworld(CancellationToken token, bool birds = false)
        {
            Log("Let's GO Overworld Bot Proof of Concept!");
            GameVersion version = await LGWhichGameVersion(token).ConfigureAwait(false);
            uint prev = 0;
            uint newspawn;
            uint catchcombo;
            uint speciescombo;
            int i = 0;
            bool freeze = false;
            bool searchforshiny = Hub.Config.StopConditions.ShinyTarget != TargetShinyType.NonShiny && Hub.Config.StopConditions.ShinyTarget != TargetShinyType.DisableOption;
            bool found = false;

            //Catch combo to increment spawn quality and shiny rate (Thanks to Lincoln-LM for the offsets)
            if ((int)Hub.Config.LetsGoSettings.ChainSpecies > 0)
            {
                speciescombo = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                if ((speciescombo != (uint)Hub.Config.LetsGoSettings.ChainSpecies) && (Hub.Config.LetsGoSettings.ChainSpecies != 0))
                {
                    Log($"Current catch combo being on {(speciescombo == 0 ? "None" : SpeciesName.GetSpeciesName((int)speciescombo, 4))}, changing to {Hub.Config.LetsGoSettings.ChainSpecies}.");
                    await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes((uint)Hub.Config.LetsGoSettings.ChainSpecies), await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), token).ConfigureAwait(false);
                    speciescombo = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(SpeciesComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                    Log($"Current catch combo being on {(speciescombo == 0 ? "None" : SpeciesName.GetSpeciesName((int)speciescombo, 4))}.");
                }
            }
            if (Hub.Config.LetsGoSettings.ChainCount > 0)
            {
                catchcombo = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                if (catchcombo < (uint)Hub.Config.LetsGoSettings.ChainCount)
                {
                    Log($"Current catch combo being {catchcombo}, incrementing to {Hub.Config.LetsGoSettings.ChainCount}.");
                    await SwitchConnection.WriteBytesAbsoluteAsync(BitConverter.GetBytes((uint)Hub.Config.LetsGoSettings.ChainCount), await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), token).ConfigureAwait(false);
                    catchcombo = BitConverter.ToUInt16(await SwitchConnection.ReadBytesAbsoluteAsync(await ParsePointer(CatchComboPointer, token).ConfigureAwait(false), 2, token).ConfigureAwait(false), 0);
                    Log($"Current catch combo being {catchcombo}.");
                }
            }

            while (!token.IsCancellationRequested)
            {
                if(searchforshiny)
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
                                encounterCount++;
                                Log($"New spawn ({i}): {newspawn} {SpeciesName.GetSpeciesName((int)newspawn, 4)}");
                            }
                            prev = newspawn;
                            if (!searchforshiny &&
                                ((!birds && (int)newspawn == (int)Hub.Config.StopConditions.StopOnSpecies) ||
                                (birds && ((int)newspawn == 144 || (int)newspawn == 145 || (int)newspawn == 146)))){
                                    Log("Stop conditions met, restart the bot(s) to search again.");
                                    return;
                            }
                        }
                    }
                    else if (searchforshiny)
                        freeze = true;
                }

                if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag) && searchforshiny)
                    Log($"<@{Hub.Config.Discord.UserTag}> game is freezed, a Shiny has been detected.");
                else
                    Log("Game is freezed. A Shiny has been detected.");

                //Unfreeze to restart the routine, or log the Shiny species.
                await LGUnfreeze(token, version).ConfigureAwait(false);
                newspawn = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawn1, 2, token).ConfigureAwait(false), 0);

                //Stop Conditions logic
                if (birds && (int)newspawn == 144 || (int)newspawn == 145 || (int)newspawn == 146)
                        found = true;
                else if (!birds && (int)Hub.Config.StopConditions.StopOnSpecies > 0 && (int)newspawn == (int)Hub.Config.StopConditions.StopOnSpecies)
                        found = true;
                else
                    found = false;

                if (!found)
                {
                    freeze = false;
                    if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                        Log($"<@{Hub.Config.Discord.UserTag}> {SpeciesName.GetSpeciesName((int)newspawn, 4)} SHINY FOUND but not the target.");
                    else
                        Log($"{SpeciesName.GetSpeciesName((int)newspawn, 4)} SHINY FOUND but not the target.");
                }
                else
                {
                    if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                        Log($"<@{Hub.Config.Discord.UserTag}> SHINY {SpeciesName.GetSpeciesName((int)newspawn, 4)} FOUND!!");
                    else
                        Log($"SHINY {SpeciesName.GetSpeciesName((int)newspawn, 4)} FOUND!!");
                    await Click(X, 1_000, token).ConfigureAwait(false);
                    return;
                }

            }
        }

        private async Task Static(CancellationToken token)
        {
            Log("Ensure to have a powerful Pokémon in the first slot of your team, with a move that can knock out the enemy in a few turns as first move.");
            while (!token.IsCancellationRequested)
            {
                //Spam A until battle starts
                while(!await LGIsInBattle(token).ConfigureAwait(false))
                    await Click(A, 0_500, token).ConfigureAwait(false);

                Log("Battle started, checking details...");

                var pk = await LGReadUntilPresent(StationaryBattleData, 2_000, 0_200, token).ConfigureAwait(false);
                if (pk != null)
                    if (await HandleEncounter(pk, IsPKLegendary(pk.Species), token).ConfigureAwait(false))
                    {
                        Log("Result found, defeating the enemy.");
                        //Spam A until the battle ends
                        while (await LGIsInBattle(token).ConfigureAwait(false) && !await LGIsInCatchScreen(token).ConfigureAwait(false))
                            await Click(A, 0_500, token).ConfigureAwait(false);

                        await Click(HOME, 0_500, token).ConfigureAwait(false);
                        return;

                    }
                Log($"Resetting Static Encounter by restarting the game");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await LGOpenGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task Gift(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                //Click through all the menus until the Gift.
                while (!await LGIsGiftFound(token).ConfigureAwait(false))
                    await Click(A, 0_500, token).ConfigureAwait(false);

                Log("A Gift has been found! Checking details...");

                var pk = await LGReadUntilPresent(TradeData, 2_000, 0_200, token, EncryptedSize, false).ConfigureAwait(false);
                if (pk != null)
                    if (await HandleEncounter(pk, IsPKLegendary(pk.Species), token).ConfigureAwait(false))
                        return;

                Log($"Resetting the Gift Encounter by restarting the game");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await LGOpenGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task Trade(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                //Click through all the menus until the trade.
                while (!await LGIsInTrade(token).ConfigureAwait(false))
                    await Click(A, 0_500, token).ConfigureAwait(false);

                Log("A trade has started! Checking details...");

                var pk = await LGReadUntilPresent(TradeData, 2_000, 0_200, token, EncryptedSize, false).ConfigureAwait(false);
                if (pk != null)
                {
                    if (await HandleEncounter(pk, false, token).ConfigureAwait(false))
                        return;
                }

                Log($"Resetting the trade by restarting the game");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await LGOpenGame(Hub.Config, token).ConfigureAwait(false);
            }
        }

        private async Task Test(CancellationToken token)
        {

            var task = Hub.Config.LetsGoSettings.TestRoutine switch
            {
                LetsGoTest.TestOffsets => TestOffsets(token),
                LetsGoTest.CatchComboTest => TestCatchCombo(token),
                LetsGoTest.CheckGameOpen => TestGameReady(token),
                LetsGoTest.CheckIsInBattle => TestBattle(token),
                LetsGoTest.EscapeFromBattle => TestEscape(token),
                _ => TestOffsets(token),
            };
            await task.ConfigureAwait(false);
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
            byte[] zak = new byte[] { 0xE9, 0x03, 0x00, 0x2A };
            if (data.SequenceEqual(compare) || data.SequenceEqual(zak))
                Log($"OK: {BitConverter.ToString(data)}");
            else
                Log($"FAILED: {BitConverter.ToString(data)} should be {BitConverter.ToString(compare)}.");

            Log("Testing generating function...");
            data = await SwitchConnection.ReadBytesMainAsync(version == GameVersion.GP ? PGeneratingFunction1 : EGeneratingFunction1, 4, token).ConfigureAwait(false);
            compare = new byte[] { 0xE8, 0x03, 0x00, 0x2A };
            if (data.SequenceEqual(compare))
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
                if (i >= Hub.Config.LetsGoSettings.FreezingTestCount)
                {
                    Log($"Test completed. Max WaitValue: {maxms}");
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
                //Log($"Editing to {Hub.Config.LetsGoSettings.ChainSpecies}, at {Hub.Config.LetsGoSettings.ChainCount}");
                //await Connection.WriteBytesAsync(BitConverter.GetBytes((uint)Hub.Config.LetsGoSettings.ChainSpecies), SpeciesCombo, token).ConfigureAwait(false);
                //await SwitchConnection.WriteBytesAsync(BitConverter.GetBytes((uint)Hub.Config.LetsGoSettings.ChainCount), CatchCombo, token).ConfigureAwait(false);
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

        private async Task<bool> HandleEncounter(PB7 pk, bool legends, CancellationToken token)
        {
            encounterCount++;

            //Star/Square Shiny Recognition
            var showdowntext = ShowdownParsing.GetShowdownText(pk);
            if (pk.IsShiny && pk.ShinyXor == 0)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
            else if (pk.IsShiny)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");

            Log($"Encounter: {encounterCount}{Environment.NewLine}{showdowntext}{Environment.NewLine}");
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
        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }
    }
}
