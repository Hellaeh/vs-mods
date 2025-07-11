using System.Collections.Generic;

using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace SharedLib;

public class InventoryIterator
{
	readonly List<InventoryBase> inventories;

	public InventoryIterator(IPlayer player, params string[] classNames)
	{
		inventories = [];

		foreach (var className in classNames)
		{
			if (player.InventoryManager.GetOwnInventory(className) is not InventoryBase inv) continue;
			inventories.Add(inv);
		}
	}

	public static InventoryIterator MainInventory(IPlayer player)
	{
		var inventories = new List<InventoryBase>();
		var ignored = new HashSet<string>(
			[
				GlobalConstants.characterInvClassName,
				GlobalConstants.craftingInvClassName,
				GlobalConstants.creativeInvClassName,
				GlobalConstants.groundInvClassName,
				GlobalConstants.mousecursorInvClassName,
			]
		);

		foreach (var inv in player.InventoryManager.Inventories)
		{

		}

		// player.InventoryManager.ActiveHotbarSlot

		return new InventoryIterator(inventories);
	}

	public InventoryIterator(params InventoryBase[] inventories)
	{
		this.inventories = [.. inventories];
	}

	public InventoryIterator(List<InventoryBase> inventories)
	{
		this.inventories = inventories;
	}

}
