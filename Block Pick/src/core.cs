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
	public int SlotIdx;
	[ProtoMember(2)]
	public string InventoryId;
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

		api.Input.RegisterHotKey(HotKey, Lang.Get(ModId + ":hotkey"), GlKeys.Q, HotkeyType.HelpAndOverlays);
		api.Input.SetHotKeyHandler(HotKey, _ => HotKeyHandler(api));

		cChannel = api.Network.GetChannel(Channel);
	}

	private void ChannelHandler(IServerPlayer player, Packet packet)
	{
		var inv = player.InventoryManager;

		var currentSlot = inv.ActiveHotbarSlot;

		// WARNING: Should check for `null`? Technically it's checked on client, but with latency and whatnot
		// this might be `null`. Leaving a comment here for future.
		var swapSlot = inv.GetInventory(packet.InventoryId)[packet.SlotIdx];

		currentSlot.TryFlipWith(swapSlot);
		currentSlot.MarkDirty();
	}

	private bool HotKeyHandler(ICoreClientAPI api)
	{
		var player = api.World.Player;
		var lookingAt = player.CurrentBlockSelection;
		var currentSlot = player.InventoryManager.ActiveHotbarSlot;

		if (lookingAt == null || currentSlot == null)
			return false;

		var lookingAtId = lookingAt.Block.OnPickBlock(api.World, lookingAt.Position).Id;

		var hotbarInv = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
		int idx = SearchInventory(hotbarInv, lookingAtId);

		if (idx >= 0)
		{
			player.InventoryManager.ActiveHotbarSlotNumber = idx;
			return true;
		}

		var backpackInv = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
		idx = SearchInventory(backpackInv, lookingAtId);

		if (idx >= 0)
		{
			Packet packet = new()
			{
				InventoryId = backpackInv.InventoryID,
				SlotIdx = idx
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
}

