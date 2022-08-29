namespace Botsome;

public class BotsomeEvent : IEquatable<BotsomeEvent> {
	public ulong ChannelId { get; }
	public ulong MessageId { get; }
	public BotsomeItem Item { get; }
	public string? Data { get; }

	public BotsomeEvent(ulong channelId, ulong messageId, BotsomeItem item, string? data = null) {
		ChannelId = channelId;
		MessageId = messageId;
		Item = item;
		Data = data;
	}
	
	public bool Equals(BotsomeEvent? other) {
		if (ReferenceEquals(null, other)) return false;
		if (ReferenceEquals(this, other)) return true;
		return ChannelId == other.ChannelId && MessageId == other.MessageId;
	}

	public override bool Equals(object? obj) {
		if (ReferenceEquals(null, obj)) return false;
		if (ReferenceEquals(this, obj)) return true;
		if (obj.GetType() != this.GetType()) return false;
		return Equals((BotsomeEvent) obj);
	}

	public override int GetHashCode() {
		return HashCode.Combine(ChannelId, MessageId);
	}

	public static bool operator ==(BotsomeEvent? left, BotsomeEvent? right) {
		return Equals(left, right);
	}

	public static bool operator !=(BotsomeEvent? left, BotsomeEvent? right) {
		return !Equals(left, right);
	}
};
