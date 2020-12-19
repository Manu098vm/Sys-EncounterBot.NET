namespace SysBot.Pokemon
{
    public enum EncounterMode
    {
        /// <summary>
        /// Bot will move back and forth in a straight vertical path to encounter Pokémon
        /// </summary>
        VerticalLine,

        /// <summary>
        /// Bot will move back and forth in a straight horizontal path to encounter Pokémon
        /// </summary>
        HorizontalLine,

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
        LegendaryDogs,

        /// <summary>
        /// Bot will soft reset the Swords of Justice Trio
        /// </summary>
        SwordsJustice,

        /// <summary>
        /// Bot will soft reset Spiritomb
        /// </summary>
        Spiritomb,
    }
}