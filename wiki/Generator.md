The Generator basically do what PokéFinder do, but slower. 
This routine can still be useful if you're lazy and you don't want to copy/paste states into external programs, or if you want to calculate **wild encounter slots** reading the pg location directly from ram. 
Seriously, if you're not lazy as I am, just use PokeFinder.

## Game Setup
If you want to calculate proper encounter slots for wild encounters, stand in the route your target will be.

For any other calculation, just ensure that your console is up and running the game.

## Bot Setup
Download or compile the latest Sys-EncounterBot.

Go in the Hub tab and select `Generator` under `BDSP_RNG->Routine`, then open the `GeneratorSettings`.

If you're seeking for specific target calculation, leave `GeneratorVerbose` to false, and set `GeneratorMaxResults` as the max advances you're available to wait to hit your target.
Otherwise, if you just wanna check which Pokémon the next few frames will generate, set `GeneratorVerbose` to true. This will make the routine slower due to logging.


Scroll down and set `RNGType`, `CheckMode`, `WildMode` and `Event` according to your target.

The settings should be self-explainatory, but here's some examples.

1. If you want to RNG a Pokémon from a Grass, or a Grotto encounter use the settings as follows.

![img1](https://i.imgur.com/EbAIFMr.png)

2. If you want to RNG a Pokémon from a Honey Tree, use the settings as follows.

![img2](https://i.imgur.com/P87Avll.png)

3. If you want to RNG an in-game Legendary or Mythical Pokémon, use the settings as follows.

![img3](https://i.imgur.com/jKUUXIw.png)

4. If you want to RNG a Roamer, use the settings as follows.

![img4](https://i.imgur.com/oXzT6qd.png)

5. If you want to RNG a ManaphyEgg from MysteryGift that will be sent to the Slot 2 of your Team, use the settings as follows, use the settings as follows.

![img5](https://i.imgur.com/FQvipTT.png)

6. If you want to RNG a Birthday Happiny Egg from Mystery Gift that will be sent to boxes, use the settings as follows.

![img6](https://i.imgur.com/2KJNsGs.png)




Scroll down to `Stop Conditions` and set them according to your target.

Go back to the Bots tab, select `BDSP_RNG`, click `Add` then `Start All`.

**N.B: If you used CaptureSight, sysbot-base will be unable to attach to the game process.**