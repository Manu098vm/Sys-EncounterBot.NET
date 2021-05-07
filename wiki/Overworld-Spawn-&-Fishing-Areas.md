# Quick Guide for (Any) Pokémon Hunting

## Game Setup
Save the game in any location in which there are Wild Spawns. With this routine you can also hunt the Swords of Justice, Keldeo and the Fihishing spots. You can't hunt Hidden Wild Encounters with this bot. It suggested to use the [Vertical & Horizontal Line bot](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/Breeding,-Wild,-Legendary-Dogs,-Eternatus-and-Calyrex) for those.

For fishing encounters, save in front of a fishing spot.

If a wild encounter starts while the routine is running, the bot will automatically escape from the battle.

Once the result is found **don't fly in another location, or the encounter will be rerolled.** Save the game and go straight ahead to the spawned Pokémon. If you fail the encounter, you can restart your game, the encounter will remain the same.

## Bot Setup
First, download the Sys-EncounterBot: https://github.com/Manu098vm/Sys-EncounterBot.NET/releases.

Go in the Hub tab and select `OverworldSpawn` under `SWSH_OverworldScan`.

For fishing encounters toggle the `GetOnOffBike` to true.

Set the `WaitMsBeforeSave` to any value of your liking. This is the timing that will elapse between each scan, expressed in Milliseconds.

If you want, you can personalize the routine with some automatic movements. This can be really useful to make some Pokémon despawn and respawn. Taking as example the Swords of Justice and Keldeo, their stats/shinyness/nature/marks are rerolled each time they spawn. This applies to any wild Pokémon in the overworld. In order to make them to spawn, you can use a personalized Movement routine. 

An exemple is given here:
![overworld settings](https://i.imgur.com/oq1jTc1.png)

In this example, the caracter will go North for 5 seconds and will revert to South for 5 seconds, making Pokémon despawn when they are out the range of view, and respawn when the character returns to the original location. Unwanted battles with wild encounters can potentially break the movement chain, resulting in the character going to unwanted areas.

Scroll to `ConsoleLanguage` under `FeatureToggle` and set it to your language.

Scroll to `StopConditions` and set them according to what you're looking for.

Go back to the Bots tab, select `SWSH_OverworldScan`, click `Add` then `Start All`.