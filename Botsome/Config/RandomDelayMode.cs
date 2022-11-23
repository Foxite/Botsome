namespace Botsome;

/// <summary>
/// Determines how long to wait before selecting a bot to respond to a TriggerType.Random trigger.
/// </summary>
public enum RandomDelayMode {
	/// <summary>
	/// Always waits the specified amount of time.
	/// </summary>
	Fixed,
	
	/// <summary>
	/// Automatically adjust the delay based on the actual rate at which events are received by clients. The specified delay is the initial value, and it will not be increased beyond that value.
	/// </summary>
	Auto
}
