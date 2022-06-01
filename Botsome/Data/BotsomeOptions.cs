namespace Botsome;

public class BotsomeOptions {
	public string? EmoteName { get; set; }
	public string? Word { get; set; }
	public ulong[]? WordsOnlyInChannel { get; set; }
}

/*
"Botsome": [
  {
    "Trigger": {
      "Type": "Message",
      "EmoteName": "botsome"
    },
    "Response": {
      "Which": "Random",
      "Reaction": {
        "Name": "botsome"
      }
    }
  },
  {
    "Trigger": {
      "Type": "Message",
      "Word": "sus"
    },
    "Response": {
      "Which": "All",
      "Message": "sus"
    }
  }
]

public class BotsomeItem {
	public BotsomeTrigger Trigger { get; set; }
	public BotsomeResponse Response { get; set; }
}

public class BotsomeTrigger {
	public TriggerType Type { get; set; }
	public string? EmoteName { get; set; }
	public string? Word { get; set; }
}

public enum TriggerType {
	Message,
}

public class BotsomeResponse {
	public BotSelection Which { get; set; }
	public BotsomeResponseReaction? Reaction { get; set; }
	public string? Message { get; set; }
}

public enum BotSelection {
	Random, InOrder, All
}

public class BotsomeResponseReaction {
	public string? Name { get; set; }
	public ulong? Id { get; set; }
}
//*/