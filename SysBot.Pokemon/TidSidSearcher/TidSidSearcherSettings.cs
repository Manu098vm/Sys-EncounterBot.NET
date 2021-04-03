using System.ComponentModel;

namespace SysBot.Pokemon
{
    public class TidSidSearcherSettings
    {
        private const string Encounter = nameof(Encounter);
        public override string ToString() => "TidSid Searcher Settings";

        [Category(Encounter), Description("Enter the TID you're looking for. Ignored if -1.")]
        public int TID { get; set; } = -1;

        [Category(Encounter), Description("Enter the SID you're looking for. Ignored if -1.")]
        public int SID { get; set; } = -1;
    }
}