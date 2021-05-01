namespace SysBot.Pokemon
{
    public enum LetsGoMode
    {
        /// <summary>
        /// Bot will test the offsets
        /// </summary>
        TestRoutine,

        /// <summary>
        /// Bot will scan for any mon
        /// </summary>
        OverworldSpawn,

        /// <summary>
        /// Bot will scan for Birds
        /// </summary>
        WildBirds,

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
    }
}