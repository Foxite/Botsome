using Botsome.Util;
using DSharpPlus.EventArgs;

namespace Botsome; 

/// <summary>
/// Responsible for receiving events from <see cref="BotsomeClient"/>s and activating <see cref="ResponseService"/>.
/// It is also responsible for tweaking the RandomDelay.
/// </summary>
public class ClientEventService {
	private readonly ItemsService m_ItemsService;
	
	private readonly ExpiringDictionary<>

	public ClientEventService(ItemsService itemsService) {
		m_ItemsService = itemsService;
	}

	public void OnMessageCreated(BotsomeClient client, MessageCreateEventArgs eventArgs) {
		
	}
}
