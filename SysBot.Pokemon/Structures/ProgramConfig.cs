using SysBot.Base;

namespace SysBot.Pokemon
{
    public class ProgramConfig : BotList<PokeBotState>
    {
        public ProgramMode Mode { get; set; } = ProgramMode.Switch;
        public PokeBotHubConfig Hub { get; set; } = new();
    }

    public enum ProgramMode
    {
        None, // invalid
        Switch,
        _3DS,
    }
}
