using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace HelFavorite;

public class Core : ModSystem
{
	public static Core Instance { get; private set; }

	public ICoreClientAPI Api { get; private set; }

	public const string ModId = "helfavorite";

	private const string hotkey = ModId + "hotkey";
	private const string ClientConfigFile = ModId + "/ConfigClient.json";

	private Harmony harmony;

	// Favorite slots per savefile
	private string FavoriteSlotsFile => ModId + "/" + Api.World.SavegameIdentifier + ".json";

	/// <summary>Offset to ignore slots for bags</summary>
	public const int BagsOffset = 4;

	public IInventory Backpack { get; private set; }
	public IInventory CraftingGrid { get; private set; }
	public IInventory Hotbar { get; private set; }
	public IInventory Mouse { get; private set; }

	private ClientConfig config;

	/// <summary>
	/// Color currenly in use to mark item as favorite.
	/// Read from config
	/// </summary>
	public string Color => config.FavoriteColor;

	/// <summary>A collection of slot indices by inventory</summary>
	public FavoriteSlots FavoriteSlots { get; private set; }

	/// <summary>An abstraction for `FavoriteSlots[Mouse].Contains(0)`</summary>
	public bool MouseFavorite
	{
		get => FavoriteSlots?[Mouse]?.Count == 1;
		// Hopefully in the future mouse cursor item will be saved in worldsave
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

	private void OnPlayerJoin(IClientPlayer player)
	{
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

		Update();
		HijackDropItemHotkeyHandler();

		Api.Event.MouseDown += OnMouseDown;
		Api.Event.MouseUp += OnMouseUp;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// Simple throttle
		static Action<int> OnModified(IInventory inv)
		{
			const int INTERVAL = 50;

			int lastSlotId = -1;
			long lastTime = 0;

			return (int slotId) =>
			{
				var now = Instance.Api.ElapsedMilliseconds;

				if (slotId != lastSlotId || now - lastTime > INTERVAL)
				{
					inv[slotId].UpdateFavorite(slotId);
					lastSlotId = slotId;
					lastTime = now;
				}
			};
		}

		Backpack.SlotModified += OnModified(Backpack);
		CraftingGrid.SlotModified += OnModified(CraftingGrid);
		Hotbar.SlotModified += OnModified(Hotbar);
	}

	private void OnPlayerLeave()
	{
		Api.StoreModConfig(config, ClientConfigFile);
		Api.StoreModConfig(new FavoriteSlotsConfig() { SlotsByInventory = FavoriteSlots.ToDictionary() }, FavoriteSlotsFile);
	}

	private void OnMouseDown(MouseEvent e)
	{
		if (e.Button is not (EnumMouseButton.Left or EnumMouseButton.Right))
			return;

		var slot = Api.World.Player.InventoryManager.CurrentHoveredSlot;

		if (slot == null || slot.Empty)
			return;

		if (!Api.Input.KeyboardKeyStateRaw[hotkeyCode])
			return;

		var favSlots = FavoriteSlots[slot.Inventory];

		if (favSlots == null)
			return;

		var slotId = slot.Inventory.GetSlotId(slot);

		if (slot.Inventory == Backpack && slotId < BagsOffset)
			return;

		slot.TryToggleFavorite(slotId);

		e.Handled = true;
	}

	private void OnMouseUp(MouseEvent e) { if (Mouse.Empty && MouseFavorite) MouseFavorite = false; }

	private void HijackDropItemHotkeyHandler()
	{
		foreach (var hotkey in (string[])(["dropitem", "dropitems"]))
		{
			var originalHandler = Api.Input.GetHotKeyByCode(hotkey).Handler;

			bool newHandler(KeyCombination key)
			{
				var hoverSlot = Api.World.Player.InventoryManager.CurrentHoveredSlot;

				return (
					hoverSlot != null
						? !hoverSlot.IsFavorite()
						: !FavoriteSlots[Hotbar].Contains(Api.World.Player.InventoryManager.ActiveHotbarSlotNumber)
				) && originalHandler(key);
			}

			Api.Input.SetHotKeyHandler(hotkey, newHandler);
		}
	}

	// TODO: Remove in 1.0.0
	[Obsolete("`UpdateFavorite` is renamed to `Update` and will be removed in 1.0.0")]
	public void UpdateFavorite(IInventory inv = null) => Update(inv);

	/// <summary>Will forcefully update favorite slots and VFX</summary>
	public void Update(IInventory forInv = null)
	{
		foreach ((var inv, var _) in FavoriteSlots.Where(kv => forInv == null || kv.Key == forInv))
			for (int slotId = 0; slotId < inv.Count; ++slotId)
				inv[slotId].UpdateFavorite(slotId);
	}

	public override void Dispose()
	{
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool CanFavorite(this ItemSlot slot) =>
		Core.Instance.FavoriteSlots[slot.Inventory] != null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsFavorite(this ItemSlot slot, int? slotId = null)
	{
		var inv = slot.Inventory;
		var favSlots = Core.Instance.FavoriteSlots[inv];

		return favSlots != null && favSlots.Contains(slotId ?? inv.GetSlotId(slot));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryMarkAsFavorite(this ItemSlot slot, int? slotId = null)
	{
		if (!slot.CanFavorite())
			return false;

		var inv = slot.Inventory;

		slot.HexBackgroundColor = Core.Instance.Color;

		Core.Instance.FavoriteSlots[inv].Add(slotId ?? inv.GetSlotId(slot));

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryUnmarkAsFavorite(this ItemSlot slot, int? slotId = null)
	{
		var inv = slot.Inventory;

		if (Core.Instance.FavoriteSlots[inv] == null)
			return false;

		if (slot.HexBackgroundColor == Core.Instance.Color)
			slot.HexBackgroundColor = null;

		Core.Instance.FavoriteSlots[inv].Remove(slotId ?? inv.GetSlotId(slot));

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void UpdateFavorite(this ItemSlot slot, int? slotId = null)
	{
		if (!slot.IsFavorite(slotId))
			return;

		if (slot.Empty)
			slot.TryUnmarkAsFavorite(slotId);
		else
			slot.TryMarkAsFavorite(slotId);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void TryToggleFavorite(this ItemSlot slot, int? slotId = null)
	{
		if (slot.IsFavorite(slotId))
			slot.TryUnmarkAsFavorite(slotId);
		else
			slot.TryMarkAsFavorite(slotId);
	}
}
