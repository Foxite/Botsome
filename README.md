# Botsome
This repository consists of two projects, a bot companion and a coordinator. When put together, your Discord bots will respond to someone using a specified emote, by reacting to the message with that emote.

The bot companion is supposed to be run next to any of your bots, and you should give it the bot token of that bot. It listens for messages containing only the emote you specified, and it forwards the ID of that messge to the coordinator. The coordinator then waits for a certain delay and picks a random bot companion that reported on that message, and then that bot companion will react to the message with the same emote.

As an unwanted side effect, your bots will always appear online, even if the actual bot container is offline.

## Etymology
Botsome is the name of an emote in a series of "wholesome" derived emotes, in one of my servers.

## Docker deployment
The coordinator currently does not have any configuration options, this is bound to change. It also has no dependencies such as databases, this may change.

The bot companion has three envvars:
- COORDINATOR_URL: a url such as `http://coordinator` (use the service name of the coordinator)
- EMOTE: the ID of the emote you want to act on (no emoji's as of now)
- BOT_TOKEN_ENVVAR: the name of the envvar that contains the bot token. Default is `BOT_TOKEN`

Botsome is designed with this deployment in mind:
```yaml
services:
  bot:
    image: your-bot-image
    env_file: file_with_bot_token.env
    environment:
      envvars: that are of no concern to the companion
  companion:
    image: botsome-botcompanion
    env_file:
      - file_with_bot_token.env
      - file_with_coordinator_url_and_emote_id.env
    networks:
      - botsome-network

networks:
  botsome-network:
    external: true
```

In the above example, botsome-network needs to be defined externally, so you should have the botsome coordinator define it:
```yaml
services:
  coordinator:
    image: botsome-coordinator
    networks:
      - botsome-network

networks:
  botsome-network:
```
Do not expose the coordinator by publishing ports or running an ingress on the botsome-network, because the coordinator has no security measures in place whatsoever.
