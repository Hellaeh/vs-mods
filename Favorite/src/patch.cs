using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Common;

namespace HelFavorite;

[HarmonyPatch]
public class InventoryManagerPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerInventoryManager), nameof(PlayerInventoryManager.DropMouseSlotItems))]
	// Prevent favorite items dropping from cursor
	static bool DropMouseSlotItemsPrefix() => !Core.Instance?.MouseFavorite ?? true;
}

[HarmonyPatch]
public class GuiElementItemSlotGridBasePatch
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(GuiElementItemSlotGridBase), "RedistributeStacks")]
	// This will propagate favorite slots when holding mouse left button
	internal static void RedistributeStacksPostfix(GuiElementItemSlotGridBase __instance, int intoSlotId)
	{
		if (!Core.Instance.MouseFavorite)
			return;

		var inv = Traverse.Create(__instance).Field("inventory").GetValue<IInventory>();

		Core.Instance.FavoriteSlots[inv]?.Add(intoSlotId);
	}
}

[HarmonyPatch]
public class InventoryBasePatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.ActivateSlot))]
	// 1. Prevent `shift + click` on favorite slot
	// 2. Track favorite item across player inventory
	static bool ActivateSlotPrefix(InventoryBase __instance, int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
	{
		if (op.MouseButton is not (EnumMouseButton.Left or EnumMouseButton.Right))
			return true;

		var inv = __instance;
		var mouseSlot = sourceSlot;

		if (inv.Api.Side == EnumAppSide.Server)
			return true;

		var fav = Core.Instance;
		var favSlots = fav.FavoriteSlots[inv];

		if (!fav.MouseFavorite && (favSlots == null || !favSlots.Contains(slotId)))
			return true;

		// Allow only direct merge for favorite slots
		if (op.CurrentPriority != EnumMergePriority.DirectMerge)
			return false;

		sourceSlot = inv[slotId];

		// FIXME: Improve and simplify logic
		if (mouseSlot.Empty && !sourceSlot.Empty)
			return fav.MouseFavorite = true;

		if (fav.MouseFavorite)
		{
			if (sourceSlot.IsFavorite(slotId))
				return true;

			if (!sourceSlot.Empty && sourceSlot.Itemstack != mouseSlot.Itemstack)
				fav.MouseFavorite = false;

			favSlots?.Add(slotId);
		}

		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.TryFlipItems))]
	static bool TryFlipItemsPrefix(InventoryBase __instance, int targetSlotId, ItemSlot itemSlot)
	{
		var inv = __instance;

		if (inv.Api.Side == EnumAppSide.Server)
			return true;

		var sourceSlot = itemSlot;
		var destSlot = inv[targetSlotId];

		static bool TrackSwap(ItemSlot favSlot, ItemSlot notFavSlot)
		{
			var destInv = notFavSlot.Inventory;

			if (Core.Instance.FavoriteSlots[destInv] != null)
			{
				favSlot.TryRemoveMarkedAsFavorite();

				Core.Instance.FavoriteSlots[destInv].Add(destInv.GetSlotId(notFavSlot));

				return true;
			}

			return false;
		}

#pragma warning disable // False positive for IDE0072
		return (sourceSlot.IsFavorite(), destSlot.IsFavorite(targetSlotId)) switch
#pragma warning restore
		{
			(true, true) or (false, false) => true,
			(true, _) => TrackSwap(sourceSlot, destSlot),
			(_, true) => TrackSwap(destSlot, sourceSlot),
		};
	}
}
