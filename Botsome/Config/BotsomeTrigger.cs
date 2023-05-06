using System.Text.RegularExpressions;

namespace Botsome;

public class BotsomeTrigger {
	public TriggerType Type { get; set; }
	public float Probability { get; set; } = 1.0f;
	public List<ulong>? OnlyInChannels { get; set; }
	public List<ulong>? OnlyInServers { get; set; }
	public string? MessageRegex { get; set; }
	public string? EmoteNameRegex { get; set; }
	public bool CaseSensitiveRegex { get; set; } = false;
	public ulong? UserId { get; set; }

	public Regex? ActualMessageRegex => MessageRegex == null ? null : new Regex(MessageRegex, CaseSensitiveRegex ? RegexOptions.None : RegexOptions.IgnoreCase);
	public Regex? ActualEmoteNameRegex => EmoteNameRegex == null ? null : new Regex(EmoteNameRegex, CaseSensitiveRegex ? RegexOptions.None : RegexOptions.IgnoreCase);
}
