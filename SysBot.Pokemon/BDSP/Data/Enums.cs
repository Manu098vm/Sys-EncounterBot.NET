namespace SysBot.Pokemon
{
    public enum PokeEvents
	{
        None,
        ManaphyEgg,
        BirthDayHappiny,
	}

    public enum RNGType
	{
        Wild,
        Starter,
        Legendary,
        MysteryGift,
        Roamer,
        Egg,
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
        TEST,
	}

    public enum CheckMode
	{
        Team,
        Box,
        Wild,
        Seed,
	}

    public enum WildMode
	{
        None,
        Grass,
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