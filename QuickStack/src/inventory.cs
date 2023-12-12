using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Vintagestory.API.Common;

namespace HelQuickStack;

public class VirtualSlot(ItemSlot slot, int slotId)
{
	public ItemStack Itemstack { get; set; } = slot.Itemstack?.GetEmptyClone();

	public int Id { get; } = slotId;
	public int ItemId => Itemstack?.Id ?? 0;

	public int MaxStackSize => Itemstack?.Collectible?.MaxStackSize ?? int.MaxValue;
	public int StackSize { get; set; } = slot.StackSize;

	public bool Empty => StackSize == 0;
	public int RemSpace => MaxStackSize - StackSize;
}

public class VirtualSlotComparer : IEqualityComparer<VirtualSlot>
{
	public bool Equals(VirtualSlot x, VirtualSlot y)
	{
		return x.Id == y.Id;
	}

	public int GetHashCode([DisallowNull] VirtualSlot obj)
	{
		return obj.Id;
	}
}
