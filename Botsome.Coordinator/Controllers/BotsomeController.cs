using Microsoft.AspNetCore.Mvc;
using Shared;

namespace Botsome.Coordinator;

[ApiController]
[Route("[controller]")]
public class BotsomeController : ControllerBase {
	private readonly ILogger<BotsomeController> m_Logger;
	private readonly BotsomeService m_Service;

	public BotsomeController(ILogger<BotsomeController> logger, BotsomeService service) {
		m_Logger = logger;
		m_Service = service;
	}

	[HttpPost]
	public void Report([FromBody] BotsomeEvent evt, [FromQuery] Guid id) {
		m_Service.OnBotsome(evt, id);
	}

	[HttpGet]
	public IActionResult Stream([FromQuery] Guid id) {
		return File(m_Service.OpenStream(id), "application/json");
	}
}
