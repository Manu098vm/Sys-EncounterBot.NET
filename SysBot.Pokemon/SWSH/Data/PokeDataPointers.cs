﻿using System.Collections.Generic;

namespace SysBot.Pokemon
{
	public static class PokeDataPointers
	{
		public static IReadOnlyList<long> GiftPokemon { get; } = new long[] { 0x28F4060, 0x208, 0x08, 0x58, 0x0};
		public static IReadOnlyList<long> LairReward { get; } = new long[] { 0x28F4060, 0x1B0, 0x68, 0x58, 0xD0};
	}
}
