using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class DiscordSettings
    {
        private const string Startup = nameof(Startup);
        private const string Operation = nameof(Operation);
        private const string Channels = nameof(Channels);
        private const string Roles = nameof(Roles);
        private const string Users = nameof(Users);
        public override string ToString() => "Discord Integration Settings";

        // Startup

        [Category(Startup), Description("Bot login token.")]
        public string Token { get; set; } = string.Empty;

        [Category(Startup), Description("Bot command prefix.")]
        public string CommandPrefix { get; set; } = "$";

        [Category(Startup), Description("List of modules that will not be loaded when the bot is started (comma separated).")]
        public string ModuleBlacklist { get; set; } = string.Empty;

        [Category(Startup), Description("Toggle to handle commands asynchronously or synchronously.")]
        public bool AsyncCommands { get; set; }

        [Category(Startup), Description("Custom Status for playing a game.")]
        public string BotGameStatus { get; set; } = "Sys-EncounterBot";

        [Category(Operation), Description("Custom message the bot will reply with when a user says hello to it. Use string formatting to mention the user in the reply.")]
        public string HelloResponse { get; set; } = "Ciao {0}!";

        // Whitelists

        [Category(Roles), Description("Users with this role are allowed to remotely control the console (if running as Remote Control Bot.")]
        public RemoteControlAccessList RoleRemoteControl { get; set; } = new() { AllowIfEmpty = false };

        [Category(Roles), Description("Users with this role are allowed to bypass command restrictions.")]
        public RemoteControlAccessList RoleSudo { get; set; } = new() { AllowIfEmpty = false };

        // Operation

        [Category(Channels), Description("Channels with these IDs are the only channels where the bot acknowledges commands.")]
        public RemoteControlAccessList ChannelWhitelist { get; set; } = new();

        [Category(Users), Description("Comma separated Discord user IDs that will have sudo access to the Bot Hub.")]
        public RemoteControlAccessList GlobalSudoList { get; set; } = new();

        [Category(Users), Description("Disabling this will remove global sudo support.")]
        public bool AllowGlobalSudo { get; set; } = true;

        [Category(Channels), Description("Channel IDs that will echo the log bot data.")]
        public RemoteControlAccessList LoggingChannels { get; set; } = new();

        [Category(Channels), Description("Echo channels that will log special messages.")]
        public RemoteControlAccessList EchoChannels { get; set; } = new();

        [Category(Operation), Description("Replies to users if they are not allowed to use a given command in the channel. When false, the bot will silently ignore them instead.")]
        public bool ReplyCannotUseCommandInChannel { get; set; } = true;
    }
}