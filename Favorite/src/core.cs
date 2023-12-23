using System.Collections;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.Client.NoObf;

namespace HelFavorite;

public class Core : ModSystem
{
	public static Core Instance { get; private set; }

	public const string ModId = "helfavorite";
	private const string hotkey = "hotkey";

	private Harmony harmony;

	private const string ClientConfigFile = ModId + "/ConfigClient.json";
	// Favorite slots per savefile
	private string FavoriteSlotsFile => ModId + "/" + Api.World.SavegameIdentifier + ".json";

	// Offset to ignore slots for bags
	public const int BagsOffset = 4;

	public IInventory Backpack { get; private set; }
	public IInventory CraftingGrid { get; private set; }
	public IInventory Hotbar { get; private set; }
	public IInventory Mouse { get; private set; }

	public ICoreClientAPI Api { get; private set; }

	// FIXME: Remove. We need this later as a workaround to avoid crashing a client 
	// ISSUE: https://github.com/anegostudios/VintageStory-Issues/issues/3305
	private GuiDialog backpackGui;
	// FIXME: Remove. We need this later as a workaround to avoid crashing a client 
	// ISSUE: https://github.com/anegostudios/VintageStory-Issues/issues/3305
	private GuiDialog hotbarGui;

	private ClientConfig config;

	public string Color => config.FavoriteColor;

	public FavoriteSlots FavoriteSlots { get; private set; }

	public bool MouseFavorite
	{
		get => FavoriteSlots?[Mouse]?.Count == 1;
		internal set
		{
			if (value)
				FavoriteSlots[Mouse].Add(0);
			else
				FavoriteSlots[Mouse].Remove(0);
		}
	}

	private int hotkeyCode;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

	public override void StartClientSide(ICoreClientAPI api)
	{
		Api = api;

		Instance = this;

		harmony = new(ModId);
		harmony.PatchAll();

		api.Input.RegisterHotKey(hotkey, Lang.Get(ModId + ":hotkey"), GlKeys.AltLeft, HotkeyType.InventoryHotkeys);

		api.Event.PlayerJoin += OnPlayerJoin;
		api.Event.LeaveWorld += OnPlayerLeave;

		api.Event.HotkeysChanged += OnHotkeyChanged;
		OnHotkeyChanged();

		config = Helper.LoadConfig<ClientConfig>(Api, ClientConfigFile);
	}

	private void OnHotkeyChanged() => hotkeyCode = Api.Input.GetHotKeyByCode(hotkey).CurrentMapping.KeyCode;

	// NOTE: Should throttle? 
	private void OnBackpackSlotModified(int _) => UpdateFavorite(Backpack);
	private void OnCraftingGridSlotModified(int _) => UpdateFavorite(CraftingGrid);
	private void OnHotbarSlotModified(int _) => UpdateFavorite(Hotbar);

