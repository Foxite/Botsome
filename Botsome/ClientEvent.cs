using DSharpPlus.Entities;

namespace Botsome;

public interface IClientEvent { }

public record MessageClientEvent(
	ulong Id,
	DateTime Received,
	DiscordUser Author,
	string? Content) : IClientEvent;
