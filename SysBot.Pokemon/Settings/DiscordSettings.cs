using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class DiscordSettings
    {
        private const string Startup = nameof(Startup);
        private const string Operation = nameof(Operation);
        private const string Whitelists = nameof(Whitelists);
        private const string DefaultDisable = "DISABLE";
        public override string ToString() => "Discord Integration Settings";

        // Startup

        [Category(Startup), Description("Bot login token.")]
        public string Token { get; set; } = string.Empty;

        [Category(Startup), Description("Bot command prefix.")]
        public string CommandPrefix { get; set; } = "/";

        [Category(Startup), Description("Toggle to handle commands asynchronously or synchronously.")]
        public bool AsyncCommands { get; set; }

        [Category(Startup), Description("Custom Status for playing a game.")]
        public string BotGameStatus { get; set; } = "Sys-EncounterBot";

        [Category(Operation), Description("Custom message the bot will reply with when a user says hello to it. Use string formatting to mention the user in the reply.")]
        public string HelloResponse { get; set; } = "Ciao {0}!";

        [Category(Operation), Description("User with this ID will receive a ping if an Encounter Bot will match a Stop Condition.")]
        public string UserTag { get; set; } = string.Empty;

        // Whitelists

        [Category(Whitelists), Description("Users with this role are allowed to bypass command restrictions.")]
        public string RoleSudo { get; set; } = DefaultDisable;

        // Operation

        [Category(Operation), Description("Comma separated Discord user IDs that will have sudo access to the Bot Hub.")]
        public string GlobalSudoList { get; set; } = string.Empty;

        [Category(Operation), Description("Disabling this will remove global sudo support.")]
        public bool AllowGlobalSudo { get; set; } = true;

        [Category(Operation), Description("Comma separated channel IDs that will echo the log bot data.")]
        public string LoggingChannels { get; set; } = string.Empty;
    }
}