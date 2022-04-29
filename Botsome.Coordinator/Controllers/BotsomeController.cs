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
	public void Report([FromBody] BotsomeEvent evt) {
		m_Service.OnBotsome(evt);
	}

	[HttpGet]
	public IAsyncEnumerable<BotsomeEvent> Stream() {
		return m_Service.OpenStream();
	}
}
