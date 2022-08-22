namespace SysBot.Pokemon
{
    public enum LetsGoMode
    {
        /// <summary>
        /// Bot will soft reset Stationaries
        /// </summary>
        Stationary,

        /// <summary>
        /// Bot will soft reset for Fossils
        /// </summary>
        Fossils,

        /// <summary>
        /// Bot will soft reset for Gifts
        /// </summary>
        Gifts,

        /// <summary>
        /// Bot will soft reset trades
        /// </summary>
        Trades,

        /// <summary>
        /// Bot will soft reset for Go Park entities
        /// </summary>
        GoPark,

        /// <summary>
        /// Bot will scan for any mon
        /// </summary>
        LiveStatsChecking,
    }
    public enum LGPEOverworldMode
    {
        /// <summary>
        /// Bot will scan for any mon
        /// </summary>
        OverworldSpawn,

        /// <summary>
        /// Bot will scan for Birds
        /// </summary>
        WildBirds,
    }
    public enum Lure
    {
        None = 0,
        Lure = 900,
        SuperLure = 901,
        MaxLure = 902,
    }
    public enum TextSpeed
    {
        Slow,
        Normal,
        Fast,
    }

    public enum LGPESpecies : ushort
    {
        None,
        Bulbasaur,
        Ivysaur,
        Venusaur,
        Charmander,
        Charmeleon,
        Charizard,
        Squirtle,
        Wartortle,
        Blastoise,
        Caterpie,
        Metapod,
        Butterfree,
        Weedle,
        Kakuna,
        Beedrill,
        Pidgey,
        Pidgeotto,
        Pidgeot,
        Rattata,
        Raticate,
        Spearow,
        Fearow,
        Ekans,
        Arbok,
        Pikachu,
        Raichu,
        Sandshrew,
        Sandslash,
        NidoranF,
        Nidorina,
        Nidoqueen,
        NidoranM,
        Nidorino,
        Nidoking,
        Clefairy,
        Clefable,
        Vulpix,
        Ninetales,
        Jigglypuff,
        Wigglytuff,
        Zubat,
        Golbat,
        Oddish,
        Gloom,
        Vileplume,
        Paras,
        Parasect,
        Venonat,
        Venomoth,
        Diglett,
        Dugtrio,
        Meowth,
        Persian,
        Psyduck,
        Golduck,
        Mankey,
        Primeape,
        Growlithe,
        Arcanine,
        Poliwag,
        Poliwhirl,
        Poliwrath,
        Abra,
        Kadabra,
        Alakazam,
        Machop,
        Machoke,
        Machamp,
        Bellsprout,
        Weepinbell,
        Victreebel,
        Tentacool,
        Tentacruel,
        Geodude,
        Graveler,
        Golem,
        Ponyta,
        Rapidash,
        Slowpoke,
        Slowbro,
        Magnemite,
        Magneton,
        Farfetchd,
        Doduo,
        Dodrio,
        Seel,
        Dewgong,
        Grimer,
        Muk,
        Shellder,
        Cloyster,
        Gastly,
        Haunter,
        Gengar,
        Onix,
        Drowzee,
        Hypno,
        Krabby,
        Kingler,
        Voltorb,
        Electrode,
        Exeggcute,
        Exeggutor,
        Cubone,
        Marowak,
        Hitmonlee,
        Hitmonchan,
        Lickitung,
        Koffing,
        Weezing,
        Rhyhorn,
        Rhydon,
        Chansey,
        Tangela,
        Kangaskhan,
        Horsea,
        Seadra,
        Goldeen,
        Seaking,
        Staryu,
        Starmie,
        MrMime,
        Scyther,
        Jynx,
        Electabuzz,
        Magmar,
        Pinsir,
        Tauros,
        Magikarp,
        Gyarados,
        Lapras,
        Ditto,
        Eevee,
        Vaporeon,
        Jolteon,
        Flareon,
        Porygon,
        Omanyte,
        Omastar,
        Kabuto,
        Kabutops,
        Aerodactyl,
        Snorlax,
        Articuno,
        Zapdos,
        Moltres,
        Dratini,
        Dragonair,
        Dragonite,
        Mewtwo,
        Mew,
    }
}
