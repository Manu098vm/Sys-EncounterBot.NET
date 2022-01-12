This routine is useful to calculate delays for any kind of encounter. Since the delays might vary due to blinks and noisy routes, it is highly suggested to repeat this routine at least 2-3 times, to check the variation, if any. Once you have your delay, you can use it for the [AutoRNG](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/AutoRNG) routine. If you find different delays for the same encounter, a good strategy would be to use the delay you got more commonly.

## Game Setup
Wait in the last screen input before your encounter starts (or before the roamer 3d model disappearing). For example, last screen input for legendaries is after their cry.

**N.B: Do not calculate delay for a Mystery Gift, you would lose the chance to RNG for it. Mystery Gifts delay is always 0.**

## Bot Setup
Download or compile the latest Sys-EncounterBot.

Go in the Hub tab and select `DelayCalc` under `BDSP_RNG->Routine`, then open the `DelayCalcSettings`.

Use the Combo Box to select the button that will start your encounter. In most cases you should use `A`. 


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




Go back to the Bots tab, select `BDSP_RNG`, click `Add` then `Start All`.

**N.B: If you used CaptureSight, sysbot-base will be unable to attach to the game process.**