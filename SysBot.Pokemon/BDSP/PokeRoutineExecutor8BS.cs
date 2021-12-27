using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;
using SysBot.Base;
using static SysBot.Pokemon.BasePokeDataOffsetsBS;
using static SysBot.Base.SwitchButton;

namespace SysBot.Pokemon
{
    public abstract class PokeRoutineExecutor8BS : PokeRoutineExecutor<PB8>
    {
        protected IPokeDataOffsetsBS Offsets { get; private set; } = new PokeDataOffsetsBS_BD();
        protected PokeRoutineExecutor8BS(PokeBotState cfg) : base(cfg)
        {
        }

        public override async Task<PB8> ReadPokemon(ulong offset, CancellationToken token) => await ReadPokemon(offset, BoxFormatSlotSize, token).ConfigureAwait(false);

        public override async Task<PB8> ReadPokemon(ulong offset, int size, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, size, token).ConfigureAwait(false);
            return new PB8(data);
        }

        public override async Task<PB8> ReadPokemonPointer(IEnumerable<long> jumps, int size, CancellationToken token)
        {
            var (valid, offset) = await ValidatePointerAll(jumps, token).ConfigureAwait(false);
            if (!valid)
                return new PB8();
            return await ReadPokemon(offset, token).ConfigureAwait(false);
        }

        public async Task<bool> ReadIsChanged(uint offset, byte[] original, CancellationToken token)
        {
            var result = await Connection.ReadBytesAsync(offset, original.Length, token).ConfigureAwait(false);
            return !result.SequenceEqual(original);
        }

        public override async Task<PB8> ReadBoxPokemon(int box, int slot, CancellationToken token)
        {
            // Shouldn't be reading anything but box1slot1 here. Slots are not consecutive.
            var jumps = Offsets.BoxStartPokemonPointer.ToArray();
            return await ReadPokemonPointer(jumps, BoxFormatSlotSize, token).ConfigureAwait(false);
        }

        public async Task SetBoxPokemonAbsolute(ulong offset, PB8 pkm, CancellationToken token, ITrainerInfo? sav = null)
        {
            if (sav != null)
            {
                // Update PKM to the current save's handler data
                DateTime Date = DateTime.Now;
                pkm.Trade(sav, Date.Day, Date.Month, Date.Year);
                pkm.RefreshChecksum();
            }

            pkm.ResetPartyStats();
            await SwitchConnection.WriteBytesAbsoluteAsync(pkm.EncryptedPartyData, offset, token).ConfigureAwait(false);
        }

        public async Task<SAV8BS> IdentifyTrainer(CancellationToken token)
        {
            // pull title so we know which set of offsets to use
            string title = await SwitchConnection.GetTitleID(token).ConfigureAwait(false);
            Offsets = title switch
            {
                BrilliantDiamondID => new PokeDataOffsetsBS_BD(),
                ShiningPearlID => new PokeDataOffsetsBS_SP(),
                _ => throw new Exception($"{title} is not a valid Pokémon BDSP title. Is your mode correct?"),
            };

            var sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
            InitSaveData(sav);

            if (await GetTextSpeed(token).ConfigureAwait(false) != TextSpeedOption.Fast)
                Log("Text speed should be set to FAST. Stop the bot and fix this if you encounter problems.");

            return sav;
        }

        public async Task<SAV8BS> GetFakeTrainerSAV(CancellationToken token)
        {
            var sav = new SAV8BS();
            var info = sav.MyStatus;

            // Set the OT.
            var name = await SwitchConnection.PointerPeek(0x2E, Offsets.MyStatusTrainerPointer, token).ConfigureAwait(false);
            info.OT = ReadStringFromRAMObject(name);

            // Set the TID, SID, and Language
            var id = await SwitchConnection.PointerPeek(4, Offsets.MyStatusTIDPointer, token).ConfigureAwait(false);
            info.TID = BitConverter.ToUInt16(id, 0);
            info.SID = BitConverter.ToUInt16(id, 2);

            var lang = await SwitchConnection.PointerPeek(1, Offsets.ConfigLanguagePointer, token).ConfigureAwait(false);
            sav.Language = lang[0];
            return sav;
        }

