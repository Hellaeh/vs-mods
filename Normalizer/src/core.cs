using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace BlockNormalizer;

public class Core : ModSystem
{
	private int ChiseledBlockId;
	private ICoreServerAPI sApi;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

	public override void StartServerSide(ICoreServerAPI api)
	{
		base.StartServerSide(api);

		const string CB = "chiseledblock";
		var block = Array.Find(api.World.SearchBlocks(new(CB)), block => block.Code.GetName() == CB);

		if (block == null)
			return;

		api.Event.MatchesGridRecipe += OnGridRecipeMatch;

		ChiseledBlockId = block.Id;
		sApi = api;
	}

	private bool OnGridRecipeMatch(IPlayer player, GridRecipe recipe, ItemSlot[] inputSlots, int gridWidth)
	{
		var slotCount = 0;
		ItemSlot match = null;

		foreach (var slot in inputSlots)
		{
			if (slot.Empty)
				continue;

			++slotCount;

			if (slot.Itemstack.Id == ChiseledBlockId)
				match = slot;
		}

		if (match == null || slotCount > 1)
			return true;

		var materials = (match.Itemstack.Attributes["materials"] as IntArrayAttribute)?.value;

		if (materials == null)
			return true;

		var outputCount = match.Itemstack.StackSize;

		// First id is initial block, which could be a variant `ew`, `ud` etc, that should not be obtained in survival
		var first = materials[0];
		var block = sApi.World.GetBlock(first);
		foreach (var drop in block.Drops)
		{
			var stack =
				drop.ResolvedItemstack.Block == null
					? new(block)
					: drop.ResolvedItemstack;

			stack.StackSize = outputCount;

			if (!player.InventoryManager.TryGiveItemstack(stack))
				sApi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);

			break;
		}

		foreach (var id in materials.Skip(1))
		{
			var stack = new ItemStack(sApi.World.GetBlock(id), outputCount);

			if (!player.InventoryManager.TryGiveItemstack(stack))
				sApi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
		}

		match.TakeOutWhole();

		return false;
	}
}

