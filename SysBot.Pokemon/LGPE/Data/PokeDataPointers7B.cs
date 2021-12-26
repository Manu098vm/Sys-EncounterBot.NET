using System.Collections.Generic;

namespace SysBot.Pokemon
{
	public class PokeDataPointers7B
	{
		public IReadOnlyList<long> SpeciesComboPointer { get; } = new long[] { 0x160E410, 0x50, 0x770, 0x40, 0x298 };
		public IReadOnlyList<long> CatchComboPointer { get; } = new long[] { 0x160E410, 0x50, 0x840, 0x20, 0x1D0 };
	}
}