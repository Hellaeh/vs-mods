using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;
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

		Core.Instance.shouldUpdate = true;
	}
}

[HarmonyPatch]
public class GuiDialogInventoryPatch
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
				int backpackSlotId = Core.BagsOffset;
				var shouldRetry = false;

				for (; backpackSlotId < Core.Instance.Backpack.Count; ++backpackSlotId)
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

			var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

			for (int i = 0; packets != null && i < packets.Length; ++i)
			{
				var packet = (Packet_Client)packets[i];

				Core.Instance.Api.Network.SendPacketClient(packet);

				if (
					typeof(Packet_Client)
						.GetField("MoveItemstack", flags)
						.GetValue(packet)
						is not Packet_MoveItemstack mvPacket
				)
					continue;

				var targetInvId = (string)mvPacket.GetType().GetField("TargetInventoryId", flags).GetValue(mvPacket);
				var targetSlotId = (int)mvPacket.GetType().GetField("TargetSlot", flags).GetValue(mvPacket);
				var targetInv = player.InventoryManager.GetInventory(targetInvId);

				Core.Instance.FavoriteSlots[targetInv]?.Add(targetSlotId);
				Core.Instance.shouldUpdate = true;
			}
		}

		return true;
	}
}

[HarmonyPatch]
public class InventoryBasePatch
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

		var mouseSlot = sourceSlot;
		var targetSlot = inv[slotId];

		var mouseEmpty = mouseSlot.Empty;
		var targetEmpty = targetSlot.Empty;

		if (mouseEmpty && targetEmpty)
			return false;

		var mouseFavorite = Core.Instance.MouseFavorite;
		var targetFavorite = targetSlot.IsFavorite(slotId);

		if (mouseFavorite == targetFavorite)
			return true;

		// Allow only direct merge for favorite slots
		if (op.CurrentPriority != EnumMergePriority.DirectMerge)
			return false;

		var matchItemstack = !targetEmpty && targetSlot.Itemstack.Satisfies(mouseSlot.Itemstack);

		if (mouseFavorite) // mouse is favorite, target is not
		{
			if (mouseEmpty)
				return true;

			// left or right click will swap mouse and target
			if (!targetEmpty && !matchItemstack)
				Core.Instance.MouseFavorite = false;

			Core.Instance.FavoriteSlots[inv]?.Add(slotId);
		}
		else // target is favorite, mouse is not
		{
			if (targetEmpty)
				return true;

			if (mouseEmpty || !matchItemstack)
				Core.Instance.MouseFavorite = true;

			if (!mouseEmpty && !matchItemstack)
				Core.Instance.FavoriteSlots[inv]?.Remove(slotId);
		}

		return Core.Instance.shouldUpdate = true;
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

				return Core.Instance.shouldUpdate = true;
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
