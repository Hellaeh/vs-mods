using System;
using System.Collections.Generic;
using System.Linq;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace HelQuickStack;

public class Core : ModSystem
{
	private const string ModId = "helquickstack";
	private const string HotKey = ModId + "hotkey";
	private const string Channel = ModId + "channel";
	private const string ClientConfigFile = ModId + "ConfigClient.json";
	private const string ServerConfigFile = ModId + "ConfigServer.json";

	private const string FavoriteColor = "#FFD000";

	// Offset to ignore slots for bags
	private const int BAGS_OFFSET = 4;
	// Inventory row length in GUI window
	private const int ROWLEN = 6;

	private const int MaxRadius = 256;
	private static int Radius;

	private ICoreClientAPI cApi;
	private IClientNetworkChannel cChannel;

	private GuiDialog backpackGui;
	private IInventory backpackInv => cApi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
	private IntArrayAttribute favSlots => (cApi.World.Player.Entity.Attributes[ModId] ??= new IntArrayAttribute(Array.Empty<int>())) as IntArrayAttribute;

	public override void Start(ICoreAPI api)
	{
		base.Start(api);

		api.Network.RegisterChannel(Channel)
			.RegisterMessageType<StackPacket>()
			.RegisterMessageType<RadiusPacket>();
	}

	public override void StartServerSide(ICoreServerAPI api)
	{
		base.StartServerSide(api);

		int radius;
		try
		{
			radius = Math.Min(api.LoadModConfig<ServerConfig>(ServerConfigFile).Radius, MaxRadius);
		}
		catch
		{
			radius = ServerConfig.DefaultRadius;
			api.StoreModConfig(new ServerConfig(), ServerConfigFile);
		}

		var maxViewDistance = api.Server.Config.MaxChunkRadius << Utils.ChunkShift;

		var ch = api.Network.GetChannel(Channel)
			.SetMessageHandler<StackPacket>(ChannelHandler);

		api.Event.PlayerJoin += player => ch.SendPacket(new RadiusPacket() { Payload = Math.Min(radius, maxViewDistance) }, player);
	}

	public override void StartClientSide(ICoreClientAPI api)
	{
		base.StartClientSide(api);

		api.Input.RegisterHotKey(HotKey, Lang.Get(ModId + ":hotkey"), GlKeys.X, HotkeyType.InventoryHotkeys);
		api.Input.SetHotKeyHandler(HotKey, _ => HotKeyHandler());

		api.Event.MouseDown += OnMouseDown;
		api.Event.PlayerJoin += OnPlayerJoin;
		api.Event.LeaveWorld += () => cApi.StoreModConfig(new ClientConfig { FavoriteSlots = favSlots.value }, ClientConfigFile);

		// FIXME: We need this later as a workaround to avoid crashing a client 
		backpackGui = api.Gui.LoadedGuis.Find(gui => gui.ToString().Contains("GuiDialogInventory"));

		cApi = api;
		cChannel = api.Network.GetChannel(Channel)
			.SetMessageHandler<RadiusPacket>(packet => Radius = Math.Min(packet.Payload, MaxRadius));
	}

	private void OnPlayerJoin(IPlayer player)
	{
		favSlots.value = cApi.LoadModConfig<ClientConfig>(ClientConfigFile)?.FavoriteSlots ?? favSlots.value;

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
		if (favSlots.value.Length == 0 || !favSlots.value.Any(v => v < backpackInv.Count))
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

		// Temp buf vars
		var stackableItemIds = new List<int>();
		var emptySlots = new List<int>();
		var nonEmptySlots = new List<int>();

		bool ClearBufsAndReturn(bool ret)
		{
			stackableItemIds.Clear();
			emptySlots.Clear();
			nonEmptySlots.Clear();

			return ret;
		}

		Utils.WalkNearbyContainers(cApi.World.Player, Radius, container =>
		{
			var destInv = container.Inventory;
			var destPos = container.Pos;

			if (destInv.PutLocked)
				return true;

			for (var i = 0; i < destInv.Count; ++i)
			{
				var slot = destInv[i];

				if (slot.Empty)
					emptySlots.Add(i);
				else
					nonEmptySlots.Add(i);
			}

			// Filter not suited containers
			if (nonEmptySlots.Count == 0)
				return ClearBufsAndReturn(true);

			foreach (var slotId in nonEmptySlots)
			{
				var itemId = destInv[slotId].Itemstack.Id;

				if (sourceSlotIds.ContainsKey(itemId))
					stackableItemIds.Add(itemId);
			}

			foreach (var itemId in stackableItemIds)
			{
				if (!sourceSlotIds.TryGetValue(itemId, out var sourceSlots))
					continue;

				// Prioritize already filled slots
				foreach (var slotId in nonEmptySlots)
				{
					if (!sourceSlots.TryPop(out var id))
					{
						sourceSlotIds.Remove(itemId);
						// continue outer loop
						goto Continue;
					}

					var destSlot = destInv[slotId];
					var sourceSlot = backpackInv[id];

					var merged = destSlot.Itemstack.Collectible.GetMergableQuantity(destSlot.Itemstack, sourceSlot.Itemstack, EnumMergePriority.AutoMerge);

					if (merged > 0)
					{
						if (!payload.TryGetValue(destPos, out var ids))
							payload.Add(destPos, ids = new());

						ids.Add(id);
					}

					if (merged < sourceSlot.StackSize)
						sourceSlots.Push(id);
				}

				// Add the rest of source slots
				foreach (var _ in emptySlots)
				{
					if (!sourceSlots.TryPop(out var id))
					{
						sourceSlotIds.Remove(itemId);
						// continue outer loop
						goto Continue;
					}

					if (!payload.TryGetValue(destPos, out var ids))
						payload.Add(destPos, ids = new());

					ids.Add(id);
				}

			Continue:;
			}

			return ClearBufsAndReturn(sourceSlotIds.Count > 0);
		});


		if (payload.Count == 0)
			return true;

		cChannel.SendPacket(new StackPacket()
		{
			Payload = payload
		});

		return true;
	}

	private static void ChannelHandler(IServerPlayer player, StackPacket packet)
	{
		var sourceInv = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
		var world = player.Entity.World;

		foreach ((var pos, var ids) in packet.Payload)
		{
			// Welp something terrible happened
			if (player.Entity.World.BlockAccessor.GetBlockEntity(pos) is not BlockEntityContainer container)
				return;

			var destInv = container.Inventory;
			int i = 0, slot = 0;

			while (i < ids.Count && slot < destInv.Count)
			{
				var id = ids[i];
				var destSlot = destInv[slot];

				var sourceSlot = sourceInv[id];
				var stackSize = sourceSlot.StackSize;

				var res = sourceSlot.TryPutInto(world, destSlot, stackSize);

				if (res == 0)
					++slot;
				else if (res == stackSize)
					++i;
			}
		}
	}
}
