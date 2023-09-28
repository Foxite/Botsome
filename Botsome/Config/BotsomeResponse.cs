namespace Botsome;

public class BotsomeResponse {
	public ResponseType Type { get; set; }
	public string Response { get; set; } = null!;
	public bool IsReply { get; set; } = false;
}
