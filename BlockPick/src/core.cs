using ProtoBuf;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace HelBlockPick;

[ProtoContract]
public class Packet
{
	[ProtoMember(1)]
	public int Payload;
}

public class Core : ModSystem
{
	private const string ModId = "helblockpick";
	private const string HotKey = ModId + "hotkey";
	private const string Channel = ModId + "channel";

	private static IClientNetworkChannel cChannel;
	private static ICoreClientAPI cApi;

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

		var channel = api.Network.GetChannel(Channel);

		api.Input.RegisterHotKey(HotKey, Lang.Get(ModId + ":hotkey"), GlKeys.Q, HotkeyType.CharacterControls);
		api.Input.SetHotKeyHandler(HotKey, _ => HotKeyHandler());

		api.Event.MouseDown += OnMouseDown;

		cApi = api;
		cChannel = channel;
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
		currentSlot.MarkDirty();
	}

	private static void OnMouseDown(MouseEvent e)
	{
		if (e.Button != EnumMouseButton.Middle)
			return;

		if (cApi.World.Player.WorldData.CurrentGameMode != EnumGameMode.Survival)
			return;

		e.Handled = HotKeyHandler();
	}

	private static bool HotKeyHandler()
	{
		var player = cApi.World.Player;
		var lookingAt = player.CurrentBlockSelection;

		if (lookingAt == null)
			return false;

		// WARNING: This returns wrong `Block` for some blocks (e.g. planks)
		var lookingAtItemStack = lookingAt.Block.OnPickBlock(cApi.World, lookingAt.Position);

		// HACK: A temp fix for `OnPickBlock`, might have a quirky behavior
		if (!SearchAndSwap(player, lookingAtItemStack))
			foreach (var drop in lookingAt.Block.Drops)
				return SearchAndSwap(player, drop.ResolvedItemstack);

		return false;
	}

	private bool SearchAndSwap(IPlayer player, ItemStack lookFor)
	{
		var swapInv = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
		int swapIdx = SearchInventory(swapInv, lookFor);

		if (swapIdx >= 0)
		{
			player.InventoryManager.ActiveHotbarSlotNumber = swapIdx;
			return true;
		}

		swapInv = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
		swapIdx = SearchInventory(swapInv, lookFor);

		if (swapIdx >= 0)
		{
			var bestSlotIdx = GetBestSuitedHotbarSlot(player, swapInv, swapInv[swapIdx]);
			player.InventoryManager.ActiveHotbarSlotNumber = bestSlotIdx;

			Packet packet = new()
			{
				Payload = swapIdx
			};

			cChannel.SendPacket(packet);

			return true;
		}

		return false;
	}

	private static int SearchInventory(IInventory inv, ItemStack lookFor)
	{
		for (int i = 0; i < inv.Count; ++i)
		{
			var slot = inv[i];

			if (slot.Empty)
				continue;

			if (lookFor.Satisfies(slot.Itemstack))
				return i;
		}

		return -1;
	}

	private static int GetBestSuitedHotbarSlot(IPlayer player, IInventory inv, ItemSlot slot)
	{
		var bestSlot = player.InventoryManager.GetBestSuitedHotbarSlot(inv, slot);
		var bestSlotIdx = bestSlot?.Inventory?.GetSlotId(bestSlot) ?? -1;

		if (bestSlotIdx == -1)
		{
			if (player.InventoryManager.ActiveTool == null)
				return player.InventoryManager.ActiveHotbarSlotNumber;

			var hotbarInv = player.InventoryManager.GetHotbarInventory();

			for (int i = 0; i < hotbarInv.Count; ++i)
			{
				var currentSlot = hotbarInv[i];

				if (currentSlot.Empty)
					return i;

				if (currentSlot.Itemstack?.Item?.Tool == null)
					return i;
			}

			return 9;
		}

		return bestSlotIdx;
	}
}

