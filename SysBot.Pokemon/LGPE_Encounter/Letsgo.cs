using PKHeX.Core;
using System;
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
        private readonly int[] DesiredMinIVs;
        private readonly int[] DesiredMaxIVs;

        public Letsgo(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            Counts = Hub.Counts;
            DumpSetting = Hub.Config.Folder;
            StopConditionSettings.InitializeTargetIVs(Hub, out DesiredMinIVs, out DesiredMaxIVs);
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

            var task = Hub.Config.LGPE_Encounter.EncounteringType switch
            {
                LetsGoMode.Trades => DoRestartingEncounter(token),
                LetsGoMode.Stationary => DoRestartingEncounter(token),
                LetsGoMode.Gifts => DoRestartingEncounter(token),
                LetsGoMode.LiveStatsChecking => DoLiveStatsChecking(token),
                _ => DoLiveStatsChecking(token),
            };
            await task.ConfigureAwait(false);

            await ResetStick(token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }

        private async Task DoLiveStatsChecking(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
               while (await LGIsInCatchScreen(token).ConfigureAwait(false) || await LGIsGiftFound(token).ConfigureAwait(false) || await LGIsInBattle(token).ConfigureAwait(false) || await LGIsInTrade(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                while (!await LGIsInCatchScreen(token).ConfigureAwait(false) && !await LGIsGiftFound(token).ConfigureAwait(false) && !await LGIsInBattle(token).ConfigureAwait(false) && !await LGIsInTrade(token).ConfigureAwait(false))
                    await Task.Delay(1_000, token).ConfigureAwait(false);

                if (!await LGIsInTitleScreen(token).ConfigureAwait(false))
                {
                    Log("Pokémon found! Checking details...");
                    var pk = await LGReadUntilPresent(PokeData, 2_000, 0_200, token, EncryptedSize, false).ConfigureAwait(false);
                    if (pk == null)
                        pk = await LGReadUntilPresent(StationaryBattleData, 2_000, 0_200, token, EncryptedSize, true).ConfigureAwait(false);

                    if (pk == null)
                        Log("Check error. Either a wrong offset is used, or the RAM is shifted.");
                    else
                        await HandleEncounter(pk, IsPKLegendary(pk.Species), token);
                }
            }
        }

        private async Task DoRestartingEncounter(CancellationToken token)
        {
            LetsGoMode mode = Hub.Config.LGPE_Encounter.EncounteringType;
            uint offset = mode == LetsGoMode.Stationary ? StationaryBattleData : PokeData;
            bool isheap = mode == LetsGoMode.Stationary;
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            long ms = 0;

            if (mode == LetsGoMode.Stationary)
                Log("Ensure to have a powerful Pokémon in the first slot of your team, with a move that can knock out the enemy in a few turns as first move.");

            while (!token.IsCancellationRequested)
            {

                //Force the Fortune Teller Nature value
                if (Hub.Config.LGPE_Encounter.SetFortuneTellerNature != Nature.Random)
                {
                    await Task.Delay(2_000, token).ConfigureAwait(false);
                    await LGEnableNatureTeller(token).ConfigureAwait(false);
                    await LGEditWildNature(Hub.Config.LGPE_Encounter.SetFortuneTellerNature, token).ConfigureAwait(false);
                    Log($"Nature Teller services Enabled, Nature set to {Hub.Config.LGPE_Encounter.SetFortuneTellerNature}.");
                }

                stopwatch.Restart();

                //Spam A until battle starts
                if (mode == LetsGoMode.Stationary)
                {
                    while(!await LGIsInBattle(token).ConfigureAwait(false) && !(ms != 0 && stopwatch.ElapsedMilliseconds > ms))
                        await Click(A, 0_200, token).ConfigureAwait(false);
                    Log("Battle started, checking details...");
                }
                else if (mode == LetsGoMode.Trades)
                {
                    while(!await LGIsInTrade(token).ConfigureAwait(false) && !(ms != 0 && stopwatch.ElapsedMilliseconds > ms))
                        await Click(A, 0_200, token).ConfigureAwait(false);
                    Log("Trade started, checking details...");
                }
                else if (mode == LetsGoMode.Gifts)
                {
                    while(!await LGIsGiftFound(token).ConfigureAwait(false) && !(ms != 0 && stopwatch.ElapsedMilliseconds > ms))
                        await Click(A, 0_200, token).ConfigureAwait(false);
                    Log("Gift found, checking details...");
                }

                //Ms taken from a single encounter + margin
                if (ms == 0)
                    ms = stopwatch.ElapsedMilliseconds + (long)2500;

                var pk = await LGReadUntilPresent(offset, 2_000, 0_200, token, EncryptedSize, isheap).ConfigureAwait(false);
                if (pk != null)
                {
                    if (await HandleEncounter(pk, IsPKLegendary(pk.Species), token).ConfigureAwait(false))
                    {
                        if (mode == LetsGoMode.Stationary)
                        {
                            Log("Result found, defeating the enemy.");
                            stopwatch.Restart();
                            //Spam A until the battle ends
                            while (!await LGIsInCatchScreen(token).ConfigureAwait(false))
                            {
                                if (stopwatch.ElapsedMilliseconds > 60000)
                                {
                                    Log("Enemy could not be defeated. Target has been lost. Use a pokemon with a more powerful move in the first slot next time.");
                                    return;
                                }
                                await Click(A, 0_500, token).ConfigureAwait(false);
                            }
                        }
                        await Click(HOME, 1_000, token).ConfigureAwait(false);
                        return;
                    }
                }

                Log($"Resetting the encounter by restarting the game...");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                await LGOpenGame(Hub.Config, token).ConfigureAwait(false);
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

            if (StopConditionSettings.EncounterFound(pk, DesiredMinIVs, DesiredMaxIVs, Hub.Config.StopConditions))
            {
                if (!String.IsNullOrEmpty(Hub.Config.Discord.UserTag))
                    Log($"<@{Hub.Config.Discord.UserTag}> result found! Stopping routine execution; restart the bot(s) to search again.");
                else
                    Log("Result found!");
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
