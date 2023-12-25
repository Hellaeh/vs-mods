using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace HelBlockPick;

public class Core : ModSystem
{
	private const string ModId = "helblockpick";
	private const string HotKey = ModId + "hotkey";

	private ICoreClientAPI cApi;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

	public override void StartClientSide(ICoreClientAPI api)
	{
		base.StartClientSide(api);

		api.Input.RegisterHotKey(HotKey, Lang.Get(ModId + ":hotkey"), GlKeys.Q, HotkeyType.CharacterControls);
		api.Input.SetHotKeyHandler(HotKey, _ => HotKeyHandler());

		api.Event.MouseDown += OnMouseDown;

		cApi = api;
	}

	private void OnMouseDown(MouseEvent e)
	{
		if (e.Button != EnumMouseButton.Middle)
			return;

		if (cApi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Survival)
			return;

		e.Handled = HotKeyHandler();
	}

	private bool HotKeyHandler()
	{
		var player = cApi.World.Player;
		var lookingAt = player.CurrentBlockSelection;

		if (lookingAt == null)
			return false;

		// WARNING: This returns wrong `Block` for some blocks (e.g. planks)
		var lookingAtItemStack = lookingAt.Block.OnPickBlock(cApi.World, lookingAt.Position);

		var res = PickBlock(player, lookingAtItemStack);

		// HACK: A temp fix for `OnPickBlock`, might have a quirky behavior
		if (!res)
			foreach (var drop in lookingAt.Block.GetDrops(cApi.World, lookingAt.Position, player))
				return PickBlock(player, drop);

		return res;
	}

	// WARNING: There might be a sneaky bug about first four slots of backpack inventory
	// are bags themself
	private bool PickBlock(IClientPlayer player, ItemStack lookFor)
	{
		var handled = false;

		player.Entity.WalkInventory(slot =>
		{
			if (slot.Empty)
				return true;

			if (!slot.Itemstack.Satisfies(lookFor))
				return true;

			var inv = slot.Inventory;

			switch (inv.ClassName)
			{
				case GlobalConstants.hotBarInvClassName:
					player.InventoryManager.ActiveHotbarSlotNumber = inv.GetSlotId(slot);
					break;

				case GlobalConstants.backpackInvClassName:
					var bestSlotIdx = GetBestSuitedHotbarSlot(player);
					player.InventoryManager.ActiveHotbarSlotNumber = bestSlotIdx;

					var packet = player.InventoryManager.GetHotbarInventory().TryFlipItems(bestSlotIdx, slot);

					if (packet != null)
						cApi.Network.SendPacketClient(packet);

					break;

				case GlobalConstants.characterInvClassName:
				case GlobalConstants.craftingInvClassName:
				case GlobalConstants.creativeInvClassName:
				case GlobalConstants.groundInvClassName:
				case GlobalConstants.mousecursorInvClassName:
					break;

				// For mod compatibility?
				default:
					var ERR_MSG = $"BlockPick Error: Unknown inventory class - \"{inv.ClassName}\". Report this error to author.";
					player.ShowChatNotification(ERR_MSG);
					cApi.Logger.Warning(ERR_MSG);
					break;
			}

			return !(handled = true);
		});

		return handled;
	}

	private static int GetBestSuitedHotbarSlot(IClientPlayer player)
	{
		// Hardcoded for now
		const int hbSlots = 10;

		var hotbarInv = player.InventoryManager.GetHotbarInventory();

		// Scan for empty slots
		for (int i = 0; i < hbSlots; ++i)
			if (hotbarInv[i].Empty)
				return i;

		// Scan for non tool slots
		for (int i = 0; i < hbSlots; ++i)
			if (hotbarInv[i].Itemstack.Item?.Tool != null)
				return i;

		// Return last slot
		return hbSlots - 1;
	}

	public override void Dispose()
	{
		cApi.Event.MouseDown -= OnMouseDown;

		base.Dispose();
	}
}