        public static string ReadStringFromRAMObject(byte[] obj)
        {
            // 0x10 typeinfo/monitor, 0x4 len, char[len]
            const int ofs_len = 0x10;
            const int ofs_chars = 0x14;
            Debug.Assert(obj.Length >= ofs_chars);

            // Detect string length, but be cautious about its correctness (protect against bad data)
            int maxCharCount = (obj.Length - ofs_chars) / 2;
            int length = BitConverter.ToInt32(obj, ofs_len);
            if (length < 0 || length > maxCharCount)
                length = maxCharCount;

            return StringConverter.GetString7b(obj, ofs_chars, length * 2);
        }

        public async Task InitializeHardware(IBotStateSettings settings, CancellationToken token)
        {
            Log("Detaching on startup.");
            await DetachController(token).ConfigureAwait(false);
            if (settings.ScreenOff)
            {
                Log("Turning off screen.");
                await SetScreen(ScreenState.Off, token).ConfigureAwait(false);
            }

            Log("Setting BDSP-specific hid waits.");
            await Connection.SendAsync(SwitchCommand.Configure(SwitchConfigureParameter.keySleepTime, 50), token).ConfigureAwait(false);
            await Connection.SendAsync(SwitchCommand.Configure(SwitchConfigureParameter.pollRate, 50), token).ConfigureAwait(false);
        }

        public async Task CleanExit(IBotStateSettings settings, CancellationToken token)
        {
            if (settings.ScreenOff)
            {
                Log("Turning on screen.");
                await SetScreen(ScreenState.On, token).ConfigureAwait(false);
            }
            Log("Detaching controllers on routine exit.");
            await DetachController(token).ConfigureAwait(false);
        }

        protected virtual async Task EnterLinkCode(int code, PokeBotHubConfig config, CancellationToken token)
        {
            // Default implementation to just press directional arrows. Can do via Hid keys, but users are slower than bots at even the default code entry.
            var keys = TradeUtil.GetPresses(code);
            foreach (var key in keys)
            {
                int delay = config.Timings.KeypressTime;
                await Click(key, delay, token).ConfigureAwait(false);
            }
            // Confirm Code outside of this method (allow synchronization)
        }

        public async Task ReOpenGame(bool untiloverworld, PokeBotHubConfig config, CancellationToken token)
        {
            await CloseGame(config, token).ConfigureAwait(false);
            await StartGame(untiloverworld, config, token).ConfigureAwait(false);
        }

        public async Task CloseGame(PokeBotHubConfig config, CancellationToken token)
        {
            var timing = config.Timings;
            // Close out of the game
            await Click(SwitchButton.HOME, 2_000 + timing.ExtraTimeReturnHome, token).ConfigureAwait(false);
            await Click(SwitchButton.X, 1_000, token).ConfigureAwait(false);
            await Click(SwitchButton.A, 5_000 + timing.ExtraTimeCloseGame, token).ConfigureAwait(false);
            Log("Closed out of the game!");
        }

        public async Task StartGame(bool untiloverworld, PokeBotHubConfig config, CancellationToken token)
        {
            var timing = config.Timings;
            // Open game.
            await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);

            // Menus here can go in the order: Update Prompt -> Profile -> DLC check -> Unable to use DLC.
            //  The user can optionally turn on the setting if they know of a breaking system update incoming.
            if (timing.AvoidSystemUpdate)
            {
                await Click(DUP, 0_600, token).ConfigureAwait(false);
                await Click(A, 1_000 + timing.ExtraTimeLoadProfile, token).ConfigureAwait(false);
            }

            await Click(A, 1_000 + timing.ExtraTimeCheckDLC, token).ConfigureAwait(false);
            // If they have DLC on the system and can't use it, requires an UP + A to start the game.
            // Should be harmless otherwise since they'll be in loading screen.
            await Click(DUP, 0_600, token).ConfigureAwait(false);
            await Click(A, 0_600, token).ConfigureAwait(false);

            Log("Restarting the game!");

