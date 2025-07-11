using System;

namespace SharedLib;

public unsafe struct Map<T>(uint size = 32)
	where T : unmanaged
{
	private Vec<T> inner = new(size);

	private Vec<uint> freeSlotsIds = new(size); // stack of free indices
	private Vec<uint> filledSlotsIds = new(size); // sorted list of indices

	public uint Insert(T value)
	{
		if (freeSlotsIds.TryPop(out var freeSlot))
		{
			inner[freeSlot] = value;
			filledSlotsIds.BinaryInsert(freeSlot);
			return freeSlot;
		}

		var id = inner.Length();

		inner.Push(value);
		filledSlotsIds.Push(id);

		return id;
	}

	public T* TakeOut(uint id)
	{
		if (id >= size)
			return null;

		var res = filledSlotsIds.BinarySearch(id);

		if (res.Type == Vec<uint>.ResultType.Miss)
			return null;

		filledSlotsIds.RemoveAt(res.Index);

		return inner.GetRaw(id);
	}

	public void PutBack(uint id)
	{
		if (id >= size)
			throw new ArgumentOutOfRangeException($"{nameof(id)}:{id} is out of boundaries of {nameof(size)}:{size}");

		if (filledSlotsIds.BinarySearch(id) is Vec<uint>.Result { Type: Vec<uint>.ResultType.Hit })
			return;

		filledSlotsIds.BinaryInsert(id);
	}

	public T? Remove(uint id)
	{
		if (id >= size || freeSlotsIds.IndexOf(id) >= 0)
			return null;

		freeSlotsIds.Push(id);
		filledSlotsIds.Remove(id);

		return inner[id];
	}

	internal readonly void Dispose()
	{
		inner.Dispose();
		freeSlotsIds.Dispose();
		filledSlotsIds.Dispose();
	}

	public readonly uint Length() => filledSlotsIds.Length();
}
