using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Botsome;

public class BotsomeOptions {
	public List<BotsomeItem> Items { get; set; }
}

public class BotsomeItem {
	public BotsomeTrigger Trigger { get; set; }
	public BotSelection RespondUsing { get; set; }
	public List<BotsomeResponse> Responses { get; set; }
}

public class BotsomeTrigger {
	public TriggerType Type { get; set; }
	public List<ulong>? OnlyInChannels { get; set; }
	public string? MessageRegex { get; set; }

	public Regex? ActualMessageRegex => MessageRegex == null ? null : new Regex(MessageRegex, RegexOptions.IgnoreCase);
	public string? EmoteName { get; set; }
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
