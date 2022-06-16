using System;
using System.Collections.Generic;
using System.Linq;

namespace SysBot.Pokemon.Discord
{
    public class DiscordManager
    {
        public readonly DiscordSettings Config;
        public ulong Owner { get; internal set; }
        public RemoteControlAccessList WhitelistedChannels => Config.ChannelWhitelist;

        public RemoteControlAccessList SudoDiscord => Config.GlobalSudoList;
        public RemoteControlAccessList SudoRoles => Config.RoleSudo;

        public bool CanUseSudo(ulong uid) => SudoDiscord.Contains(uid);
        public bool CanUseSudo(IEnumerable<string> roles) => roles.Any(SudoRoles.Contains);

        public bool CanUseCommandChannel(ulong channel) => (WhitelistedChannels.List.Count == 0 && WhitelistedChannels.AllowIfEmpty) || WhitelistedChannels.Contains(channel);

        public DiscordManager(DiscordSettings cfg) => Config = cfg;

        public bool GetHasRoleAccess(string type, IEnumerable<string> roles)
        {
            var set = GetSet(type);
            return (set.AllowIfEmpty && set.List.Count == 0) || roles.Any(set.Contains);
        }

        private RemoteControlAccessList GetSet(string type) => throw new ArgumentOutOfRangeException(nameof(type));
    }
}