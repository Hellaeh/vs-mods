using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace HelBlockPick;

public class Core : ModSystem
{
	public const string ModId = "helblockpick";

	private const string Hotkey = "pickblock";
	private const string ConfigFileName = ModId + "config.json";

	public ICoreClientAPI Api { get; private set; }

	private Config config;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

	public override void StartClientSide(ICoreClientAPI api)
	{
		try
		{
			config = api.LoadModConfig<Config>(ConfigFileName);
		}
		finally
		{
			if (config == null)
			{
				config = new();
				api.StoreModConfig(config, ConfigFileName);
			}
		}

		api.Event.PlayerJoin += OnPlayerJoin;

		Api = api;
	}

	private void OnPlayerJoin(IClientPlayer _)
	{
		var hk = Api.Input.GetHotKeyByCode(Hotkey);
		hk.KeyCombinationType = HotkeyType.CharacterControls;

		var originalHandler = hk.Handler;

		bool newHandler(KeyCombination key) =>
			(Api.World.Player.WorldData.CurrentGameMode == EnumGameMode.Survival && Handler()) || originalHandler(key);

		Api.Input.SetHotKeyHandler(Hotkey, newHandler);
	}

	private bool Handler()
	{
		var player = Api.World.Player;
		var lookingAt = player.CurrentBlockSelection;

		if (lookingAt?.Block == null || lookingAt?.Position == null)
			return false;

		var lookingAtItemStack = lookingAt.Block.OnPickBlock(Api.World, lookingAt.Position);

		var res = PickBlock(player, lookingAtItemStack);

		// HACK: If `OnBlockPick` fails - we fallback to `GetDrops`
		if (!res)
			foreach (var drop in lookingAt.Block.GetDrops(Api.World, lookingAt.Position, player))
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

				// REMINDER: Add inventory classnames you want to ignore here
				case GlobalConstants.characterInvClassName:
				case GlobalConstants.craftingInvClassName:
				case GlobalConstants.creativeInvClassName:
				case GlobalConstants.groundInvClassName:
				case GlobalConstants.mousecursorInvClassName:
					break;

				default:
					var bestSlotIdx = GetBestSuitedHotbarSlot(player);
					var packet = player.InventoryManager.GetHotbarInventory().TryFlipItems(bestSlotIdx, slot);

					if (packet == null)
						break;

					Api.Network.SendPacketClient(packet);
					player.InventoryManager.ActiveHotbarSlotNumber = bestSlotIdx;

					break;
			}

			return !(handled = true);
		});

		return handled;
	}

	private int GetBestSuitedHotbarSlot(IClientPlayer player)
	{
		// Hardcoded for now
		const int hbSlots = 10;

		var hotbarInv = player.InventoryManager.GetHotbarInventory();
		var activeSlotId = player.InventoryManager.ActiveHotbarSlotNumber;

		// Try to choose current active hotbar slot
		if (config.PreferCurrentActiveSlot)
			if (!config.IgnoreToolSlot || hotbarInv[activeSlotId].Itemstack?.Item?.Tool == null)
				return activeSlotId;

		// Try to choose any empty slot
		if (config.PreferEmptySlot)
			for (int i = 0; i < hbSlots; ++i)
				if (hotbarInv[i].Empty)
					return i;

		// Try to choose any non-tool slot
		if (config.IgnoreToolSlot)
			for (int i = 0; i < hbSlots; ++i)
				if (hotbarInv[i].Itemstack?.Item?.Tool == null)
					return i;

		// Return user defined slot
		return config.FallbackSlot;
	}

	public override void Dispose()
	{
		Api.Event.PlayerJoin -= OnPlayerJoin;
	}
}

