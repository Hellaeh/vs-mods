using System.Linq;

using HarmonyLib;

using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

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

		var materials = (stackInSlot.Itemstack.Attributes["materials"] as IntArrayAttribute).value.Distinct();

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

	[HarmonyPrefix]
	[HarmonyPatch(typeof(CharacterSystem), "Event_MatchesGridRecipe")]
	static bool Event_MatchesGridRecipePrefix(IPlayer player, GridRecipe recipe, ItemSlot[] ingredients, int gridWidth, ref bool __result)
	{
		// `MatchesGridRecipe` will fire for every single recipe in game, so we filter em
		if (Core.Recipe != recipe)
			return true;

		var count = 0;
		ItemSlot match = null;

		// Quick check if it's out recipe
		foreach (var slot in ingredients)
		{
			if (slot.Empty)
				continue;

			++count;

			if (slot.Itemstack.Id == Core.ChiseledBlockId)
				match = slot;
		}

		if (match == null || count > 1)
			return true;

		var materials = (match.Itemstack.Attributes["materials"] as IntArrayAttribute).value;
		var initialBlock = player.Entity.Api.World.GetBlock(materials[0]);

		// First id is initial block, which could be a variant `ew`, `ud` etc, that should not be obtained in survival
		foreach (var drop in initialBlock.Drops.Take(1))
			initialBlock = drop.ResolvedItemstack?.Block ?? initialBlock;

		recipe.Output = new()
		{
			Code = initialBlock.Code,
			ResolvedItemstack = new(initialBlock),
		};

		return !(__result = true);
	}
}
