using PKHeX.Core;
using System;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchStick;
using static SysBot.Pokemon.PokeDataOffsets;

namespace SysBot.Pokemon
{
    public class TidSidSearcher : PokeRoutineExecutor
    {
        private readonly PokeTradeHub<PK8> Hub;
        private readonly int DesiredTID;
        private readonly int DesiredSID;

        public TidSidSearcher(PokeBotState cfg, PokeTradeHub<PK8> hub) : base(cfg)
        {
            Hub = hub;
            DesiredTID = Hub.Config.TidSidSearcherSettings.TID;
            DesiredSID = Hub.Config.TidSidSearcherSettings.SID;
        }

        public override async Task MainLoop(CancellationToken token)
        {
            SAV8SWSH sav = new SAV8SWSH();

            while(!token.IsCancellationRequested) {
                Log("Starting main loop.");
                Config.IterateNextRoutine();

                // Clear out any residual stick weirdness.
                await ResetStick(token).ConfigureAwait(false);

                sav = await GetFakeTrainerSAV(token).ConfigureAwait(false);
                LanguageID GameLang = (LanguageID)sav.Language;
                GameVersion Version = sav.Version;
                string InGameName = sav.OT;
                Log($"New profile detected:\nOT Name: {InGameName}\nTID:{sav.DisplayTID:000000}\nSID:{sav.DisplaySID:0000}");
                if(DesiredTID == -1 || DesiredTID == sav.DisplayTID)
                {
                    if(DesiredSID == -1 || DesiredSID == sav.DisplaySID)
                    {
                        Log("Result found. Ending the routine.");
                        continue;
                    }
                }
            }
            await DetachController(token).ConfigureAwait(false);
        }

        private async Task ResetStick(CancellationToken token) => await SetStick(LEFT, 0, 0, 0_500, token).ConfigureAwait(false);

    }
}
