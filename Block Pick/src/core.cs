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

	private IClientNetworkChannel cChannel = null;

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
		api.Input.SetHotKeyHandler(HotKey, _ => HotKeyHandler(api));

		cChannel = api.Network.GetChannel(Channel);
	}

	private void ChannelHandler(IServerPlayer player, Packet packet)
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

	private bool HotKeyHandler(ICoreClientAPI api)
	{
		var player = api.World.Player;
		var lookingAt = player.CurrentBlockSelection;

		if (lookingAt == null)
			return false;

		var lookingAtId = lookingAt.Block.OnPickBlock(api.World, lookingAt.Position).Id;

		var hotbarInv = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
		int swapIdx = SearchInventory(hotbarInv, lookingAtId);

		if (swapIdx >= 0)
		{
			player.InventoryManager.ActiveHotbarSlotNumber = swapIdx;
			return true;
		}

		var backpackInv = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
		swapIdx = SearchInventory(backpackInv, lookingAtId);

		if (swapIdx >= 0)
		{
			var bestSlotIdx = GetBestSuitedHotbarSlot(player, backpackInv, backpackInv[swapIdx]);
			player.InventoryManager.ActiveHotbarSlotNumber = bestSlotIdx;

			Packet packet = new()
			{
				Payload = swapIdx
			};

			cChannel.SendPacket<Packet>(packet);

			return true;
		}

		return false;
	}

	private int SearchInventory(IInventory inv, int lookFor)
	{
		for (int i = 0; i < inv.Count; ++i)
		{
			var slot = inv[i];

			if (slot.Empty)
				continue;

			var stackId = slot.Itemstack.Id;

			if (stackId == lookFor)
				return i;
		}

		return -1;
	}

	private int GetBestSuitedHotbarSlot(IPlayer player, IInventory inv, ItemSlot slot)
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

