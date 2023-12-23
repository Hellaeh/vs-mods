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

	private const string hotkey = ModId + "hotkey";
	private const string channel = ModId + "channel";
	private const string serverConfigFile = ModId + "/ConfigServer.json";

	// Offset to ignore slots for bags
	public const int BagsOffset = 4;

	private const int maxRadius = 256;
	private static int radius;

	public static HelFavorite.Core Favorite { get; private set; }

	private ICoreClientAPI cApi;
	private IClientNetworkChannel cChannel;
	private IServerNetworkChannel sChannel;

	private IInventory backpackInv => cApi.World.Player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);

	public override void Start(ICoreAPI api)
	{
		base.Start(api);

		api.Network.RegisterChannel(channel)
			.RegisterMessageType<RadiusPacket>()
			.RegisterMessageType<StackPacket>()
			.RegisterMessageType<SuccessPacket>();
	}

	public override void StartServerSide(ICoreServerAPI api)
	{
		base.StartServerSide(api);

		int radius = Math.Min(Helper.LoadConfig<ServerConfig>(api, serverConfigFile).Radius, maxRadius);

		var maxViewDistance = api.Server.Config.MaxChunkRadius << Utils.ChunkShift;

		sChannel = api.Network.GetChannel(channel)
			.SetMessageHandler<StackPacket>(ChannelHandler);

		api.Event.PlayerJoin += player => sChannel.SendPacket(new RadiusPacket() { Payload = Math.Min(radius, maxViewDistance) }, player);
	}

	public override void StartClientSide(ICoreClientAPI api)
	{
		base.StartClientSide(api);

		api.Input.RegisterHotKey(hotkey, Lang.Get(ModId + ":hotkey"), GlKeys.X, HotkeyType.InventoryHotkeys);
		api.Input.SetHotKeyHandler(hotkey, _ => HotkeyHandler());

		cApi = api;
		cChannel = api.Network.GetChannel(channel)
			.SetMessageHandler<RadiusPacket>(packet => radius = Math.Min(packet.Payload, maxRadius))
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

	private bool HotkeyHandler()
	{
		Dictionary<int, Stack<VirtualSlot>> sourceSlotsByItemId = [];
		Dictionary<BlockPos, List<SourceDestIds>> payload = [];

		for (int slotId = BagsOffset; slotId < backpackInv.Count; ++slotId)
		{
			var slot = backpackInv[slotId];

			// TODO: Change for better mod compatibility
			if (slot.Empty || slot.IsFavorite(slotId))
				continue;

			var itemId = slot.Itemstack.Id;

			if (!sourceSlotsByItemId.TryGetValue(itemId, out var slots))
				sourceSlotsByItemId.Add(itemId, slots = new());

			slots.Push(new(slot, slotId));
		}

		if (sourceSlotsByItemId.Count == 0)
			return true;

		// Temp buf vars
		var stackableItemIds = new HashSet<int>();
		var nonEmptySlots = new HashSet<VirtualSlot>();
		var emptySlots = new HashSet<VirtualSlot>();

		bool ClearBufsAndReturn(bool ret)
		{
			stackableItemIds.Clear();
			nonEmptySlots.Clear();
			emptySlots.Clear();

			return ret;
		}

		Utils.WalkNearbyContainers(cApi.World.Player, radius, container =>
		{
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
				return ClearBufsAndReturn(true);

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

			return ClearBufsAndReturn(sourceSlotsByItemId.Count > 0);
		});

		if (payload.Count == 0)
			return true;

		cChannel.SendPacket(new StackPacket()
		{
			Payload = payload.Select(kv => (kv.Key, kv.Value)).ToList()
		});

		return true;
	}

	private void ChannelHandler(IServerPlayer player, StackPacket packet)
	{
		var sourceInv = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
		var world = player.Entity.World;

		var successPacket = new SuccessPacket() { Payload = new List<BlockPos>(packet.Payload.Count) };

		foreach ((var pos, List<SourceDestIds> slotPairs) in packet.Payload)
		{
			if (world.BlockAccessor.GetBlockEntity(pos) is not BlockEntityContainer container)
			{
				if (successPacket.Payload.Count > 0)
					sChannel.SendPacket(successPacket, player);

				return;
			}

			var destInv = container.Inventory;
			var transferedAmount = 0;

			foreach ((var sId, var dId) in slotPairs)
			{
				var destSlot = destInv[dId];
				var sourceSlot = sourceInv[sId];
				var stackSize = sourceSlot.StackSize;

				transferedAmount += sourceSlot.TryPutInto(world, destSlot, stackSize);
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
		Favorite = null;
		base.Dispose();
	}
}
