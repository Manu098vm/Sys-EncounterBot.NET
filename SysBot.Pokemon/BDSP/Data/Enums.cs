namespace SysBot.Pokemon
{
    public enum PokeEvents
	{
        None,
        ManaphyEgg,
        BirthDayHappiny,
        PokeCenterPiplup,
        KorDawnPiplup,
        KorRegigigas,
        OtsukimiClefairy,
	}

    public enum RNGType
	{
        Wild,
        Stationary,
        Stationary_3IVs,
        MysteryGift,
        Roamer,
        Egg,
        Gift,
        Gift_3IV,
        Custom,
	}

    public enum AutoRNGMode
	{
        AutoCalc,
        ExternalCalc,
	}

    public enum RNGRoutine
	{
        DelayCalc,
        LogAdvances,
        AutoRNG,
        Generator,
        CheckAvailablePKM,
	}

    public enum CheckMode
	{
        Encounter,
        Seed,
        Box1Slot1,
        TeamSlot1,
        TeamSlot2,
	}

    public enum WildMode
	{
        None,
        Grass_or_Cave,
        Surf,
        Swarm,
        OldRod,
        GoodRod,
        SuperRod,
	}

    public enum GameTime
	{
        Morning = 0, //4am-10am
        Day = 1, //10am-5pm
        Sunset = 2, //5pm-8pm
        Night = 3, //8pm-2am
        DeepNight = 4, //2am-4am
	}
}