# Disclaimer
I am doing this project for personal use and, above all, as a learning tool. Some bots are a proof of concept and they may work for me but not for you. If you run into any problems or have any suggestions, feel free to contact me, but please note that I may not be able to offer the support you are looking for. This is by no means a polished product.

# Quick Guide
1. You need a CFW'ed Switch, [Atmosphére](https://github.com/Atmosphere-NX/Atmosphere/releases) is needed!
1. Install the required sys-modules. If you want to control the Switch over the local network, [install sys-botbase](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/Install-sys-botbase), otherwise you can [install usb-botbase](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/Install-usb-botbase) and use a USB-C cable to connect your console to your computer. For general usage, USB-Botbase is arguibly faster and more reliable than sys-botbase. Please note that you can't have both the sysmodules at the same time.
1. Disable any CFW-based RAM read/writing processes and cheats that can shift your RAM. Examples: EdiZon, LayeredFS mods, CaptureSight, Tesla.
1. Dock your joycons and disable all extra controllers.

# EncounterBot configurations
Check the wiki page for the Pokémon(s) you want to hunt for detailed instructions on the initial in-game setup and for the Hub settings.
Set as follows the Sys-EncounterBot "Bots" tab:
![Sys-EncounterBot mainpage](https://i.imgur.com/pFreEVR.png)

RED: Your Nintendo Switch IP address (only if using sys-botbase).

BLUE: Port used to communicate to the switch. Refer to the [sys-botbase](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/Install-sys-botbase) or [usb-botbase](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/Install-usb-botbase) installation page to check which is the right port for you.

GREEN: Select WiFi if using sys-botbase, USB if using usb-botbase.

PURPLE: The Bot you want to use. Check the guide index for detailed instructions for every bot available. Click `Start All` to start the bot(s), and `Stop All` to stop them.

## Troubleshooting
1. If you're experiencing lags/freeze during Sword or Shield gameplay, it means that the ldn-mitm sysmodule can't detect any reliable WiFi connection. Ensure to have your console plugged to your home Wi-Fi or uninstall ldn-mitm (remove the folder named `4200000000000010` from `SD` - `Atmosphere` - `contents`) and use [usb-botbase](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/Install-usb-botbase) instead.
1. If you're experiencing freeze during Let's Go gameplay, follow the [unfreeze](https://github.com/Manu098vm/Sys-EncounterBot.NET/wiki/LGPE-Overworld-Spawn#unfreeze) instructions.
1. Check the [original SysBot.NET wiki page](https://github.com/kwsch/SysBot.NET/wiki/Troubleshooting) for common troubleshooting. If you still need help, feel free to join [my Discord server](https://discord.gg/WFbcUd6U8d). As this project is not maintened by the original SysBot.NET devs, please don't hassle them.

It is suggested to delete previous configurations files (config.json) before using a new release.

## Extra
If you already caught the Pokémon you're interested in and now you want to rehunt it, you can reset the flags, read this page: https://projectpokemon.org/home/forums/topic/58060-enablingdisabling-event-flags-in-sword-shield/