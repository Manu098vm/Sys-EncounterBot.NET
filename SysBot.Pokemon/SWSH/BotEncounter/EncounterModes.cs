namespace SysBot.Pokemon
{
    public enum EncounterMode
    {
        /// <summary>
        /// Bot will log any battle-based encounter
        /// </summary>
        LiveStatsChecking,

        /// <summary>
        /// Bot will log any battle-based encounter
        /// </summary>
        MaxLair,

        /// <summary>
        /// Bot will soft reset Eternatus
        /// </summary>
        Eternatus,

        /// <summary>
        /// Bot will soft reset Regigigas
        /// </summary>
        Regigigas,

        /// <summary>
        /// Bot will soft reset for Regis
        /// </summary>
        Regis,

        /// <summary>
        /// Bot will soft reset the Legendary Dogs
        /// </summary>
        Dogs_or_Calyrex,

        /// <summary>
        /// Bot will soft reset the Swords of Justice Trio
        /// </summary>
        SwordsJustice,

        /// <summary>
        /// Bot will soft reset the Swords of Justice Trio
        /// </summary>
        Keldeo,

        /// <summary>
        /// Bot will soft reset Spiritomb
        /// </summary>
        Spiritomb,

        /// <summary>
        /// Bot will soft reset any Strong Spawn
        /// </summary>
        StrongSpawn,

        /// <summary>
        /// Bot will soft reset Gifts
        /// </summary>
        Gifts,

        /// <summary>
        /// Bot will soft reset Gifts
        /// </summary>
        Fossils,

        /// <summary>
        /// Bot will soft reset for in-game trades
        /// </summary>
        Trades,

        /// <summary>
        /// Bot will move back and forth in a straight vertical path to encounter Pokémon
        /// </summary>
        VerticalLine,

        /// <summary>
        /// Bot will move back and forth in a straight horizontal path to encounter Pokémon
        /// </summary>
        HorizontalLine,

        /// <summary>
        /// Bot resets Motostoke Gym encounters
        /// </summary>
        MotostokeGym,
    }
}