using ProtoBuf;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace HelBlockPick;

[ProtoContract]
class Packet
{
	[ProtoMember(1)]
	public int Payload;
}

public class Core : ModSystem
{
	private const string ModId = "helblockpick";
	private const string HotKey = ModId + "hotkey";
	private const string Channel = ModId + "channel";

	private ICoreClientAPI cApi;
	private IClientNetworkChannel cChannel;

	public override void Start(ICoreAPI api)
	{
		base.Start(api);

		api.Network.RegisterChannel(Channel)
			.RegisterMessageType<Packet>();
	}

	public override void StartServerSide(ICoreServerAPI api)
	{
		base.StartServerSide(api);

		api.Network.GetChannel(Channel)
			.SetMessageHandler<Packet>(ChannelHandler);
	}

	public override void StartClientSide(ICoreClientAPI api)
	{
		base.StartClientSide(api);

		api.Input.RegisterHotKey(HotKey, Lang.Get(ModId + ":hotkey"), GlKeys.Q, HotkeyType.CharacterControls);
		api.Input.SetHotKeyHandler(HotKey, _ => HotKeyHandler());

		api.Event.MouseDown += OnMouseDown;

		cApi = api;
		cChannel = api.Network.GetChannel(Channel);
	}

	private static void ChannelHandler(IServerPlayer player, Packet packet)
	{
		var inv = player.InventoryManager;
		var currentSlot = inv.ActiveHotbarSlot;

		// WARNING: Should check for `null`? Technically it's checked on client, but with latency and whatnot
		// this might be `null`. Leaving a comment here for future.
		var swapInv = inv.GetOwnInventory(GlobalConstants.backpackInvClassName);
		var swapSlot = swapInv[packet.Payload];

		currentSlot.TryFlipWith(swapSlot);
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
			var slotIdx = inv.GetSlotId(slot);

			switch (inv.ClassName)
			{
				case GlobalConstants.hotBarInvClassName:
					player.InventoryManager.ActiveHotbarSlotNumber = slotIdx;
					break;

				case GlobalConstants.backpackInvClassName:
					var bestSlotIdx = GetBestSuitedHotbarSlot(player, inv, slot);
					player.InventoryManager.ActiveHotbarSlotNumber = bestSlotIdx;

					Packet packet = new()
					{
						Payload = slotIdx
					};

					cChannel.SendPacket(packet);
					break;

				default:
					var ERR_MSG = $"BlockPick Error: Unknown inventory class - \"{inv.ClassName}\". Report this error to author.";
					player.ShowChatNotification(ERR_MSG);
					cApi.Logger.Error(ERR_MSG);
					break;
			}

			return !(handled = true);
		});

		return handled;
	}

	private static int GetBestSuitedHotbarSlot(IClientPlayer player, IInventory inv, ItemSlot slot)
	{
		var bestSlot = player.InventoryManager.GetBestSuitedHotbarSlot(inv, slot);
		var bestSlotIdx = bestSlot?.Inventory?.GetSlotId(bestSlot) ?? -1;

		if (bestSlotIdx != -1)
			return bestSlotIdx;

		// hardcoded for now
		const int hbSlots = 10;

		if (player.InventoryManager.ActiveTool == null)
			return player.InventoryManager.ActiveHotbarSlotNumber;

		var hotbarInv = player.InventoryManager.GetHotbarInventory();

		for (int i = 0; i < hbSlots; ++i)
		{
			var currentSlot = hotbarInv[i];

			if (currentSlot.Empty)
				return i;

			if (currentSlot.Itemstack?.Item?.Tool == null)
				return i;
		}

		return hbSlots - 1;
	}
}

