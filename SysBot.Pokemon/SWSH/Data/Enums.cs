namespace SysBot.Pokemon
{
    public enum SwordShieldDaycare
    {
        WildArea,
        Route5,
    }

    public enum ScanMode
    {
        /// <summary>
        /// Bot will scan for any mon
        /// </summary>
        OverworldSpawn,

        /// <summary>
        /// Bot will reroll Zapdos
        /// </summary>
        G_Articuno,

        /// <summary>
        /// Bot will reroll Zapdos
        /// </summary>
        G_Zapdos,

        /// <summary>
        /// Bot will reroll Moltres
        /// </summary>
        G_Moltres,

        /// <summary>
        /// Bot will reroll Moltres
        /// </summary>
        IoA_Wailord,
    }

    public enum FossilSpecies
    {
        /// <summary>
        /// Bot will revive Dracozolt
        /// </summary>
        Dracozolt,

        /// <summary>
        /// Bot will revive Arctozolt
        /// </summary>
        Arctozolt,

        /// <summary>
        /// Bot will revive Dracovish
        /// </summary>
        Dracovish,

        /// <summary>
        /// Bot will revive Arctovish
        /// </summary>
        Arctovish,
    }

    public enum LairSpecies : ushort
    {
        None = 0,
        Articuno = 144,
        Zapdos = 145,
        Moltres = 146,
        Mewtwo = 150,
        Raikou = 243,
        Entei = 244,
        Suicune = 245,
        Lugia = 249,
        HoOh = 250,
        Latias = 380,
        Latios = 381,
        Kyogre = 382,
        Groudon = 383,
        Rayquaza = 384,
        Uxie = 480,
        Mesprit = 481,
        Azelf = 482,
        Dialga = 483,
        Palkia = 484,
        Heatran = 485,
        Giratina = 487,
        Cresselia = 488,
        Tornadus = 641,
        Thundurus = 642,
        Landorus = 645,
        Reshiram = 643,
        Zekrom = 644,
        Kyurem = 646,
        Xerneas = 716,
        Yveltal = 717,
        Zygarde = 718,
        TapuKoko = 785,
        TapuLele = 786,
        TapuBulu = 787,
        TapuFini = 788,
        Solgaleo = 791,
        Lunala = 792,
        Nihilego = 793,
        Buzzwole = 794,
        Pheromosa = 795,
        Xurkitree = 796,
        Celesteela = 797,
        Kartana = 798,
        Guzzlord = 799,
        Necrozma = 800,
        Stakataka = 805,
        Blacephalon = 806
    }
}
