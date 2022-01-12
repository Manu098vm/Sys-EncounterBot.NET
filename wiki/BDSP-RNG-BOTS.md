Since _Sys-EncounterBot 2.5_, the bot can handle some automated RNG routines.


These routines are here mainly to assist the player in the RNG Abuse project, allowing more precise RNGs.
In order to use the bots, you must already understand the RNG basics.

If you want to know more about RNG Abuse in BDSP games, [this](https://github.com/zaksabeast/PokemonRNGGuides/tree/rough-drafts/guides/Brilland%20Diamond%20annd%20Shining%20Pearl) might be a good point to start.


If you want to do manual RNGs, the Bot could still be useful with the Delay Calculation and the Log Advances routines.
The first one allows you to calculate delays for the encounters you want to RNG, while the latter logs the advances and states from your game.


If you want more automated stuff, you have few options: ExternalCalc and AutoCalc routines.

AutoCalc will automatically calculate the advances needed for the encounter you set in the Stop Conditions, it will advance frames and it will try to hit the target without any needs of human inputs. Keep in mind that the delays might vary due to blinks, and in some noisy areas most of advances are skipped by the game. Due to these facts, the bot might fail to hit the target. In these cases, the routine will automatically restart the game searching for a new target.
ExternalCalc must be used in combo with [PokéFinder](https://github.com/Admiral-Fish/PokeFinder/releases) or [Chatot](https://chatot.pokemonrng.com/#/bdsp). Given a target, it will automatically try advance frames and hit it like the previous routine.

Egg RNG is not compatible with AutoCalc mode.

More info about RNG bots here:

1. [Generator](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/Generator)
1. [LogAdvances](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/logadvances)
1. [DelayCalc](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/DelayCalc)
1. [AutoRNG: AutoCalc and ExternalCalc](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/AutoRNG)