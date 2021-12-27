using PKHeX.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.Linq;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets7B;

namespace SysBot.Pokemon
{
    public class BotOverworld7B : PokeRoutineExecutor7B, IEncounterBot
    {
        protected readonly PokeBotHub<PK8> Hub;
        protected readonly Overworld7BSettings Settings;
        public ICountSettings Counts => Settings;
        public BotOverworld7B(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.LGPE_OverworldScan;
        }

        protected int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            await InitializeHardware(Settings, token).ConfigureAwait(false);

            try
            {
                Log($"Starting main {GetType().Name} loop.");
                Config.IterateNextRoutine();

                // Clear out any residual stick weirdness.
                await ResetStick(token).ConfigureAwait(false);
                var task = Hub.Config.LGPE_OverworldScan.Routine switch
                {
                    LGPEOverworldMode.OverworldSpawn => Overworld(token),
                    LGPEOverworldMode.WildBirds => Overworld(token, true),
                    _ => Test(token),
                };
                await task.ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception e)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Log(e.Message);
            }

            Log($"Ending {GetType().Name} loop.");
            await HardStop().ConfigureAwait(false);
        }

        private async Task Overworld(CancellationToken token, bool birds = false)
        {
            var version = await CheckGameVersion(token).ConfigureAwait(false);
            var movementslist = ParseMovements(Settings.MovementOrder, Settings.MoveUpMs, Settings.MoveRightMs, Settings.MoveDownMs, Settings.MoveLeftMs);
            var firstrun = movementslist.Count > 0;
            var stopwatch = new Stopwatch();
            var prev = (uint)0;
            var i = 0;
            var freeze = false;
            var searchforshiny = Settings.OnlyShiny;
            uint newspawn;
            uint catchcombo;
            uint speciescombo;
            bool found;

            if (movementslist.Count > 0)
                Log($"{Environment.NewLine}----------------------------------------{Environment.NewLine}" +
                    $"ATTENTION{Environment.NewLine}Any wild battles will broke the movement routine, resulting in the pg moving to unwanted areas!{Environment.NewLine}" +
                    $"----------------------------------------{Environment.NewLine}" +
                    $"ATTENTION{Environment.NewLine}Unexpected behaviour can occur if a Pokémon is detected while changing area. It is higlhy recommended to avoid that.{Environment.NewLine}" +
                    $"-----------------------------------------{Environment.NewLine}");

            //Catch combo to increment spawn quality and shiny rate (Thanks to Lincoln-LM for the offsets)
            if ((int)Settings.ChainSpecies > 0)
            {
                speciescombo = await ReadSpeciesCombo(token).ConfigureAwait(false);
                if ((speciescombo != (uint)Settings.ChainSpecies) && (Settings.ChainSpecies != 0))
                {
                    Log($"Current catch combo being on {(speciescombo == 0 ? "None" : SpeciesName.GetSpeciesName((int)speciescombo, 2))}, changing to {Settings.ChainSpecies}.");
                    await EditSpeciesCombo((uint)Settings.ChainSpecies, token).ConfigureAwait(false);
                    speciescombo = await ReadSpeciesCombo(token).ConfigureAwait(false);
                    Log($"Current catch combo being now on {(speciescombo == 0 ? "None" : SpeciesName.GetSpeciesName((int)speciescombo, 2))}.");
                }
            }

            if (Settings.ChainCount > 0)
            {
                catchcombo = await ReadComboCount(token).ConfigureAwait(false);
                if (catchcombo < (uint)Settings.ChainCount)
                {
                    Log($"Current catch combo being {catchcombo}, incrementing to {Settings.ChainCount}.");
                    await EditComboCount((uint)Settings.ChainCount, token).ConfigureAwait(false);
                    catchcombo = await ReadComboCount(token).ConfigureAwait(false);
                    Log($"Current catch combo being now {catchcombo}.");
                }
            }

            //Main Loop
            while (!token.IsCancellationRequested)
            {
                if (searchforshiny)
                    await Zaksabeast(token, version).ConfigureAwait(false);

                //Main Loop
                while (!freeze && !token.IsCancellationRequested)
                {
                    if (await CountMilliseconds(Hub.Config, token).ConfigureAwait(false) > 0 || !searchforshiny)
                    {
                        //Force the Fortune Teller Nature value, value is reset at the end of the day
                        if (Settings.SetFortuneTellerNature != Nature.Random && !await IsNatureTellerEnabled(token).ConfigureAwait(false))
                        {
                            await EnableNatureTeller(token).ConfigureAwait(false);
                            await EditWildNature(Settings.SetFortuneTellerNature, token).ConfigureAwait(false);
                            Log($"Fortune Teller enabled, Nature set to {await ReadWildNature(token).ConfigureAwait(false)}.");
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
                        if (await IsInCatchScreen(token).ConfigureAwait(false))
                            await FleeToOverworld(token).ConfigureAwait(false);

                        //Check new spawns
                        newspawn = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawn, 2, token).ConfigureAwait(false), 0);
                        if (newspawn != prev)
                        {
                            if (newspawn != 0)
                            {
                                encounterCount++;
                                Settings.AddCompletedScans();

                                var msg = $"New spawn ({encounterCount}): {newspawn} {SpeciesName.GetSpeciesName((int)newspawn, 4)}";
                                Log(msg);
                            }
                            prev = newspawn;
                            if (!searchforshiny &&
                                ((!birds && (int)newspawn == (int)Settings.StopOnSpecies) ||
                                (!birds && (int)Settings.StopOnSpecies == 0) ||
                                (birds && ((int)newspawn == 144 || (int)newspawn == 145 || (int)newspawn == 146))))
                            {
                                await Click(X, 1_000, token).ConfigureAwait(false);
                                await Click(HOME, 1_000, token).ConfigureAwait(false);

                                var msg = "Stop conditions met, restart the bot(s) to search again.";
                                if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                                    msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
                                EchoUtil.Echo(msg);
                                Log(msg);

                                IsWaiting = true;
                                while (IsWaiting)
                                    await Task.Delay(1_000, token).ConfigureAwait(false);

                                return;
                            }
                        }
                    }
                    else if (searchforshiny)
                        freeze = true;
                }

                await Unfreeze(token, version).ConfigureAwait(false);
                freeze = false;
                newspawn = BitConverter.ToUInt16(await Connection.ReadBytesAsync(LastSpawn, 2, token).ConfigureAwait(false), 0);

                //Stop Conditions
                if (birds && ((int)newspawn == 144 || (int)newspawn == 145 || (int)newspawn == 146) && !token.IsCancellationRequested)
                    found = true;
                else if ((!birds && (int)Settings.StopOnSpecies > 0 && (int)newspawn == (int)Settings.StopOnSpecies) ||
                        (!birds && (int)Settings.StopOnSpecies == 0))
                    found = true;
                else
                    found = false;

                encounterCount++;
                Settings.AddCompletedScans();
                Settings.AddCompletedShiny();

                if (!found && !token.IsCancellationRequested)
                    Log($"New spawn ({encounterCount}): {newspawn} Shiny {SpeciesName.GetSpeciesName((int)newspawn, 4)}");
                else if (found && !token.IsCancellationRequested)
                {
                    await ResetStick(token).ConfigureAwait(false);
                    await Click(X, 1_000, token).ConfigureAwait(false);
                    await Click(HOME, 1_000, token).ConfigureAwait(false);

                    var msg = $"Shiny Target {SpeciesName.GetSpeciesName((int)newspawn, 4)} found!";
                    if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                        msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
                    Log(msg);

                    return;
                }

            }
            await ResetStick(token).ConfigureAwait(false);
            if (searchforshiny)
                await Unfreeze(token, version).ConfigureAwait(false);
        }

        private async Task Test(CancellationToken token)
        {
            var task = Settings.TestRoutine switch
            {
                LetsGoTest.Unfreeze => Unfreeze(token, await CheckGameVersion(token).ConfigureAwait(false)),
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
            var version = await CheckGameVersion(token).ConfigureAwait(false);
            var i = 0;
            var maxms = (long)0;
            long waitms;
            

            Log("Testing Game Version...");
            if (version is GameVersion.GP)
                Log("OK: Let's Go Pikachu.");
            else if (version is GameVersion.GE)
                Log("OK: Let's Go Eevee.");
            else
                Log("FAILED: Incompatible game or update.");

            Log("Testing Shiny Value...");
            var data = await SwitchConnection.ReadBytesMainAsync(version == GameVersion.GP ? PShinyValue : EShinyValue, 4, token).ConfigureAwait(false);
            var compare = new byte[] { 0xE0, 0x02, 0x00, 0x54 };

            if (data.SequenceEqual(compare))
                Log($"OK: {BitConverter.ToString(data)}");
            else
                Log($"FAILED: {BitConverter.ToString(data)} should be {BitConverter.ToString(compare)}.");

            Log("Testing generating function...");
            data = await SwitchConnection.ReadBytesMainAsync(version == GameVersion.GP ? PGeneratingFunction1 : EGeneratingFunction1, 4, token).ConfigureAwait(false);
            compare = new byte[] { 0xE8, 0x03, 0x00, 0x2A };
            var zak = new byte[] { 0xE9, 0x03, 0x00, 0x2A };
            if (data.SequenceEqual(compare) || data.SequenceEqual(zak))
                Log($"OK: {BitConverter.ToString(data)}");
            else
                Log($"FAILED: {BitConverter.ToString(data)} should be {BitConverter.ToString(compare)}.");

            while (!token.IsCancellationRequested)
            {
                i++;
                Log($"Checking freezing value, attempt n.{i}...");
                waitms = await CountMilliseconds(Hub.Config, token).ConfigureAwait(false);
                if (waitms > 0)
                {
                    if (waitms > maxms)
                        maxms = waitms;
                    Log($"OK: 0x1610EE0 changed after {waitms}ms");
                }
                else
                    Log("FAILED: 0x1610EE0 not changed.");
                if (i >= Settings.FreezingTestCount)
                {
                    Log($"Test completed. MaxMS value: {maxms}");
                    return;
                }
            }
        }
        private async Task TestEscape(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
                if (await IsInCatchScreen(token).ConfigureAwait(false))
                    await FleeToOverworld(token).ConfigureAwait(false);
        }

        public override async Task HardStop()
        {
            await ResetStick(CancellationToken.None).ConfigureAwait(false);
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        private bool IsWaiting;
        public void Acknowledge() => IsWaiting = false;

        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }

        private async Task FleeToOverworld(CancellationToken token)
        {
            await ResetStick(token).ConfigureAwait(false);
            Log($"Unwanted encounter detected!");
            int i = 0;
            while (await IsInCatchScreen(token).ConfigureAwait(false) && !token.IsCancellationRequested)
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
    }
}
