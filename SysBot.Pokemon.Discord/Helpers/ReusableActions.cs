using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Discord
{
    public static class ReusableActions
    {

        public static async Task RepostPKMAsShowdownAsync(this ISocketMessageChannel channel, IAttachment att)
        {
            if (!PKX.IsPKM(att.Size))
                return;
            var result = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
            if (!result.Success)
                return;

            var pkm = result.Data!;
            await channel.SendPKMAsShowdownSetAsync(pkm).ConfigureAwait(false);
        }


        public static async Task EchoAndReply(this ISocketMessageChannel channel, string msg)
        {
            // Announce it in the channel the command was entered only if it's not already an echo channel.
            EchoUtil.Echo(msg);
            if (!EchoModule.IsEchoChannel(channel))
                await channel.SendMessageAsync(msg).ConfigureAwait(false);
        }

        public static async Task SendPKMAsShowdownSetAsync(this ISocketMessageChannel channel, PKM pkm)
        {
            var txt = GetFormattedShowdownText(pkm);
            await channel.SendMessageAsync(txt).ConfigureAwait(false);
        }

        public static string GetFormattedShowdownText(PKM pkm)
        {
            int TID = (pkm.Gen7 || pkm.Gen8) ? pkm.TrainerID7 : pkm.TID;
            int SID = (pkm.Gen7 || pkm.Gen8) ? pkm.TrainerSID7 : pkm.SID;
            string showdowntext = ShowdownParsing.GetShowdownText(pkm);
            if (pkm.IsShiny && pkm.ShinyXor == 0)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Square");
            else if (pkm.IsShiny)
                showdowntext = showdowntext.Replace("Shiny: Yes", "Shiny: Star");
            string showdown = $"{showdowntext}\nOT: {pkm.OT_Name}\nTID: {TID}\nSID: {SID}";
            return Format.Code(showdown);
        }

        public static List<string> GetListFromString(string str)
        {
            // Extract comma separated list
            return str.Split(new[] { ",", ", ", " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        public static string StripCodeBlock(string str) => str.Replace("`\n", "").Replace("\n`", "").Replace("`", "").Trim();
    }
}