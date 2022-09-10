using System.Text.RegularExpressions;
using DSharpPlus.Entities;
using Newtonsoft.Json;

namespace Botsome;

public class BotsomeOptions {
	public Dictionary<string, BotActivity> Status { get; set; }
	public List<BotsomeItem> Items { get; set; }
}

public class BotActivity {
	public ActivityType Type { get; set; }
	public string Message { get; set; }
}

public class BotsomeItem {
	public BotsomeTrigger Trigger { get; set; }
	public BotSelection RespondMode { get; set; }
	public string RespondGroup { get; set; }
	public List<BotsomeResponse> Responses { get; set; }
}

public class BotsomeTrigger {
	public TriggerType Type { get; set; }
	public List<ulong>? OnlyInChannels { get; set; }
	public string? MessageRegex { get; set; }
	public string? EmoteNameRegex { get; set; }

	public Regex? ActualMessageRegex => MessageRegex == null ? null : new Regex(MessageRegex, RegexOptions.IgnoreCase);
	public Regex? ActualEmoteNameRegex => EmoteNameRegex == null ? null : new Regex(EmoteNameRegex, RegexOptions.IgnoreCase);
}

public enum TriggerType {
	MessageContent,
	EmoteNameAsMessage,
}

public class BotsomeResponse {
	public ResponseType Type { get; set; }
	public string Response { get; set; }
}

public enum BotSelection {
	Random,
	//RoundRobin,
	All
}

public enum ResponseType {
	EmojiAsReaction,
	EmoteNameAsReaction,
	EmoteNameAsMessage,
	Message,
}
