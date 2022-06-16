This Wiki page is a mirror of the official SysBot one.

These settings need be configured for Bots to tell the bot when it has found its target. It is your responsibility to ensure that these settings make sense for what you are hunting. **These conditions are not checked by the LGPE OverworldScan Bot.**

* **StopOnSpecies**: Stop only on Pokémon of a particular species. If set to "None", it will not check species at all.
* **TargetNatures**: Stop only on Pokémon with specific natures. If left empty, it will allow any nature.
* **TargetMinIVs** / **TargetMaxIVs**: Allows you to specify minimum and maximum IVs you want the bot to search for. When defining a spread, use "x" for unchecked IVs and "/" as a separator. By default, TargetIVs is empty and the bot will not check any IVs.
  * Examples: specifying 31/0/25/25/25/25 will select for any Pokémon with 31 HP IV, 0 Att IV, and 25-31 of everything else. Specifying 31/0/22/x/x/x will select for any Pokémon with 31 HP IV, 0 Att IV, at least 22 Def IV, and any IV for the rest.
* **ShinyTarget**: Allows you to specify the type of shiny to stop on. "DisableOption" ignores the shiny status.
* **MarkOnly**: Only stops on Pokémon with a Mark if set to "true".
* **UnwantedMarks**: List of marks to ignore, separated by commas. This should be a case-sensitive list of Mark names separated by commas like `Fishing Mark, Uncommon Mark, Dawn Mark`. Restart the program after adding your settings..
* **CaptureVideoClip** / **ExtraTimeWaitCaptureVideo**: If enabled, the bot will hold Capture to record a 30 second video clip when it finds a target. Adjust the time in milliseconds if you want it to wait longer after a match. Beware that many encounters are detected before the Pokémon is visible on screen.
* **MatchShinyAndIV**: If set to "true", the bot will only stop if the encounter matches the TargetIvs and ShinyTarget. Otherwise, it will stop if either of them match. This is useful if you are okay with specific IVs or any shiny with any IVs, but do not necessarily want both on the same Pokémon.
* **MatchFoundEchoMention**: If not empty, this will be prepended to the "Result Found" notification to Echo alerts for whomever is specified. For Discord, use `<@userIDnumber>` for a mention.

**N.B: Most of Stop Conditions settings are initialized at Bot Creation. That means that if you edit them you'll need either to delete and recreate the Bot or to close and reopen the program.**