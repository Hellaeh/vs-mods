using System;
using System.Collections.Generic;
using System.Linq;

using HelFavorite;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

using SourceDestIds = (int, int);

namespace HelQuickStack;

public class Core : ModSystem, IDisposable
{
	public const string ModId = "helquickstack";

	private const string quickRefillHK = ModId + "hotkey2";
	private const string quickStackHK = ModId + "hotkey";

	private const string channel = ModId + "channel";
	private const string serverConfigFile = ModId + "ConfigServer.json";

	// Offset to ignore slots for bags
	public const int BagsOffset = 4;

	private const int maxRadius = 256;
	public static int Radius { get; private set; }

	public HelFavorite.Core Favorite { get; private set; }

	private ICoreClientAPI cApi;
	private IClientNetworkChannel cChannel;
	private IServerNetworkChannel sChannel;

	private IInventory backpackInv => cApi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);

	public override void Start(ICoreAPI api)
	{
		base.Start(api);

		api.Network.RegisterChannel(channel)
			.RegisterMessageType<BulkMoveItemsPacket>()
			.RegisterMessageType<RadiusPacket>()
			.RegisterMessageType<SuccessPacket>();
	}

	public override void StartServerSide(ICoreServerAPI api)
	{
		base.StartServerSide(api);

		int radius = Math.Min(Helper.LoadConfig<ServerConfig>(api, serverConfigFile).Radius, maxRadius);

		var maxViewDistance = api.Server.Config.MaxChunkRadius << Utils.ChunkShift;

		sChannel = api.Network.GetChannel(channel)
			.SetMessageHandler<BulkMoveItemsPacket>(ServerMoveItems);

		api.Event.PlayerJoin += player => sChannel.SendPacket(new RadiusPacket() { Payload = Math.Min(radius, maxViewDistance) }, player);

		api.StoreModConfig<ServerConfig>(new() { Radius = radius }, serverConfigFile);
	}

	public override void StartClientSide(ICoreClientAPI api)
	{
		base.StartClientSide(api);

		api.Input.RegisterHotKey(quickRefillHK, Lang.Get(ModId + ":quickrefill"), GlKeys.X, HotkeyType.InventoryHotkeys, ctrlPressed: true);
		api.Input.RegisterHotKey(quickStackHK, Lang.Get(ModId + ":quickstack"), GlKeys.X, HotkeyType.InventoryHotkeys);

		api.Input.SetHotKeyHandler(quickRefillHK, _ => QuickRefill());
		api.Input.SetHotKeyHandler(quickStackHK, _ => QuickStack());

		cApi = api;
		cChannel = api.Network.GetChannel(channel)
			.SetMessageHandler<RadiusPacket>(packet => Radius = Math.Min(packet.Payload, maxRadius))
			.SetMessageHandler<SuccessPacket>(OnSuccess);

		Favorite = cApi.ModLoader.GetModSystem<HelFavorite.Core>();
	}

	private void OnSuccess(SuccessPacket packet)
	{
		var rattle = new AssetLocation(ModId + ":sounds/rattle");

		// TODO: Add shaking animation or something
		foreach (var pos in packet.Payload)
			cApi.World.PlaySoundAt(rattle, pos.X + .5, pos.Y, pos.Z + .5);
	}

	private bool QuickRefill()
	{
		// On top of a stack will be hotbar slots, if any
		Dictionary<int, Stack<VirtualSlot>> destSlotsByItemId = [];

		Dictionary<BlockPos, List<SourceDestIds>> hotbarPayload = [];
		Dictionary<BlockPos, List<SourceDestIds>> backpackPayload = [];

		var favSlotsByInv = Favorite.FavoriteSlots;
		var favSlotsFromHotbarAndBackpack = favSlotsByInv
			.Where(kv => kv.Key == Favorite.Backpack)
			.Concat(favSlotsByInv.Where(kv => kv.Key == Favorite.Hotbar));

		// Get non full favorite slots
		foreach ((var inv, var favSlots) in favSlotsFromHotbarAndBackpack)
			foreach (var slotId in favSlots)
			{
				var slot = inv[slotId];

				if (slot == null || slot.Empty || slot.Itemstack.Collectible.MaxStackSize == slot.StackSize)
					continue;

				var itemId = slot.Itemstack.Id;

				if (!destSlotsByItemId.TryGetValue(itemId, out var slots))
					destSlotsByItemId.Add(itemId, slots = new());

				slots.Push(new(slot, slotId));
			}

		if (destSlotsByItemId.Count == 0)
			return true;

		var suitableSourceSlots = new List<VirtualSlot>();

		Utils.WalkNearbyContainers(cApi.World.Player, Radius, container =>
		{
			suitableSourceSlots.Clear();

			var sourceInv = container.Inventory;
			var sourcePos = container.Pos;

			if (sourceInv.TakeLocked)
				return true;

			for (int souceSlotId = 0; souceSlotId < sourceInv.Count; ++souceSlotId)
			{
				var sourceSlot = sourceInv[souceSlotId];

				if (sourceSlot.Empty)
					continue;

				var itemId = sourceSlot.Itemstack.Id;

				if (!destSlotsByItemId.TryGetValue(itemId, out var destSlots))
					continue;

				var sourceSlotRem = sourceSlot.StackSize;
				while (sourceSlotRem > 0)
				{
					if (!destSlots.TryPop(out var destSlot))
					{
						destSlotsByItemId.Remove(itemId);
						break;
					}

					if (sourceSlotRem >= destSlot.RemSpace)
					{
						sourceSlotRem -= destSlot.RemSpace;
						destSlot.StackSize = destSlot.MaxStackSize;
					}
					else
					{
						destSlot.StackSize += sourceSlotRem;
						sourceSlotRem = 0;

						destSlots.Push(destSlot);
					}

					var payloadByInv = destSlot.Inventory == Favorite.Hotbar ? hotbarPayload : backpackPayload;

					if (!payloadByInv.TryGetValue(sourcePos, out var payloadSlots))
						payloadByInv.Add(sourcePos, payloadSlots = []);

					payloadSlots.Add((souceSlotId, destSlot.Id));
				}
			}

			return destSlotsByItemId.Count > 0;
		});

		if (hotbarPayload.Count > 0)
			cChannel.SendPacket(new BulkMoveItemsPacket()
			{
				Operation = Operation.QuickRefillHotbar,
				Payload = hotbarPayload.Select(kv => (kv.Key, kv.Value)).ToList()
			});

		if (backpackPayload.Count > 0)
			cChannel.SendPacket(new BulkMoveItemsPacket()
			{
				Operation = Operation.QuickRefillBackpack,
				Payload = backpackPayload.Select(kv => (kv.Key, kv.Value)).ToList()
			});

		return true;
	}

	private bool QuickStack()
	{
		Dictionary<int, Stack<VirtualSlot>> sourceSlotsByItemId = [];
		Dictionary<BlockPos, List<SourceDestIds>> payload = [];

		for (int slotId = BagsOffset; slotId < backpackInv.Count; ++slotId)
		{
			var slot = backpackInv[slotId];

			if (slot.Empty || slot.IsFavorite(slotId))
				continue;

			var itemId = slot.Itemstack.Id;

			if (!sourceSlotsByItemId.TryGetValue(itemId, out var slots))
				sourceSlotsByItemId.Add(itemId, slots = []);

			slots.Push(new(slot, slotId));
		}

		if (sourceSlotsByItemId.Count == 0)
			return true;

		// Temp buf vars
		var stackableItemIds = new HashSet<int>();
		var nonEmptySlots = new HashSet<VirtualSlot>();
		var emptySlots = new HashSet<VirtualSlot>();

		Utils.WalkNearbyContainers(cApi.World.Player, Radius, container =>
		{
			stackableItemIds.Clear();
			nonEmptySlots.Clear();
			emptySlots.Clear();

			var destInv = container.Inventory;
			var destInvPos = container.Pos;

			if (destInv.PutLocked)
				return true;

			for (var i = 0; i < destInv.Count; ++i)
			{
				var destSlot = new VirtualSlot(destInv[i], i);

				if (destSlot.Empty)
					emptySlots.Add(destSlot);
				else
					nonEmptySlots.Add(destSlot);
			}

			// Filter not suited containers
			if (nonEmptySlots.Count == 0)
				return true;

			foreach (var slot in nonEmptySlots)
				if (sourceSlotsByItemId.ContainsKey(slot.ItemId))
					stackableItemIds.Add(slot.ItemId);

			foreach (var itemId in stackableItemIds)
			{
				if (!sourceSlotsByItemId.TryGetValue(itemId, out var sourceSlots))
					continue;

				bool reachedEmptySlots = false;

				// Prioritize already filled slots
				foreach (var destSlot in nonEmptySlots.Where(slot => slot.ItemId == itemId))
					while (destSlot.RemSpace > 0)
					{
						if (!sourceSlots.TryPop(out var sourceSlot))
						{
							sourceSlotsByItemId.Remove(itemId);
							// continue outer loop
							goto Continue;
						}

						if (sourceSlot.StackSize > destSlot.RemSpace)
						{
							sourceSlot.StackSize -= destSlot.RemSpace;
							destSlot.StackSize = destSlot.MaxStackSize;

							sourceSlots.Push(sourceSlot);
						}
						else
						{
							destSlot.StackSize += sourceSlot.StackSize;
							sourceSlot.StackSize = 0;
						}

						if (!payload.TryGetValue(destInvPos, out var pairs))
							payload.Add(destInvPos, pairs = []);

						pairs.Add((sourceSlot.Id, destSlot.Id));
					}

				reachedEmptySlots = true;

				// Fill empty slots one by one
				foreach (var destSlot in emptySlots)
				{
					while (destSlot.RemSpace > 0)
					{
						if (!sourceSlots.TryPop(out var sourceSlot))
						{
							sourceSlotsByItemId.Remove(itemId);
							// continue outer loop
							goto Continue;
						}

						destSlot.Itemstack = sourceSlot.Itemstack;

						if (sourceSlot.StackSize > destSlot.RemSpace)
						{
							sourceSlot.StackSize -= destSlot.RemSpace;
							destSlot.StackSize = destSlot.MaxStackSize;

							sourceSlots.Push(sourceSlot);
						}
						else
						{
							destSlot.StackSize += sourceSlot.StackSize;
							sourceSlot.StackSize = 0;
						}

						if (!payload.TryGetValue(destInvPos, out List<SourceDestIds> pairs))
							payload.Add(destInvPos, pairs = []);

						pairs.Add((sourceSlot.Id, destSlot.Id));
					}
				}

			Continue:;
				if (reachedEmptySlots)
					emptySlots.RemoveWhere(slot => !slot.Empty);
			}

			return sourceSlotsByItemId.Count > 0;
		});

		if (payload.Count == 0)
			return true;

		cChannel.SendPacket(new BulkMoveItemsPacket()
		{
			Operation = Operation.QuickStack,
			Payload = payload.Select(kv => (kv.Key, kv.Value)).ToList()
		});

		return true;
	}

	private void ServerMoveItems(IServerPlayer player, BulkMoveItemsPacket packet)
	{
		var playerInv = player.InventoryManager.GetOwnInventory(
			packet.Operation == Operation.QuickRefillHotbar
				? GlobalConstants.hotBarInvClassName
				: GlobalConstants.backpackInvClassName
		);

		var world = player.Entity.World;

		var successPacket = new SuccessPacket() { Payload = [] };

		foreach ((var pos, List<SourceDestIds> slotPairs) in packet.Payload)
		{
			if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityContainer container)
				break;

			var containerInv = container.Inventory;
			var transferedAmount = 0;

			foreach ((var sId, var dId) in slotPairs)
			{
				ItemSlot sourceSlot;
				ItemSlot destSlot;

				if (packet.Operation == Operation.QuickStack)
				{
					sourceSlot = playerInv[sId];
					destSlot = containerInv[dId];
				}
				else
				{
					sourceSlot = containerInv[sId];
					destSlot = playerInv[dId];
				}

				if (sourceSlot == null || destSlot == null)
					continue;

				transferedAmount += sourceSlot.TryPutInto(world, destSlot, sourceSlot.StackSize);
			}

			if (transferedAmount > 0)
			{
				container.MarkDirty();
				successPacket.Payload.Add(pos);
			}
		}

		if (successPacket.Payload.Count > 0)
			sChannel.SendPacket(successPacket, player);
	}

	public override void Dispose()
	{
		Favorite?.Dispose();
		base.Dispose();
	}
}
