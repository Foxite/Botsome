namespace Botsome; 

public class Bot {
	public string Id { get; }
	public string Token { get; }
	public string Groups { get; }

	public string[] ParsedGroups => Groups.Split(' ', StringSplitOptions.RemoveEmptyEntries);
	
	public Bot(string id, string token, string groups) {
		Id = id;
		Token = token;
		Groups = groups;
	}
}
