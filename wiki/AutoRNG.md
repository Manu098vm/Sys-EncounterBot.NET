AutoRNG allows to automate the RNG process. This routine is not perfect, as it do not take account of advancement patterns/timelines that might be present in some scenarios. There are two variations of the routine: AutoCalc and ExternalCalc.

AutoCalc will automatically calculate the advances needed for the set StopConditions, the bot will advance frames and it will try to hit the target without any needs of human inputs. Since delays might vary due to blinks, and in noisy areas some advances are skipped by the game, the bot might fail to hit the target. In these cases, the routine will automatically restart the game searching for a new target. ExternalCalc must be used in combo with PokéFinder or Chatot. Given a target, it will automatically try advance frames and hit it as the previous routine.

## Game Setup
Stand in any overworld spot without any menu opened, as near as possible at the start of the encounter you want to trigger.
If using `ExternalCalc` mode, pause the game by pressing Home and waiting in the Home menu. Once you start the bot, resume the game to continue the routine. This can also be done with `AutoCalc` mode as well.

## Bot Setup
Download or compile the latest Sys-EncounterBot.

Go in the Hub tab and select `AutoRNG` under `BDSP_RNG->Routine`, then open the `AutoRNGSettings`.

Select `AutoCalc` or `ExternalCalc` in `AutoRNGMode`, depending on the mode you want to use. Eggs rng must be calculated with external programs, so only use ExternalCalc for those.

Set `RebootIfFailed` to true if you're using AutoCalc and you want to automatically reboot the game if the bot fail to hit the target. The  game will also be rebooted if the target advance is above the `RebootValue`. You can see that value as the Maximum number of advances you're available to wait.

Set the `Delay` according to your calculations with the [DelayCalc routine](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/DelayCalc).

The `Target` setting is only used in the ExternalCalc mode. Once you'll start the bot you'll be requested to return to the settings and input a value in this field.

If you're seeking for high targets, it could be useful to set a `ScrollDexUntil` value. If that value is above 0, the Bot will automatically use Pokédex scrolling to advance frames faster.

Set the `Actions` field with all the buttons needed to activate your encounter. Stationaries most of times requires an "A, A" button sequence. You can also personalize the sequence to click the buttons required to open your team and use Sweet Scent, or to open the bag and use the Honey.
You might also want to set a different `ActionTimings`, that's the amount of milliseconds waited between each action.
The bot always tries to press the latest button at the right timing to hit your target, but this could be failed if the bot does not have enough time to do all the actions before the arrival of the target frame. Please set the `ScrollDexUntil` value accordingly to give enough time to the bot to do all the actions.

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

7. If you want to do an EggRNG (ExternalCalc is required), use the settings as follows.

![img7](https://i.imgur.com/yPmpuHz.png)

By Selecting `Egg` as `RNGType`, you'll also be shown the Steps needed to trigger the egg seed generation. There's a chance an Egg seed will be generated every 180 steps.

Scroll down to `Stop Conditions` and set them accordingly to your target.

Go back to the Bots tab, select `BDSP_RNG`, click `Add` then `Start All`.

If you're using `ExternalCalc` mode, you'll be shown the current states (S0, S1, S2, S3) and the routine will pause itself. You can use the states in [PokéFinder](https://github.com/Admiral-Fish/PokeFinder/releases) or [Chatot](https://chatot.pokemonrng.com/#/bdsp). PokéFinder only uses two states instead of four. You can unify S1 and S2 to obtain the first state asked by PokéFinder, while by unifying S2 and S3 you'll obtain the second state. More info about that [here](https://github.com/zaksabeast/PokemonRNGGuides/tree/rough-drafts/guides/Brilland%20Diamond%20annd%20Shining%20Pearl).
Once you got your target, set the value in `Hub`->`BDSP_RNG`->`AutoRNGSettings`->`Target` and press the Enter key, the routine will be then automatically resumed.

**N.B: If you used CaptureSight, sysbot-base will be unable to attach to the game process.**