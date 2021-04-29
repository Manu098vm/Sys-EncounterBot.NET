using PKHeX.Core;
using System;
using System.Collections.Generic;
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
        private readonly byte[] BattleMenuReady = { 0, 0, 0, 255 };

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
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("Starting main EncounterBot loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);

            var task = Overworld(token);
            await task.ConfigureAwait(false);

            await ResetStick(token).ConfigureAwait(false);
            await DetachController(token).ConfigureAwait(false);
        }

        private async Task Unfreeze(CancellationToken token)
        {
            byte[] data = new byte[] { 0x0C, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(data, 0x739948, token).ConfigureAwait(false);
        }

        private async Task Overworld(CancellationToken token)
        {
            Log("Let's Go overworld Bot, proof of concept!");
            uint prev = 0;
            uint nuovo;
            uint catchcombo;
            uint speciescombo; ;
            int i = 0;

            //Check if a shiny is generated and freeze the game if so.
            await LGZaksabeast(token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                //Catch combo to increment spawn quality (Thanks to Lincoln-LM for the offset)
                speciescombo = BitConverter.ToUInt16(await Connection.ReadBytesAsync(SpeciesCombo, 2, token).ConfigureAwait(false), 0);
                if ((int)speciescombo != (int)Hub.Config.StopConditions.StopOnSpecies && Hub.Config.StopConditions.StopOnSpecies != 0)
                {
                    Log($"Current catch combo being on {speciescombo} {SpeciesName.GetSpeciesName((int)speciescombo, 4)}, changing to {Hub.Config.StopConditions.StopOnSpecies}.");
                    await Connection.WriteBytesAsync(BitConverter.GetBytes((uint)Hub.Config.StopConditions.StopOnSpecies), SpeciesCombo, token).ConfigureAwait(false);
                    speciescombo = BitConverter.ToUInt16(await Connection.ReadBytesAsync(SpeciesCombo, 2, token).ConfigureAwait(false), 0);
                    Log($"Catch combo changed on {speciescombo} {SpeciesName.GetSpeciesName((int)speciescombo, 4)}.");
                }
                catchcombo = BitConverter.ToUInt16(await Connection.ReadBytesAsync(CatchCombo, 2, token).ConfigureAwait(false), 0);
                if (catchcombo < 41)
                {
                    Log($"Current catch combo being {catchcombo}, incrementing to 41.");
                    await Connection.WriteBytesAsync(BitConverter.GetBytes(41), CatchCombo, token).ConfigureAwait(false);
                    catchcombo = BitConverter.ToUInt16(await Connection.ReadBytesAsync(catchcombo, 2, token).ConfigureAwait(false), 0);
                    Log($"Catch combo restored to {catchcombo}.");
                }
                //Check new spawns
                nuovo = BitConverter.ToUInt16(await Connection.ReadBytesAsync(0x5E12C120, 2, token).ConfigureAwait(false), 0);
                if (nuovo != prev)
                {
                    if (nuovo != 0)
                    {
                        i++;
                        Log($"New spawn ({i}): {nuovo} {SpeciesName.GetSpeciesName((int)nuovo, 4)}");
                    }
                    prev = nuovo;
                }

                //TODO
                //check if freezed (?) -> log shiny has been found
                //if (shiny && nuovo match species stop condition) prompt user to unfreeze
                //else unfreeze and continue looping
            }
        }

        private async Task Trade(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                Log("Inside trade function");
                //Click through all the menus until the trade.
                /*while (!await LGIsInTrade(token).ConfigureAwait(false))
                {
                    await Click(LSTICK, 1_000, token).ConfigureAwait(false); //LSTICK being A with Ball Plus
                    Log("Click A");
                }*/

                Log("A trade has started! Checking details...");

                var pk = await LGReadUntilPresent(TradeData, 2_000, 0_200, token, EncryptedSize, false).ConfigureAwait(false);
                if (pk != null)
                {
                    if (await HandleEncounter(pk.ConvertToPK8(), false, token).ConfigureAwait(false))
                        return;
                }

                Log($"Resetting the trade by restarting the game");
                await CloseGame(Hub.Config, token).ConfigureAwait(false);
                for (int i = 0; i < 30; i++)
                    await Click(LSTICK, 1_000, token).ConfigureAwait(false);
            }
        }

        private async Task Test(CancellationToken token)
        {
            
            while (!token.IsCancellationRequested)
            {
                await LGZaksabeast(token).ConfigureAwait(false);
                await LGForceShiny(token).ConfigureAwait(false);
            }
            await LGNormalShiny(token).ConfigureAwait(false);
            return;
        }

        private async Task<bool> HandleEncounter(PK8 pk, bool legends, CancellationToken token)
        {
            encounterCount++;

            //Star/Square Shiny Recognition
            var showdowntext = ShowdownParsing.GetShowdownText(pk);
            if (pk.IsShiny && pk.ShinyXor == 0)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
            else if (pk.IsShiny)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");

            Log($"Encounter: {encounterCount}{Environment.NewLine}{Environment.NewLine}{showdowntext}{Environment.NewLine}{Environment.NewLine}");
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
