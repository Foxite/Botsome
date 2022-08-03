using System.Text.RegularExpressions;

namespace Botsome;

public class BotsomeTrigger {
	public TriggerType Type { get; set; }
	public List<ulong>? OnlyInChannels { get; set; }
	public string? MessageRegex { get; set; }
	public string? EmoteNameRegex { get; set; }

	public Regex? ActualMessageRegex => MessageRegex == null ? null : new Regex(MessageRegex, RegexOptions.IgnoreCase);
	public Regex? ActualEmoteNameRegex => EmoteNameRegex == null ? null : new Regex(EmoteNameRegex, RegexOptions.IgnoreCase);
}
