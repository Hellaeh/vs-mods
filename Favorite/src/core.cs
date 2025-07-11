using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using SharedLib;

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
	private const string clientConfigFile = ModId + "/ConfigClient.json";

	private Harmony harmony;

	// Favorite slots per savefile
	private string favoriteSlotsFile => ModId + "/" + Api.World.SavegameIdentifier + ".json";

	/// <summary>Offset to ignore slots for bags</summary>
	public const int BagsOffset = 4;

	public IInventory CraftingGrid { get; private set; }
	public IInventory Hotbar { get; private set; }
	public IInventory Mouse { get; private set; }

	public List<IInventory> GenericInventories { get; private set; } = [];

	public static ClientConfig Config { get; private set; }

	/// <summary>
	/// Color currenly in use to mark item as favorite.
	/// Read from config
	/// </summary>
	[Obsolete("Use `Config.FavoriteColor`")]
	public string Color => Config.FavoriteColor;

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

		api.Input.RegisterHotKey(hotkey, Lang.Get(ModId + ":hotkey"), GlKeys.Unknown, HotkeyType.InventoryHotkeys);

		api.Event.LevelFinalize += Init;
		api.Event.LeaveWorld += Cleanup;

		api.Event.HotkeysChanged += OnHotkeyChanged;
		OnHotkeyChanged();

		Config = ConfigLoader.LoadConfig<ClientConfig>(Api, clientConfigFile);
	}

	private void OnHotkeyChanged() => hotkeyCode = Api.Input.GetHotKeyByCode(hotkey).CurrentMapping.KeyCode;

	private void Init()
	{
		var player = Api.World.Player;

		CraftingGrid = player.InventoryManager.GetOwnInventory(GlobalConstants.craftingInvClassName);
		Hotbar = player.InventoryManager.GetOwnInventory(GlobalConstants.hotBarInvClassName);
		Mouse = player.InventoryManager.GetOwnInventory(GlobalConstants.mousecursorInvClassName);

		GenericInventories.Add(player.InventoryManager.GetOwnInventory(GlobalConstants.backpackInvClassName));

		FavoriteSlots = new(
			ConfigLoader.LoadConfig<FavoriteSlotsConfig>(Api, favoriteSlotsFile).SlotsByInventory
				.Select(kv => new KeyValuePair<IInventory, HashSet<int>>(player.InventoryManager.GetOwnInventory(kv.Key), [.. kv.Value]))
				.Where(kv => kv.Key != null)
				.ToList()
		);

		Update();
		HijackDropItemHotkeyHandler();

		Api.Event.MouseDown += OnMouseDown;
		Api.Event.MouseUp += OnMouseUp;

		// Simple throttle
		static Action<int> OnModified(IInventory inv)
		{
			const long INTERVAL = 50;

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

		GenericInventories.ForEach(inv => inv.SlotModified += OnModified(inv));
		CraftingGrid.SlotModified += OnModified(CraftingGrid);
		Hotbar.SlotModified += OnModified(Hotbar);
	}

	private void Cleanup()
	{
		Api.StoreModConfig(Config, clientConfigFile);
		Api.StoreModConfig(new FavoriteSlotsConfig() { SlotsByInventory = FavoriteSlots.ToDictionary() }, favoriteSlotsFile);
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

		if (slot.Inventory == GenericInventories[0] && slotId < BagsOffset)
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
		Api.Event.LevelFinalize -= Init;
		Api.Event.LeaveWorld -= Cleanup;

		Api.Event.MouseDown -= OnMouseDown;
		Api.Event.MouseUp -= OnMouseUp;

		Api.Event.HotkeysChanged -= OnHotkeyChanged;

		Instance = null;
		Config = null;

		harmony.UnpatchAll(ModId);
	}
}

public class FavoriteSlots(List<KeyValuePair<IInventory, HashSet<int>>> slotsByInventory) : IEnumerable<KeyValuePair<IInventory, HashSet<int>>>
{
	public HashSet<int> this[IInventory inv] => slotsByInventory.Find(kv => kv.Key == inv).Value;

	public IEnumerator<KeyValuePair<IInventory, HashSet<int>>> GetEnumerator() => slotsByInventory.GetEnumerator();

	internal Dictionary<string, HashSet<int>> ToDictionary() => slotsByInventory.ToDictionary(kv => kv.Key.ClassName, kv => kv.Value);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}


