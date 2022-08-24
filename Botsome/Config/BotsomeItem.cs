namespace Botsome;

public class BotsomeItem {
	public BotsomeTrigger Trigger { get; set; }
	public BotSelection RespondMode { get; set; }
	public string RespondGroup { get; set; }
	public List<BotsomeResponse> Responses { get; set; }
}
