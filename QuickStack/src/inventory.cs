using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

using Vintagestory.API.Common;

namespace HelQuickStack;

// Currently used as helper in stacking algorithm
public class VirtualSlot(ItemSlot slot, int slotId)
{
	public ItemStack Itemstack { get; set; } = slot.Itemstack;

	public int Id { get; } = slotId;
	public int ItemId => Itemstack?.Id ?? 0;

	public int MaxStackSize => Itemstack?.Collectible?.MaxStackSize ?? int.MaxValue;
	public int StackSize { get; set; } = slot.StackSize;

	public bool Empty => StackSize == 0;
	public int RemSpace => MaxStackSize - StackSize;

	public InventoryBase Inventory => slot.Inventory;
}

#pragma warning disable CS8767
public class VirtualSlotComparer : IEqualityComparer<VirtualSlot>
{
	public bool Equals([DisallowNull] VirtualSlot x, [DisallowNull] VirtualSlot y)
	{
		return x.Inventory == y.Inventory && x.Id == y.Id;
	}

	public int GetHashCode([DisallowNull] VirtualSlot obj)
	{
		return obj.Id;
	}
}
