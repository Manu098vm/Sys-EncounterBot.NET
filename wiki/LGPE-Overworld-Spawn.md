# Quick Guide for (Any) Pokémon Hunting

## Game Setup
Save the game in any location in which there are Wild Spawns of the Pokémon you want to hunt and start the bot.
![lgpe location](https://i.imgur.com/gNdRomI.jpg)

If a wild encounter starts while the routine is running, the bot will automatically escape from the battle.

Once the result is found go straight ahead to the spawned Pokémon. Closing the game will make the Pokémon to despawn. It suggested to use Golden Razz Berry and/or Master Balls.

If the Spawn happens far away from the player, it is possible that the Pokémon will despawn before the player can reach it. It can happens, so be patient.

## Bot Setup
First, download the Sys-EncounterBot: https://github.com/Manu098vm/Sys-EncounterBot.NET/releases.

Go in the Hub tab and select `OverworldSpawn` under `LGPE_OverworldScan`. 
If you're looking specifically for (all) the three wild Legendary Birds, set `WildBirds` instead of `OverworldSpawn`.
The `WildBirds` method will loop until one of three birds will appear.

Standard `StopConditions` settings cannot be checked for LGPE Overworld scans. Set the `StopOnSpecies` value to make the bot stop at the wanted species. If you're hunting specifically for shinies, set `OnlyShiny` to true.

If you wish to, you can set a `ChainSpecies` and `ChainCount` value to increase the spawn quality and the likelihood of a shiny appearance.

It is possible to personalize the routine with some automatic movements. Unexpected behaviour can occur if a shiny is detected while changing area. Unwanted battles with wild encounters can potentially break the movement chain, resulting in the character going to unwanted areas. It is recommended to avoid automatic movements except for specific cases.

Go back to the Bots tab, select `LGPE_OverworldScan`, click `Add` then `Start All`.

## Unfreeze
The Shiny Check routine works locally in your switch (thanks Zaksabeast!), if `OnlyShiny` is set to true. If the EncounterBot executable will crash, or if it will be closed unexpectedly, the Shiny Check routine will still run in the switch background. This means that if a shiny is found, the game will freeze. 
If this happens, select `TestRoutine` under `LGPE_OverworldScan` and select `Unfreeze` under `TestRoutine`. Go back to the Bots tab, select `LGPE_OverworldScan`, click `Add` then `Start All`. The game will unfreeze and you can catch the shiny appeared, if you wish to.
Rebooting the game will also work to remove the ShinyCheck routine, but you will lose the shiny this way.