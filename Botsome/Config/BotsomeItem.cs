namespace Botsome;

public class BotsomeItem {
	public bool Enabled { get; set; } = true;
	public BotsomeTrigger Trigger { get; set; } = null!;
	public BotSelection RespondMode { get; set; }
	public string RespondGroup { get; set; } = null!;
	public ResponseSelection ResponseSelection { get; set; } = ResponseSelection.All;
	public List<BotsomeResponse> Responses { get; set; } = null!;
}
