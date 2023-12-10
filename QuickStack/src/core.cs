using System;
using System.Collections.Generic;
using System.Linq;

using ProtoBuf;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace HelQuickStack;

[ProtoContract]
class Packet
{
	[ProtoMember(1)]
	public Dictionary<BlockPos, List<int>> Payload;
}

public class Core : ModSystem
{
	private const string ModId = "helquickstack";
	private const string HotKey = ModId + "hotkey";
	private const string Channel = ModId + "channel";
	private const string ConfigFile = ModId + "config.json";

	private const string FavoriteColor = "#FFD000";

	// Offset to ignore slots for bags
	private const int BAGS_OFFSET = 4;
	// Inventory row length in GUI window
	private const int ROWLEN = 6;

	private const int MAX_RADIUS = 200;
	private static int RADIUS;

	private ICoreClientAPI cApi;
	private IClientNetworkChannel cChannel;

	private GuiDialog backpackGui;
	private IInventory backpackInv => cApi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
	private IntArrayAttribute favSlots => (cApi.World.Player.Entity.Attributes[ModId] ??= new IntArrayAttribute(Array.Empty<int>())) as IntArrayAttribute;

	public override void Start(ICoreAPI api)
	{
		base.Start(api);

		api.Network.RegisterChannel(Channel)
			.RegisterMessageType<Packet>();
	}

	public override void StartServerSide(ICoreServerAPI api)
	{
		base.StartServerSide(api);

		// FIXME: Create and read server config
		// Also sync between server and client
		var config = 10;

		RADIUS = Math.Min(MAX_RADIUS, config);

		api.Network.GetChannel(Channel)
			.SetMessageHandler<Packet>(ChannelHandler);
	}

	public override void StartClientSide(ICoreClientAPI api)
	{
		base.StartClientSide(api);

		api.Input.RegisterHotKey(HotKey, Lang.Get(ModId + ":hotkey"), GlKeys.X, HotkeyType.InventoryHotkeys);
		api.Input.SetHotKeyHandler(HotKey, _ => HotKeyHandler());

		api.Event.MouseDown += OnMouseDown;
		api.Event.PlayerJoin += OnPlayerJoin;
		api.Event.LeaveWorld += () => cApi.StoreModConfig(new Config { FavoriteSlots = favSlots.value }, ConfigFile);

		// FIXME: We need this later as a workaround to avoid crashing a client 
		backpackGui = api.Gui.LoadedGuis.Find(gui => gui.ToString().Contains("GuiDialogInventory"));

		cApi = api;
		cChannel = api.Network.GetChannel(Channel);
	}

	private void OnPlayerJoin(IClientPlayer player)
	{
		favSlots.value = cApi.LoadModConfig<Config>(ConfigFile)?.FavoriteSlots ?? favSlots.value;

		backpackInv.SlotModified += OnBackpackSlotModified;
		UpdateFavorite();
	}

	private void OnBackpackSlotModified(int slotId)
	{
		if (slotId >= BAGS_OFFSET)
			return;

		// FIXME: Ugly workaround to avoid crashing the client
		if (backpackGui.TryClose())
			UpdateFavorite();
	}

	private void OnMouseDown(MouseEvent e)
	{
		if (e.Button is not (EnumMouseButton.Left or EnumMouseButton.Right))
			return;

		if (!cApi.Input.KeyboardKeyStateRaw[(int)GlKeys.AltLeft])
			return;

		var slot = cApi.World.Player.InventoryManager.CurrentHoveredSlot;

		if (slot == null || slot.Inventory.ClassName != GlobalConstants.backpackInvClassName)
			return;

		var slotId = slot.Inventory.GetSlotId(slot);

		if (slotId < BAGS_OFFSET)
			return;

		if (favSlots.value.Contains(slotId)) favSlots.RemoveInt(slotId);
		else favSlots.AddInt(slotId);

		e.Handled = true;

		UpdateFavorite();
	}

