using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using static SysBot.Base.SwitchButton;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public sealed class EncounterBotEgg : EncounterBot
    {
        public EncounterBotEgg(PokeBotState cfg, PokeBotHub<PK8> hub) : base(cfg, hub)
        {
        }

        private const SwordShieldDaycare Location = SwordShieldDaycare.Route5;
        private const int InjectBox = 0;
        private const int InjectSlot = 0;
        private static readonly PK8 Blank = new();

        protected override async Task EncounterLoop(SAV8SWSH sav, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // Walk a step left, then right => check if egg was generated on this attempt.
                // Repeat until an egg is generated.

                var attempts = await StepUntilEgg(token).ConfigureAwait(false);
                if (attempts < 0) // aborted
                    return;

                Log($"Egg available after {attempts} attempts! Clearing destination slot.");
                await SetBoxPokemon(Blank, InjectBox, InjectSlot, token).ConfigureAwait(false);

                for (int i = 0; i < 6; i++)
                    await Click(A, 0_400, token).ConfigureAwait(false);

                // Safe to mash B from here until we get out of all menus.
                while (!await IsOnOverworld(Hub.Config, token).ConfigureAwait(false))
                    await Click(B, 0_400, token).ConfigureAwait(false);

                Log("Egg received. Checking details.");
                var pk = await ReadBoxPokemon(InjectBox, InjectSlot, token).ConfigureAwait(false);
                if (await HandleEncounter(pk, token).ConfigureAwait(false))
                    return;
            }
        }

        private async Task<int> StepUntilEgg(CancellationToken token)
        {
            Log("Walking around until an egg is ready...");
            int attempts = 0;
            while (!token.IsCancellationRequested)
            {
                await SetEggStepCounter(Location, token).ConfigureAwait(false);

                // Walk Diagonally Left
                await SetStick(LEFT, -19000, 19000, 0_500, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

                // Walk Diagonally Right, slightly longer to ensure we stay at the Daycare lady.
                await SetStick(LEFT, 19000, 19000, 0_550, token).ConfigureAwait(false);
                await SetStick(LEFT, 0, 0, 500, token).ConfigureAwait(false); // reset

                bool eggReady = await IsEggReady(Location, token).ConfigureAwait(false);
                if (eggReady)
                    return attempts;

                attempts++;
                if (attempts % 10 == 0)
                    Log($"Tried {attempts} times, still no egg.");

                if (attempts > 10)
                    await Click(B, 500, token).ConfigureAwait(false);
            }

            return -1; // aborted
        }

        public async Task<bool> IsEggReady(SwordShieldDaycare daycare, CancellationToken token)
        {
            var ofs = PokeDataOffsets.GetDaycareEggIsReadyOffset(daycare);
            // Read a single byte of the Daycare metadata to check the IsEggReady flag.
            var data = await Connection.ReadBytesAsync(ofs, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        public async Task SetEggStepCounter(SwordShieldDaycare daycare, CancellationToken token)
        {
            // Set the step counter in the Daycare metadata to 180. This is the threshold that triggers the "Should I create a new egg" subroutine.
            // When the game executes the subroutine, it will generate a new seed and set the IsEggReady flag.
            // Just setting the IsEggReady flag won't refresh the seed; we want a different egg every time.
            var data = new byte[] { 0xB4, 0, 0, 0 }; // 180
            var ofs = PokeDataOffsets.GetDaycareStepCounterOffset(daycare);
            await Connection.WriteBytesAsync(data, ofs, token).ConfigureAwait(false);
        }

        private async Task SetupBoxState(CancellationToken token)
        {
            await SetCurrentBox(0, token).ConfigureAwait(false);

            var existing = await ReadBoxPokemon(InjectBox, InjectSlot, token).ConfigureAwait(false);
            if (existing.Species != 0 && existing.ChecksumValid)
            {
                Log("Destination slot is occupied! Dumping the Pokémon found there...");
                DumpPokemon(DumpSetting.DumpFolder, "saved", existing);
            }

            Log("Clearing destination slot to start the bot.");
            await SetBoxPokemon(Blank, InjectBox, InjectSlot, token).ConfigureAwait(false);
        }
    }
}
