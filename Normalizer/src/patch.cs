using System.Linq;

using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace HelNormalizer;

[HarmonyPatch]
public class Patch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(CollectibleObject), nameof(CollectibleObject.OnConsumedByCrafting))]
	static bool OnConsumedByCraftingPrefix(ItemSlot stackInSlot, GridRecipe gridRecipe, IPlayer byPlayer, int quantity)
	{
		if (Core.Recipe != gridRecipe)
			return true;

		var materials = (stackInSlot.Itemstack.Attributes["materials"] as IntArrayAttribute).value;

		// Skip initial block, we already handled it
		foreach (var id in materials.Skip(1))
		{
			var block = byPlayer.Entity.World.GetBlock(id);
			var stack = new ItemStack(block, quantity);

			if (!byPlayer.InventoryManager.TryGiveItemstack(stack, true))
				byPlayer.Entity.World.SpawnItemEntity(stack, byPlayer.Entity.Pos.XYZ);
		}

		stackInSlot.Itemstack.StackSize -= quantity;

		if (stackInSlot.Itemstack.StackSize <= 0)
		{
			stackInSlot.Itemstack = null;
			stackInSlot.MarkDirty();
		}

		return false;
	}
}
