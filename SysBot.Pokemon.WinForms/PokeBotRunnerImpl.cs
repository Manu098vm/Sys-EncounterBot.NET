using PKHeX.Core;
using SysBot.Pokemon.Discord;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Bot Environment implementation with Integrations added.
    /// </summary>
    public class PokeBotRunnerImpl : PokeBotRunner
    {
        public PokeBotRunnerImpl(PokeTradeHub<PK8> hub) : base(hub) { }
        public PokeBotRunnerImpl(PokeTradeHubConfig config) : base(config) { }
        protected override void AddIntegrations()
        {
            if (!string.IsNullOrWhiteSpace(Hub.Config.Discord.Token))
                AddDiscordBot(Hub.Config.Discord.Token);
        }

        private void AddDiscordBot(string apiToken)
        {
            SysCordInstance.Runner = this;
            var bot = new SysCord(Hub);
            Task.Run(() => bot.MainAsync(apiToken, CancellationToken.None));
        }
    }
}