	private void UpdateFavorite()
	{
		foreach (var slot in backpackInv.Skip(BAGS_OFFSET))
			// To ensure others mods keep their colors
			if (slot.HexBackgroundColor == FavoriteColor)
				slot.HexBackgroundColor = null;

		// FIXME: Remove once you can dynamically color slot without a crash
		// HACK: Ensure first row is always favorite, if no favorite slots were assigned by user. 
		// At least one slot needs to be colored to avoid crashing a client. 
		if (favSlots.value.Length == 0)
			for (int i = BAGS_OFFSET; i < BAGS_OFFSET + ROWLEN && backpackInv[i] != null; ++i)
				backpackInv[i].HexBackgroundColor = FavoriteColor;

		foreach (var id in favSlots.value)
			if (backpackInv[id] != null)
				backpackInv[id].HexBackgroundColor = FavoriteColor;
	}

	private bool HotKeyHandler()
	{
		Dictionary<int, Stack<int>> sourceSlotIds = new();
		// Packet payload
		Dictionary<BlockPos, List<int>> payload = new();

		for (int slotId = BAGS_OFFSET; slotId < backpackInv.Count; ++slotId)
		{
			var slot = backpackInv[slotId];

			if (slot.Empty)
				continue;

			// NOTE: Should probably be changed for better mod compatibility
			if (slot.HexBackgroundColor == FavoriteColor)
				continue;

			var id = slot.Itemstack.Id;

			if (!sourceSlotIds.TryGetValue(id, out var slots))
				sourceSlotIds.Add(id, slots = new());

			slots.Push(slotId);
		}

		// Check if nothing to stack
		if (sourceSlotIds.Count == 0)
			return true;

		var itemIds = new List<int>();

		// FIXME: Get view distance
		var viewDistance = RADIUS;
		var radius = Math.Min(RADIUS, viewDistance);

		Utils.WalkNearbyContainers(cApi.World.Player, RADIUS, container =>
		{
			var inv = container.Inventory;
			var pos = container.Pos;

			if (inv.PutLocked)
				return true;

			// Filter full inventories
			if (!inv.Any(slot => slot.Empty))
				return true;

			foreach (var slot in inv)
			{
				if (slot.Empty)
					continue;

				var itemId = slot.Itemstack.Id;

				if (sourceSlotIds.ContainsKey(itemId))
					itemIds.Add(itemId);
			}

			foreach (var itemId in itemIds)
			{
				var sourceSlots = sourceSlotIds[itemId];

				foreach (var destSlot in inv)
				{
					if (!sourceSlots.TryPop(out var id))
						break;

					var sourceSlot = backpackInv[id];

					if (destSlot.Empty || (destSlot.Itemstack.Collectible.MaxStackSize - destSlot.Itemstack.StackSize - sourceSlot.Itemstack.StackSize) > 0)
					{
						if (!payload.TryGetValue(pos, out var ids))
							payload.Add(pos, ids = new());

						ids.Add(id);
					}

					else
						sourceSlots.Push(id);
				}
			}

			itemIds.Clear();

			return false;
		});


		if (payload.Count == 0)
			return true;

		cChannel.SendPacket(new Packet()
		{
			Payload = payload
		});

		return true;
	}

	private static void ChannelHandler(IServerPlayer player, Packet packet)
	{
		var sourceInv = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
		var world = player.Entity.World;

		foreach ((var pos, var ids) in packet.Payload)
		{
			if (player.Entity.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityContainer container)
				return;

			var inv = container.Inventory;
			var id = ids.Count;

			foreach (var destSlot in inv)
			{
				if (--id < 0)
					break;

				player.Entity.Api.Logger.Notification($"{ids[id]}");

				var sourceSlot = sourceInv[ids[id]];

				if (destSlot.Empty || (destSlot.Itemstack.Collectible.MaxStackSize - destSlot.Itemstack.StackSize - sourceSlot.Itemstack.StackSize) > 0)
				{
					if (sourceSlot.TryPutInto(world, destSlot, sourceSlot.Itemstack.StackSize) == 0)
						return;
				}
				else
					++id;
			}
		}
	}
}
