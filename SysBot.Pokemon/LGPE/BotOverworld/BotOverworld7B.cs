using PKHeX.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using System.IO;
using System.Linq;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets7B;

namespace SysBot.Pokemon
{
    public class BotOverworld7B : PokeRoutineExecutor7B
    {
        protected readonly PokeBotHub<PK8> Hub;
        protected readonly Overworld7BSettings Settings;
        public BotOverworld7B(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Settings = Hub.Config.LGPE_OverworldScan;
        }

        protected int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);
            await InitializeHardware(Settings, token).ConfigureAwait(false);

            try
            {
                Log($"Starting main {GetType().Name} loop.");
                Config.IterateNextRoutine();

                // Clear out any residual stick weirdness.
                await ResetStick(token).ConfigureAwait(false);
                await Overworld(token).ConfigureAwait(false);
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

        private async Task Overworld(CancellationToken token)
        {
            var movementslist = ParseMovements(Settings.MovementOrder, Settings.MoveUpMs, Settings.MoveRightMs, Settings.MoveDownMs, Settings.MoveLeftMs);
            var firstrun = movementslist.Count > 0;
            Stopwatch stopwatch = new();
            var i = 0;

            if (movementslist.Count > 0)
                Log($"{Environment.NewLine}----------------------------------------{Environment.NewLine}" +
                    $"ATTENTION{Environment.NewLine}Wild battles may break the movement routine, resulting in the pg moving to unwanted areas!{Environment.NewLine}" +
                    $"----------------------------------------{Environment.NewLine}" +
                    $"ATTENTION{Environment.NewLine}Unexpected behaviour can occur if a Pokémon is detected while changing area. It is higlhy recommended to avoid that.{Environment.NewLine}" +
                    $"-----------------------------------------{Environment.NewLine}");

            //Check Text Speed
            if (await ReadTextSpeed(token).ConfigureAwait(false) != TextSpeed.Fast)
                await EditTextSpeed(TextSpeed.Fast, token).ConfigureAwait(false);

            //Catch combo to increment spawn quality and shiny rate (Thanks to Lincoln-LM for the offsets)
            await ReadAndEditCatchCombo(token).ConfigureAwait(false);

            //Main Loop
            while (!token.IsCancellationRequested)
            {
                stopwatch.Start();
                while (!token.IsCancellationRequested)
                {
                    //Force the Fortune Teller Nature value, value is reset at the end of the day
                    if (Settings.SetFortuneTellerNature is not Nature.Random &&
                        (!await IsNatureTellerEnabled(token).ConfigureAwait(false) || await ReadWildNature(token).ConfigureAwait(false) != Settings.SetFortuneTellerNature))
                    {
                        await EnableNatureTeller(token).ConfigureAwait(false);
                        await EditWildNature(Settings.SetFortuneTellerNature, token).ConfigureAwait(false);
                        Log($"Fortune Teller enabled, Nature set to {await ReadWildNature(token).ConfigureAwait(false)}.");
                    }

                    //Check Lure Type
                    if (await ReadLureType(token).ConfigureAwait(false) != Settings.SetLure)
                        await EditLureType((uint)Settings.SetLure, token).ConfigureAwait(false);

                    //Check Lure Steps
                    if (Settings.SetLure is not Lure.None && await ReadLureCounter(token).ConfigureAwait(false) < 20)
                        await EditLureCounter(100, token).ConfigureAwait(false);

                    //PG Movements. The routine need to continue and check the overworld spawns, cannot be stuck at changing stick position.
                    if (movementslist.Count > 0)
                    {
                        if (stopwatch.ElapsedMilliseconds >= movementslist.ElementAt(i)[2] || firstrun)
                        {
                            if (firstrun)
                                firstrun = false;
                            await SetStick(RIGHT, (short)(movementslist.ElementAt(i)[0]), (short)(movementslist.ElementAt(i)[1]), 0_001, token).ConfigureAwait(false);
                            i++;
                            if (i == movementslist.Count)
                                i = 0;
                            stopwatch.Restart();
                        }
                    }

                    //Check if inside an unwanted encounter
                    if (await IsInCatchScreen(token).ConfigureAwait(false))
                    {
                        stopwatch.Stop();
                        Log($"Unwanted encounter detected!");
                        await FleeToOverworld(token).ConfigureAwait(false);
                        //The encounter changes the LastSpawn value.
                        await WipeLastSpawn(token).ConfigureAwait(false);
                        encounterCount--;
                        Settings.RemoveCompletedScans();
                        stopwatch.Start();
                    }

                    //Check new spawns
                    var spawn = await ReadLastSpawn(token).ConfigureAwait(false);
                    if (spawn != 0)
                    {
                        var flag = await ReadSpawnFlags(token).ConfigureAwait(false);
                        await WipeLastSpawn(token).ConfigureAwait(false);
                        (var shiny, var gender) = HandleFlags(spawn, flag);

                        if (HandleOverworldEncounter(spawn, shiny, gender))
                        {
                            await ResetStick(token).ConfigureAwait(false);
                            await Click(X, 0_500, token).ConfigureAwait(false);
                            await Click(HOME, 0_500, token).ConfigureAwait(false);
                            return;
                        }
                    }
                }
            }
        }

        private bool HandleOverworldEncounter(int species, bool shiny, Gender gender)
        {
            if (species == 0)
                return false;

            encounterCount++;
            Settings.AddCompletedScans();
            if (shiny)
                Settings.AddCompletedShiny();

            Log($"New spawn ({encounterCount}): {(shiny ? "Shiny" : "")} {(gender is not Gender.Genderless ? gender.ToString() : "")} {SpeciesName.GetSpeciesName(species, 2)}");

            if (Settings.Routine is LGPEOverworldMode.WildBirds)
                if (!(species >= 144 && species <= 146))
                    return false;

            if (Settings.Routine is LGPEOverworldMode.OverworldSpawn)
                if (Settings.StopOnSpecies != 0 && (int)Settings.StopOnSpecies != species)
                    return false;

            if (Settings.OnlyShiny && !shiny)
                return false;

            var msg = $"Result found!{Environment.NewLine}Stopping routine execution; restart the bot to search again.";
            if (!string.IsNullOrWhiteSpace(Hub.Config.StopConditions.MatchFoundEchoMention))
                msg = $"{Hub.Config.StopConditions.MatchFoundEchoMention} {msg}";
            Log(msg);

            return true;
        }

        private async Task ReadAndEditCatchCombo(CancellationToken token)
        {
            if ((int)Settings.ChainSpecies > 0)
            {
                var speciescombo = await ReadSpeciesCombo(token).ConfigureAwait(false);
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
                var catchcombo = await ReadComboCount(token).ConfigureAwait(false);
                if (catchcombo < (uint)Settings.ChainCount)
                {
                    Log($"Current catch combo being {catchcombo}, incrementing to {Settings.ChainCount}.");
                    await EditComboCount((uint)Settings.ChainCount, token).ConfigureAwait(false);
                    catchcombo = await ReadComboCount(token).ConfigureAwait(false);
                    Log($"Current catch combo being now {catchcombo}.");
                }
            }
        }

        //Thanks Anubis!!!
        private (bool shiny, Gender gender) HandleFlags(int species, uint flags)
        {
            bool shiny = ((flags >> 1) & 1) == 1;

            var gender_ratio = PersonalTable.LG[species].Gender;
            Gender gender = gender_ratio switch
            {
                PersonalInfo.RatioMagicGenderless => Gender.Genderless,
                PersonalInfo.RatioMagicFemale => Gender.Female,
                PersonalInfo.RatioMagicMale => Gender.Male,
                _ => (flags & 1) == 0 ? Gender.Male : Gender.Female,
            };
            return (shiny, gender);
        }

        public override async Task HardStop()
        {
            await ResetStick(CancellationToken.None).ConfigureAwait(false);
            await CleanExit(Settings, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(RIGHT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }
    }
}
