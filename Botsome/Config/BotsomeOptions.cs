using Newtonsoft.Json;

namespace Botsome;

public class BotsomeOptions {
	/// <summary>
	/// How long to wait for responding to TriggerType.Random triggers.
	///
	/// In RandomDelayMode.Fixed, this will be the exact delay. In Auto, this will be the initial and maximum delay.
	/// </summary>
	public double RandomResponseTimeSeconds { get; set; }
	public TimeSpan RandomResponseTime => TimeSpan.FromSeconds(RandomResponseTimeSeconds);

	public RandomDelayMode RandomResponseTimeMode { get; set; }
}
