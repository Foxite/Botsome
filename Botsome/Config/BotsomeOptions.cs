using Newtonsoft.Json;

namespace Botsome;

public class BotsomeOptions {
	/// <summary>
	/// How long to wait for responding to TriggerType.Random triggers.
	///
	/// In RandomDelayMode.Fixed, this will be the exact delay. In Auto, this will be the initial and maximum delay.
	/// </summary>
	public double RandomDelaySeconds { get; set; }

	public RandomDelayMode RandomDelayMode { get; set; }
}