	private void OnPlayerJoin(IClientPlayer player)
	{
		backpackGui = Api.Gui.LoadedGuis.Find(gui => gui is GuiDialogInventory);
		hotbarGui = Api.Gui.LoadedGuis.Find(gui => gui is HudHotbar);

		Backpack = player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName);
		CraftingGrid = player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName);
		Hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
		Mouse = player.InventoryManager.GetOwnInventory(GlobalConstants.mousecursorInvClassName);

		FavoriteSlots = new(
			Helper.LoadConfig<FavoriteSlotsConfig>(Api, FavoriteSlotsFile).SlotsByInventory
				.Select(kv => new KeyValuePair<IInventory, HashSet<int>>(player.InventoryManager.GetOwnInventory(kv.Key), [.. kv.Value]))
				.Where(kv => kv.Key != null)
				.ToList()
		);

		foreach ((var inv, var favSlots) in FavoriteSlots)
			favSlots.RemoveWhere(slotId => inv[slotId] == null || inv[slotId].Empty);

		// FIXME: Remove. We need this later as a workaround to avoid crashing a client 
		// ISSUE: https://github.com/anegostudios/VintageStory-Issues/issues/3305
		Backpack.SlotModified += OnBackpackSlotModified;
		CraftingGrid.SlotModified += OnCraftingGridSlotModified;
		Hotbar.SlotModified += OnHotbarSlotModified;

		UpdateFavorite();
		HijackDropItemHotkeyHandler();

		Api.Event.MouseDown += OnMouseDown;
		Api.Event.MouseUp += OnMouseUp;
	}

	private void OnPlayerLeave()
	{
		Backpack.SlotModified -= OnBackpackSlotModified;
		CraftingGrid.SlotModified -= OnCraftingGridSlotModified;
		Hotbar.SlotModified -= OnHotbarSlotModified;

		Api.StoreModConfig(config, ClientConfigFile);
		Api.StoreModConfig(new FavoriteSlotsConfig() { SlotsByInventory = FavoriteSlots.ToDictionary() }, FavoriteSlotsFile);
	}

	private void OnMouseDown(MouseEvent e)
	{
		if (e.Button is not (EnumMouseButton.Left or EnumMouseButton.Right))
			return;

		var slot = Api.World.Player.InventoryManager.CurrentHoveredSlot;

		if (slot == null)
		{
			if (MouseFavorite)
				e.Handled = true;

			return;
		}

		if (slot.Empty)
			return;

		if (!Api.Input.KeyboardKeyStateRaw[hotkeyCode])
			return;

		var favSlots = FavoriteSlots[slot.Inventory];

		if (favSlots == null)
			return;

		var slotId = slot.Inventory.GetSlotId(slot);

		if (slot.Inventory == Backpack && slotId < BagsOffset)
			return;

		// Toggle slot
		if (favSlots.Contains(slotId)) favSlots.Remove(slotId);
		else favSlots.Add(slotId);

		e.Handled = true;

		UpdateFavorite(slot.Inventory);
	}

	private void OnMouseUp(MouseEvent e)
	{
		// Api.Logger.Warning($"UP - {Core.}");
		if (Mouse.Empty)
			MouseFavorite = false;
	}

	private void HijackDropItemHotkeyHandler()
	{
		string[] hotkeys = ["dropitem", "dropitems"];

		foreach (var hotkey in hotkeys)
		{
			var originalHandler = Api.Input.GetHotKeyByCode(hotkey).Handler;

			bool newHandler(KeyCombination key)
			{
				var hoverSlot = Api.World.Player.InventoryManager.CurrentHoveredSlot;

				return (hoverSlot == null || !hoverSlot.IsFavorite())
					&& !FavoriteSlots[Hotbar].Contains(Api.World.Player.InventoryManager.ActiveHotbarSlotNumber)
					&& originalHandler(key);
			}

			Api.Input.SetHotKeyHandler(hotkey, newHandler);
		}
	}

	public void UpdateFavorite(IInventory forInv = null)
	{
		foreach ((var inv, var favSlots) in FavoriteSlots.Where(kv => kv.Key != Mouse && (forInv == null || kv.Key == forInv)))
		{
			// FIXME: Remove once you can dynamically color slot without a crash
			// ISSUE: https://github.com/anegostudios/VintageStory-Issues/issues/3305
			var shouldRecompose = true;
			// Reset colors
			foreach (var slot in inv)
				// To ensure others mods keep their colors
				if (slot.HexBackgroundColor == Color)
				{
					slot.HexBackgroundColor = null;
					shouldRecompose = false;
				}

			favSlots.RemoveWhere(slotId => inv[slotId] == null || inv[slotId].Empty);

			// FIXME: Remove once you can dynamically color slot without a crash
			// ISSUE: https://github.com/anegostudios/VintageStory-Issues/issues/3305
			var didColor = false;
			foreach (var id in favSlots)
			{
				inv[id].HexBackgroundColor = Color;
				didColor = true;
			}

			// ISSUE: https://github.com/anegostudios/VintageStory-Issues/issues/3305
			if (inv == Hotbar || shouldRecompose && didColor)
			{
				// NOTE: No null checks cuz we crash anyway at this point
				var comp = (inv == Hotbar ? hotbarGui : backpackGui).Composers.First().Value;
				comp.Dispose();
				comp.ReCompose();
			}
		}
	}

	public override void Dispose()
	{
		Backpack.SlotModified -= OnBackpackSlotModified;
		CraftingGrid.SlotModified -= OnCraftingGridSlotModified;
		Hotbar.SlotModified -= OnHotbarSlotModified;

		Api.Event.PlayerJoin -= OnPlayerJoin;
		Api.Event.LeaveWorld -= OnPlayerLeave;

		Api.Event.MouseDown -= OnMouseDown;
		Api.Event.MouseUp -= OnMouseUp;

		Api.Event.HotkeysChanged -= OnHotkeyChanged;

		Instance = null;

		harmony.UnpatchAll(ModId);

		base.Dispose();
	}
}

public class FavoriteSlots(List<KeyValuePair<IInventory, HashSet<int>>> slotsByInventory) : IEnumerable<KeyValuePair<IInventory, HashSet<int>>>
{
	public HashSet<int> this[IInventory inv] => slotsByInventory.Find(kv => kv.Key == inv).Value;

	public IEnumerator<KeyValuePair<IInventory, HashSet<int>>> GetEnumerator() => slotsByInventory.GetEnumerator();

	internal Dictionary<string, HashSet<int>> ToDictionary() => slotsByInventory.ToDictionary(kv => kv.Key.ClassName, kv => kv.Value);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public static class ItemSlotExtension
{
	private static bool CanFavorite(this ItemSlot slot) =>
		!slot.Empty && Core.Instance.FavoriteSlots[slot.Inventory] != null;

	public static bool IsFavorite(this ItemSlot slot, int? slotId = null)
	{
		var inv = slot.Inventory;
		var favSlots = Core.Instance.FavoriteSlots[inv];

		return favSlots != null && favSlots.Contains(slotId ?? inv.GetSlotId(slot));
	}

	public static bool TryMarkAsFavorite(this ItemSlot slot, int? slotId = null)
	{
		if (!slot.CanFavorite())
			return false;

		var inv = slot.Inventory;

		Core.Instance.FavoriteSlots[inv].Add(slotId ?? inv.GetSlotId(slot));

		return true;
	}

	public static bool TryRemoveMarkedAsFavorite(this ItemSlot slot, int? slotId = null)
	{
		var inv = slot.Inventory;

		Core.Instance.FavoriteSlots[inv]?.Remove(slotId ?? inv.GetSlotId(slot));

		return true;
	}
}
