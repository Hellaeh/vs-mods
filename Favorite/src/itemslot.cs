using System.Runtime.CompilerServices;

using Vintagestory.API.Common;

namespace HelFavorite;

public static class ItemSlotExtension
{
	/// <summary>
	/// Checks if slot can be marked as favorite
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool CanFavorite(this ItemSlot slot) =>
		Core.Instance.FavoriteSlots[slot.Inventory] != null;

	/// <summary>
	/// Checks if slot is favorite
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsFavorite(this ItemSlot slot, int? slotId = null)
	{
		var inv = slot.Inventory;
		var favSlots = Core.Instance.FavoriteSlots[inv];

		return favSlots != null && favSlots.Contains(slotId ?? inv.GetSlotId(slot));
	}

	/// <summary>
	/// Will try to mark slot as favorite. Returns: `true` if marked or already favorite, otherwise `false`
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryMarkAsFavorite(this ItemSlot slot, int? slotId = null)
	{
		var inv = slot.Inventory;
		var favSlots = Core.Instance.FavoriteSlots[inv];

		if (favSlots == null)
			return false;

		slot.HexBackgroundColor = Core.Config.FavoriteColor;

		favSlots.Add(slotId ?? inv.GetSlotId(slot));

		return true;
	}

	/// <summary>
	/// Will try to unmark slot as favorite. Returns: `true` if unmarked or already non-favorite, otherwise `false`
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryUnmarkAsFavorite(this ItemSlot slot, int? slotId = null)
	{
		var inv = slot.Inventory;
		var favSlots = Core.Instance.FavoriteSlots[inv];

		if (favSlots == null)
			return false;

		if (slot.HexBackgroundColor == Core.Config.FavoriteColor)
			slot.HexBackgroundColor = null;

		favSlots.Remove(slotId ?? inv.GetSlotId(slot));

		return true;
	}

	/// <summary>
	/// Will update favorite slot
	/// </summary>
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

	/// <summary>
	/// Toggles slot between favorite and normal. Returns: `true` if operation was successful, otherwise `false`
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryToggleFavorite(this ItemSlot slot, int? slotId = null) =>
		slot.IsFavorite(slotId) ? slot.TryUnmarkAsFavorite(slotId) : slot.TryMarkAsFavorite(slotId);
}
