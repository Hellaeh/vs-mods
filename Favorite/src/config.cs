using System.Collections.Generic;

using Vintagestory.API.Config;

namespace HelFavorite;

public class FavoriteSlotsConfig
{
	public Dictionary<string, HashSet<int>> SlotsByInventory { get; set; } = new()
	{
		[GlobalConstants.backpackInvClassName] = [],
		[GlobalConstants.hotBarInvClassName] = [],
		[GlobalConstants.mousecursorInvClassName] = [],
		[GlobalConstants.craftingInvClassName] = []
	};
}

public class ClientConfig
{
	/// <summary>
	/// Color currenly in use to mark item as favorite.
	/// Read from config
	/// </summary>
	public string FavoriteColor { get; set; } = "#FFD000";
}
