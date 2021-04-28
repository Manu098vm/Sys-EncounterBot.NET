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
        //private readonly BotCompleteCounts Counts;
        //private readonly IDumper DumpSetting;
        //private readonly int[] DesiredIVs;
        //private readonly byte[] BattleMenuReady = { 0, 0, 0, 255 };

        public Letsgo(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            //Counts = Hub.Counts;
            //DumpSetting = Hub.Config.Folder;
            //DesiredIVs = StopConditionSettings.InitializeTargetIVs(Hub);
        }

        //private int encounterCount;

        public override async Task MainLoop(CancellationToken token)
        {
            //TODO: IdentifyTrainer routine for let's go instead of SwSh
            Log("Identifying trainer data of the host console.");
            await IdentifyTrainer(token).ConfigureAwait(false);

            Log("Starting main EncounterBot loop.");
            Config.IterateNextRoutine();

            // Clear out any residual stick weirdness.
            await ResetStick(token).ConfigureAwait(false);

            var task = Alternate(token);
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
            int i = 0;

            //Check if a shiny is generated and freeze the game if so.
            //This is basically the Zaksabeast cheat code ported for the newest Let's GO Eevee version. 
            byte[] inject = new byte[] { 0xE9, 0x03, 0x00, 0x2A, 0x60, 0x12, 0x40, 0xB9, 0xE1, 0x03, 0x09, 0x2A, 0x69, 0x06, 0x00, 0xF9, 0xDC, 0xFD, 0xFF, 0x97, 0x40, 0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(inject, 0x739930, token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                //Try controller
                await Click(A, 0_200, token).ConfigureAwait(false);
                Log("Test A");

                //Catch combo to increment spawn quality (Thanks to Lincoln-LM for the offset)
                catchcombo = BitConverter.ToUInt16(await Connection.ReadBytesAsync(0x5E1CF500, 2, token).ConfigureAwait(false), 0);
                if (catchcombo < 41)
                {
                    Log($"Current catch combo being {catchcombo}, incrementing to 41.");
                    await Connection.WriteBytesAsync(BitConverter.GetBytes(41), 0x5E1CF500, token).ConfigureAwait(false);
                    catchcombo = BitConverter.ToUInt16(await Connection.ReadBytesAsync(0x5E1CF500, 2, token).ConfigureAwait(false), 0);
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

        private async Task Alternate(CancellationToken token)
        {
            Log("Let's Go overworld Bot, proof of concept!");
            uint prev = 0;
            uint nuovo;
            uint catchcombo;
            uint speciescombo; ;
            int i = 0;

            //Check if a shiny is generated and freeze the game if so.
            //This is basically the Zaksabeast cheat code ported for the newest Let's GO Eevee version. 
            byte[] inject = new byte[] { 0xE9, 0x03, 0x00, 0x2A, 0x60, 0x12, 0x40, 0xB9, 0xE1, 0x03, 0x09, 0x2A, 0x69, 0x06, 0x00, 0xF9, 0xDC, 0xFD, 0xFF, 0x97, 0x40, 0x00, 0x00, 0x36, 0x00, 0x00, 0x00, 0x14 };
            await SwitchConnection.WriteBytesMainAsync(inject, GeneratingFunction1, token).ConfigureAwait(false);

            while (!token.IsCancellationRequested)
            {
                //Try controller
                await Click(A, 0_200, token).ConfigureAwait(false);
                Log("Test A");

                //Catch combo to increment spawn quality (Thanks to Lincoln-LM for the offset)
                speciescombo = BitConverter.ToUInt16(await Connection.ReadBytesAsync(SpeciesCombo, 2, token).ConfigureAwait(false), 0);
                if((int)speciescombo != (int)Hub.Config.StopConditions.StopOnSpecies && Hub.Config.StopConditions.StopOnSpecies != 0)
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

        private async Task ResetStick(CancellationToken token)
        {
            // If aborting the sequence, we might have the stick set at some position. Clear it just in case.
            await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false); // reset
        }
    }
}
