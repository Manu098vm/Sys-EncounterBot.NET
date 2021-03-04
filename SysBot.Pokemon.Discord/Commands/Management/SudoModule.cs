using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class SudoModule : ModuleBase<SocketCommandContext>
    {
        protected async Task Process(IEnumerable<ulong> values, Func<SensitiveSet<ulong>, ulong, bool> process, Func<DiscordManager, SensitiveSet<ulong>> fetch)
        {
            var mgr = SysCordInstance.Manager;
            var list = fetch(SysCordInstance.Manager);
            var any = false;
            foreach (var v in values)
                any |= process(list, v);

            if (!any)
            {
                await ReplyAsync("Failed.").ConfigureAwait(false);
                return;
            }

            mgr.Write();
            await ReplyAsync("Done.").ConfigureAwait(false);
        }
    }
}