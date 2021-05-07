This Wiki page is a mirror of the official SysBot one.

Discord integration requires configuration in the Hub for it to be functional in a server.

# Setup
Create a new Discord Application and Bot for your server on [Discord's Developer Portal](https://discord.com/developers/applications).

To invite the Bot to your server, you must generate an OAuth2 URL with the correct permissions. Click on the OAuth2 tab in the sidebar, select bot under "Scopes," and grant your bot permissions. You will need at minimum:

* Send Messages
* Read Message History

Under the Bot tab, make your bot private to prevent other people from inviting it to their servers and using your Switch.

* Enable Privileged Gateway Intents and Server Members Intent under the same tab.

# Before Starting
Once you have added your Discord bot to your server, paste your Bot Token into the program's Hub config. Do not share the token or other people will be able to access your Discord bot account.

When you Start your bots via the GUI, the Discord bot will be launched.

Configure the bot's settings on your server. If you only want it in a few specific channels, remove all the permissions on the bot's role itself and add the role to specific channels only. Ensure that you do not have any other roles that are restricting the bot's permissions (including the "everyone" role), and you enter any extra codes needed if you have 2FA enabled.

# Settings
## Basic Settings
* CommandPrefix: Used before all commands. Default is /.
* BotGameStatus: Displays the status of the bot as the game it is playing.
* UserTag: User with this ID will receive a ping if an Encounter Bot will match a Stop Condition.
* HelloResponse: Customized response for /hi.
* RoleSudo: Gives a role admin powers over the bot and bypasses all command restrictions. Should not be given out lightly as sudo users can cause damage by changing your bot configurations.
* GlobalSudoList / AllowGlobalSudo: List of user IDs that have admin powers over the bot in all servers, all channels, everywhere. Turn on the second setting to enable this.
* LoggingChannels: List of comma-separated channels where the bot posts its logs.

_Refer to the specific bot page or **/help** commands for more details._

