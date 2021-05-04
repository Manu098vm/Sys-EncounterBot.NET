namespace SysBot.Pokemon
{
    public enum LetsGoMode
    {
        /// <summary>
        /// Bot will soft reset Stationaries
        /// </summary>
        Stationary,

        /// <summary>
        /// Bot will soft reset for Gifts
        /// </summary>
        Gifts,

        /// <summary>
        /// Bot will soft reset trades
        /// </summary>
        Trades,

        /// <summary>
        /// Bot will scan for any mon
        /// </summary>
        LiveStatsChecking,
    }
}