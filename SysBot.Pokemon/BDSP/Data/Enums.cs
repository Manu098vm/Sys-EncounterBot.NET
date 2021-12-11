namespace SysBot.Pokemon
{
    public enum RNGType
	{
        Honey,
        HoneyTree,
        Starter,
        SweetScent,
        Fishing,
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
	}

    public enum CheckMode
	{
        Team,
        Box,
        Wild,
	}

    public enum DexMode
	{
        A,
        B,
	}
}