namespace Botsome; 

public class Bot {
	public string Id { get; set; }
	public string Token { get; set; }
	public string Groups { get; set; }

	public string[] ParsedGroups => Groups.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
