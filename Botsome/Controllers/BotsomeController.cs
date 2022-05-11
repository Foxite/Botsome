using Microsoft.AspNetCore.Mvc;

namespace Botsome;

[ApiController]
[Route("[controller]")]
public class BotsomeController : ControllerBase {
	private readonly ILogger<BotsomeController> m_Logger;
	private readonly ClientService m_ClientService;

	public BotsomeController(ILogger<BotsomeController> logger, ClientService clientService) {
		m_Logger = logger;
		m_ClientService = clientService;
	}

	[HttpPatch]
	public Task Put([FromForm] string[] botStrings) {
		return m_ClientService.UpdateList(botStrings.Select(str => {
			string[] splits = str.Split(":");
			return (splits[0], splits[1]);
		}).ToArray());
	}
}