            if (untiloverworld)
            {
                // Switch Logo lag, skip cutscene, game load screen
                await Task.Delay(22_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

                for (int i = 0; i < 10; i++)
                    await Click(A, 1_000, token).ConfigureAwait(false);

                var timer = 60_000;
                while (!await IsSceneID(SceneID_Field, token).ConfigureAwait(false))
                {
                    await Task.Delay(1_000, token).ConfigureAwait(false);
                    timer -= 1_000;
                    // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
                    // Don't risk it if hub is set to avoid updates.
                    if (timer <= 0 && !timing.AvoidSystemUpdate)
                    {
                        Log("Still not in the game, initiating rescue protocol!");
                        while (!await IsSceneID(SceneID_Field, token).ConfigureAwait(false))
                            await Click(A, 6_000, token).ConfigureAwait(false);
                        break;
                    }
                }

                await Task.Delay(5_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
                Log("Back in the overworld!");
            }
            else
                await Task.Delay(5_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);
        }

        public async Task ResumeStart(PokeBotHubConfig config, CancellationToken token)
		{
            var timing = config.Timings;

            // Switch Logo lag, skip cutscene, game load screen
            await Task.Delay(17_000 + timing.ExtraTimeLoadGame, token).ConfigureAwait(false);

            for (int i = 0; i < 10; i++)
                await Click(A, 1_000, token).ConfigureAwait(false);

            var timer = 60_000;
            while (!await IsSceneID(SceneID_Field, token).ConfigureAwait(false))
            {
                await Task.Delay(1_000, token).ConfigureAwait(false);
                timer -= 1_000;
                // We haven't made it back to overworld after a minute, so press A every 6 seconds hoping to restart the game.
                // Don't risk it if hub is set to avoid updates.
                if (timer <= 0 && !timing.AvoidSystemUpdate)
                {
                    Log("Still not in the game, initiating rescue protocol!");
                    while (!await IsSceneID(SceneID_Field, token).ConfigureAwait(false))
                        await Click(A, 6_000, token).ConfigureAwait(false);
                    break;
                }
            }

            await Task.Delay(5_000 + timing.ExtraTimeLoadOverworld, token).ConfigureAwait(false);
            Log("Back in the overworld!");
        }

        public async Task<uint> GetSceneID(CancellationToken token)
        {
            var xVal = await SwitchConnection.PointerPeek(1, Offsets.SceneIDPointer, token).ConfigureAwait(false);
            var xParsed = BitConverter.ToUInt32(xVal, 0);
            return xParsed;
        }

        public async Task<bool> IsSceneID(uint expected, CancellationToken token)
        {
            var byt = await SwitchConnection.PointerPeek(1, Offsets.SceneIDPointer, token).ConfigureAwait(false);
            return byt[0] == expected;
        }

        // Uses absolute offset which is set each session. Checks for IsGaming or IsTalking.
        public async Task<bool> IsUnionWork(ulong offset, CancellationToken token)
        {
            var data = await SwitchConnection.ReadBytesAbsoluteAsync(offset, 1, token).ConfigureAwait(false);
            return data[0] == 1;
        }

        public async Task<TextSpeedOption> GetTextSpeed(CancellationToken token)
        {
            var data = await SwitchConnection.PointerPeek(1, Offsets.ConfigTextSpeedPointer, token).ConfigureAwait(false);
            return (TextSpeedOption)data[0];
        }

        public async Task OpenDex(CancellationToken token)
		{
            Log("Opening Pokedex...");
            await Click(SwitchButton.X, 0_900, token).ConfigureAwait(false);
            await Click(SwitchButton.PLUS, 1_350, token).ConfigureAwait(false);
            await Click(SwitchButton.B, 1_000, token).ConfigureAwait(false);
            await Click(SwitchButton.DUP, 0_150, token).ConfigureAwait(false);
            await Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
            await Click(SwitchButton.R, 0_900, token).ConfigureAwait(false);
        }

        public async Task CloseDex(CancellationToken token)
		{
            for(int i = 0; i < 5; i++)
                await Click(SwitchButton.B, 0_350, token).ConfigureAwait(false);
		}

        public List<int> GetEncounterSlots(GameVersion version, int location, GameTime time, WildMode mode)
        {
            string res_data = version is GameVersion.BD ? Properties.Resources.FieldEncountTable_d : Properties.Resources.FieldEncountTable_p;
            EncounterTable? ectable = JsonConvert.DeserializeObject<EncounterTable>(res_data);
            Table current_table = new();
            var list = new List<int>();
            int i = 2;

            if (ectable != null)
            {
                if (ectable.table != null)
                {
                    foreach (var table in ectable.table)
                    {
                        if (table.zoneID == location)
                            current_table = table;
                    }
                    if (current_table != null)
                    {

                        if ((mode is WildMode.Grass || mode is WildMode.Swarm) && current_table.ground_mons != null)
                        {
                            foreach (var specie in current_table.ground_mons)
                                list.Add(specie.monsNo);
                            if ((time is GameTime.Night or GameTime.DeepNight) && current_table.night != null)
                                foreach (var specie in current_table.night)
                                {
                                    list[i] = specie.monsNo;
                                    i++;
                                }
                            else if ((time is GameTime.Day or GameTime.Sunset) && current_table.day != null)
                                foreach (var specie in current_table.day)
                                {
                                    list[i] = specie.monsNo;
                                    i++;
                                }
                            if (mode is WildMode.Swarm && current_table.tairyo != null)
                            {
                                i = 0;
                                foreach (var specie in current_table.tairyo)
                                {
                                    list[i] = specie.monsNo;
                                    i++;
                                }
                            }
                        }
                        else if (mode is WildMode.Surf && current_table.water_mons != null)
                            foreach (var specie in current_table.water_mons)
                                list.Add(specie.monsNo);
                        else if (mode is WildMode.OldRod && current_table.boro_mons != null)
                            foreach (var specie in current_table.boro_mons)
                                list.Add(specie.monsNo);
                        else if (mode is WildMode.GoodRod && current_table.ii_mons != null)
                            foreach (var specie in current_table.ii_mons)
                                list.Add(specie.monsNo);
                        else if (mode is WildMode.SuperRod && current_table.sugoi_mons != null)
                            foreach (var specie in current_table.sugoi_mons)
                                list.Add(specie.monsNo);
                    }
                }
            }
            return list;
        }

        public List<SwitchButton> ParseActions(string config_actions)
        {
            var action = new List<SwitchButton>();
            var actions = $"{config_actions.ToUpper()},";
            var word = "";
            var index = 0;

            while (index < actions.Length - 1)
            {
                if ((actions.Length > 1 && (actions[index + 1] == ',' || actions[index + 1] == '.')) || actions.Length == 1)
                {
                    word += actions[index];
                    if (Enum.IsDefined(typeof(SwitchButton), word))
                        action.Add((SwitchButton)Enum.Parse(typeof(SwitchButton), word));
                    actions.Remove(0, 1);
                    word = "";
                }
                else if (actions[index] == ',' || actions[index] == '.' || actions[index] == ' ' || actions[index] == '\n' || actions[index] == '\t' || actions[index] == '\0')
                    actions.Remove(0, 1);
                else
                {
                    word += actions[index];
                    actions.Remove(0, 1);
                }
                index++;
            }

            return action;
        }

        public string GetString(PB8 pk)
        {

            return $"\nEC: {pk.EncryptionConstant:X}\nPID: {pk.PID:X} {GetShinyType(pk)}\n" +
                $"{(Nature)pk.Nature} nature\nAbility slot: {pk.AbilityNumber}\n" +
                $"IVs: [{pk.IV_HP}, {pk.IV_ATK}, {pk.IV_DEF}, {pk.IV_SPA}, {pk.IV_SPD}, {pk.IV_SPE}]\n";
        }

        public string GetShinyType(PB8 pk)
        {
            if (pk.IsShiny)
            {
                if (pk.ShinyXor == 0)
                    return "(Square)";
                return "(Star)";
            }
            return "";
        }

        public async Task DoActions(List<SwitchButton> actions, int timings, CancellationToken token)
        {
            for(var i = 0; i < actions.Count - 1; i++)
			{
                Log($"Press {actions[i]}.");
                await Click(actions[i], timings, token).ConfigureAwait(false);
            }
        }
    }
}