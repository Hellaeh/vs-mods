using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace HelBlockPick;

public class Core : ModSystem
{
	public const string ModId = "helblockpick";

	private const string Hotkey = "pickblock";
	private const string ConfigFileName = ModId + "config.json";

	private ICoreClientAPI api;

	private Config config;

	private ActionConsumable<KeyCombination> originalHandler;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

	public override void StartClientSide(ICoreClientAPI api)
	{
		this.api = api;

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

		api.Event.LevelFinalize += Init;
	}

	private void Init()
	{
		var hk = api.Input.GetHotKeyByCode(Hotkey);
		hk.KeyCombinationType = HotkeyType.CharacterControls;

		originalHandler = hk.Handler;

		api.Input.SetHotKeyHandler(Hotkey, Handler);
	}

	private bool Handler(KeyCombination key)
		=> (api.World.Player.WorldData.CurrentGameMode == EnumGameMode.Survival && Handle(key)) || originalHandler(key);

	private bool Handle(KeyCombination key)
	{
		if (key.OnKeyUp)
			return false;

		if (api.World.Player is not IClientPlayer player || player.CurrentBlockSelection is not BlockSelection selection)
			return false;
		if (selection.Block is not Block block || selection.Position is not BlockPos pos)
			return false;

		if (block.OnPickBlock(api.World, pos) is not ItemStack stack)
			return false;

		if (PickBlock(player, stack))
			return true;

		foreach (var drop in block.GetDrops(api.World, pos, player) ?? Enumerable.Empty<ItemStack>())
			if (PickBlock(player, drop))
				return true;

		return false;
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

					api.Network.SendPacketClient(packet);
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
		api.Input.SetHotKeyHandler(Hotkey, originalHandler);
		api.Event.LevelFinalize -= Init;
	}
}

