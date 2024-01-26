using System.Runtime.CompilerServices;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;

namespace HelFavorite;

[HarmonyPatch]
class InventoryManagerPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(PlayerInventoryManager), nameof(PlayerInventoryManager.DropMouseSlotItems))]
	// Prevent favorite items from dropping from cursor
	static bool DropMouseSlotItemsPrefix() => !Core.Instance?.MouseFavorite ?? true;
}

[HarmonyPatch]
class GuiElementItemSlotGridBasePatch
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(GuiElementItemSlotGridBase), "RedistributeStacks")]
	// This will distribute favorite slots when holding mouse left button
	static void RedistributeStacksPostfix(GuiElementItemSlotGridBase __instance, int intoSlotId)
	{
		if (!Core.Instance.MouseFavorite)
			return;

		var inv = Traverse.Create(__instance).Field("inventory").GetValue<IInventory>();

		inv[intoSlotId].TryMarkAsFavorite(intoSlotId);
	}
}

[HarmonyPatch]
class GuiDialogInventoryPatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(GuiDialogInventory), nameof(GuiDialogInventory.OnGuiClosed))]
	// Move items from crafting grid back to inventory while keep track of favorite items
	static bool OnGuiClosedPrefix()
	{
		if (Core.Instance?.CraftingGrid == null)
			return true;

		var player = Core.Instance.Api.World.Player;

		if (player.WorldData.CurrentGameMode != EnumGameMode.Survival)
			return true;

		for (int slotId = 0; slotId < Core.Instance.CraftingGrid.Count; ++slotId)
		{
			var slot = Core.Instance.CraftingGrid[slotId];

			if (slot.Empty || !slot.IsFavorite(slotId))
				continue;

			var op = new ItemStackMoveOperation(Core.Instance.Api.World, EnumMouseButton.Button1, 0, EnumMergePriority.AutoMerge, slot.StackSize)
			{
				ActingPlayer = player
			};

			object[] packets = player.InventoryManager.TryTransferAway(slot, ref op, true, false);

			if (packets == null)
			{
				var shouldRetry = false;

				for (int backpackSlotId = Core.BagsOffset; backpackSlotId < Core.Instance.Backpack.Count; ++backpackSlotId)
				{
					var backpackSlot = Core.Instance.Backpack[backpackSlotId];

					if (backpackSlot.IsFavorite(backpackSlotId))
						continue;

					player.InventoryManager.DropItem(backpackSlot, true);

					shouldRetry = true;

					break;
				}

				if (shouldRetry)
					packets = player.InventoryManager.TryTransferAway(slot, ref op, true, false);
			}

			for (int i = 0; packets != null && i < packets.Length; ++i)
			{
				var packet = (Packet_Client)packets[i];

				Core.Instance.Api.Network.SendPacketClient(packet);

				if (packet.MoveItemstack is not Packet_MoveItemstack mvPacket)
					continue;

				var targetInvId = mvPacket.TargetInventoryId;
				var targetSlotId = mvPacket.TargetSlot;
				var targetInv = player.InventoryManager.GetInventory(targetInvId);

				targetInv[targetSlotId].TryMarkAsFavorite();
			}
		}

		return true;
	}
}

[HarmonyPatch]
class InventoryBasePatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.ActivateSlot))]
	static bool ActivateSlotPrefix(InventoryBase __instance, int slotId, ItemSlot sourceSlot, ref ItemStackMoveOperation op)
	{
		if (op.MouseButton is not (EnumMouseButton.Left or EnumMouseButton.Right))
			return true;

		var inv = __instance;

		if (inv.Api.Side == EnumAppSide.Server)
			return true;

		var mouseSlot = Core.Instance.Mouse[0];
		var targetSlot = inv[slotId];

		var mouseEmpty = mouseSlot.Empty;
		var targetEmpty = targetSlot.Empty;

		if (mouseEmpty && targetEmpty)
			return false;

		var mouseFavorite = mouseSlot.IsFavorite(0);
		var targetFavorite = targetSlot.IsFavorite(slotId);

		var isDirectMerge = op.CurrentPriority == EnumMergePriority.DirectMerge;

		if (mouseFavorite == targetFavorite)
			return !mouseFavorite || isDirectMerge;

		// Allow only direct merge for favorite slots
		if (!isDirectMerge)
			return false;

		var matchItemstack = !targetEmpty && targetSlot.Itemstack.Satisfies(mouseSlot.Itemstack);

		if (mouseFavorite) // mouse is favorite, target is not
		{
			if (mouseEmpty)
				return true;

			// left or right click will swap mouse and target
			if (!targetEmpty && !matchItemstack)
				mouseSlot.TryUnmarkAsFavorite(0);

			targetSlot.TryMarkAsFavorite(slotId);
		}
		else // target is favorite, mouse is not
		{
			if (targetEmpty)
				return true;

			if (mouseEmpty || !matchItemstack)
				mouseSlot.TryMarkAsFavorite(0);

			if (!mouseEmpty && !matchItemstack)
				targetSlot.TryUnmarkAsFavorite(slotId);
		}

		return true;
	}

	[HarmonyPrefix]
	[HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.TryFlipItems))]
	static bool TryFlipItemsPrefix(InventoryBase __instance, int targetSlotId, ItemSlot itemSlot, out (ItemSlot, ItemSlot)? __state)
	{
		var inv = __instance;
		__state = null;

		if (inv.Api.Side == EnumAppSide.Server)
			return true;

		var sourceSlot = itemSlot;
		var destSlot = inv[targetSlotId];

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static (ItemSlot, ItemSlot)? SwapOrBlock(ItemSlot favSlot, ItemSlot notFavSlot) =>
			Core.Instance.FavoriteSlots[notFavSlot.Inventory] == null ? null : (favSlot, notFavSlot);

		return (sourceSlot.IsFavorite(), destSlot.IsFavorite(targetSlotId)) switch
		{
			(true, true) or (false, false) => true,
			(true, _) => (__state = SwapOrBlock(sourceSlot, destSlot)) != null,
			(_, true) => (__state = SwapOrBlock(destSlot, sourceSlot)) != null
		};
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(InventoryBase), nameof(InventoryBase.TryFlipItems))]
	static void TryFlipItemsPostfix(object __result, (ItemSlot, ItemSlot)? __state)
	{
		if (__state == null || __result == null)
			return;

		__state?.Item1.TryUnmarkAsFavorite();
		__state?.Item2.TryMarkAsFavorite();
	}
}
