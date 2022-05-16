# Botsome
Botsome monitors your docker containers and will ~~steal~~ use the bot tokens of your Discord bots, and whenever someone sends a message containing the emote you specified, it will pick a random bot token and use it to react with that message with the same emote.

As an unwanted side effect, if a bot container is running but isn't actually connected to Discord, the bot will still appear online because of Botsome's connection using its token.

## Etymology
Botsome is the name of an emote in a series of "wholesome" derived emotes, in one of my servers.

## Docker deployment
See docker-compose.yml for a working setup. Do not expose the app by publishing ports or running an ingress on the botsome-network, because it has no security measures in place whatsoever.

To make it use one of your bots, add the label `me.foxite.botsome: true` to its service (so you need to use a docker-compose file for your bots).

Botsome will read the environment variables of your bot. By default, it will use the contents of the BOT_TOKEN variable, but if you have your bot token in another viarable, specify its name in the `me.foxite.botsome.envvar` label.
