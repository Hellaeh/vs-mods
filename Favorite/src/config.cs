using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace HelFavorite;

public class Helper
{
	public static T LoadConfig<T>(ICoreAPI api, string filename)
	where T : new()
	{
		T config;

		try
		{
			config = api.LoadModConfig<T>(filename) ?? new();
		}
		catch
		{
			config = new();
		}

		return config;
	}
}

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
	public string FavoriteColor { get; set; } = "#FFD000";
}
