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

	public ICollection<ulong> IgnoredRoles { get; set; } = null!;
	public ICollection<ulong> IgnoredGuilds { get; set; } = null!;
	public ICollection<ulong> IgnoredChannels { get; set; } = null!;
}
