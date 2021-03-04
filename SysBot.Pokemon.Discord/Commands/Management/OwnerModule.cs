using Discord.Commands;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public class OwnerModule : SudoModule
    {
        [Command("addSudo")]
        [Summary("Adds mentioned user to global sudo")]
        [RequireOwner]
        // ReSharper disable once UnusedParameter.Global
        public async Task SudoUsers([Remainder] string _)
        {
            await Process(Context.Message.MentionedUsers.Select(z => z.Id), (z, x) => z.Add(x), z => z.SudoDiscord).ConfigureAwait(false);
        }

        [Command("removeSudo")]
        [Summary("Removes mentioned user to global sudo")]
        [RequireOwner]
        // ReSharper disable once UnusedParameter.Global
        public async Task RemoveSudoUsers([Remainder] string _)
        {
            await Process(Context.Message.MentionedUsers.Select(z => z.Id), (z, x) => z.Remove(x), z => z.SudoDiscord).ConfigureAwait(false);
        }

        [Command("sudoku")]
        [Alias("kill", "shutdown")]
        [Summary("Causes the entire process to end itself!")]
        [RequireOwner]
        // ReSharper disable once UnusedParameter.Global
        public async Task ExitProgram()
        {
            await Context.Channel.EchoAndReply("Shutting down... goodbye! **Bot services are going offline.**").ConfigureAwait(false);
            Environment.Exit(0);
        }
    }
}
