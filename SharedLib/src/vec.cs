using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharedLib;

public unsafe struct Vec<T> where T : unmanaged
{
	private const int DEFAULT_CAPACITY = 8;

	internal T* buf; // 8 bytes
	internal uint len; // 4 bytes
	internal uint cap; // 4 bytes

	public Vec() { }
	public Vec(uint capacity)
	{
		cap = capacity;
		buf = (T*)NativeMemory.Alloc(capacity * (uint)sizeof(T));
	}

	public int IndexOf(T value)
	{
		for (var i = 0; i < len; ++i)
			if (buf[i].Equals(value))
				return i;

		return -1;
	}

	public void Push(T value)
	{
		if (len == cap)
			Grow();

		buf[len] = value;
		len += 1;
	}

	public T? Pop()
	{
		if (len == 0)
			return null;

		len -= 1;

		return buf[len];
	}

	public bool TryPop(out T value)
	{
		if (Pop() is T val)
		{
			value = val;
			return true;
		}

		value = default;
		return false;
	}

	public bool Remove(T value)
	{
		if (!(IndexOf(value) is int i && i >= 0))
			return false;

		RemoveAt((uint)i);
		return true;
	}

	public void RemoveAt(uint i)
	{
		if (i > len)
			throw new ArgumentOutOfRangeException($"Parameter i {i} is out of range");

		// TODO: use memcpy for larger chunks
		len -= 1;
		for (; i < len; ++i)
			this[i] = this[i + 1];
	}

	public void InsertAt(uint i, T value)
	{
		if (i > len)
			throw new ArgumentOutOfRangeException($"Parameter i {i} is out of range");

		if (len == cap)
			Grow();

		for (var k = i; k < len; ++k)
			this[k + 1] = this[k];

		this[i] = value;

		len += 1;
	}

	public readonly T* GetRaw(uint id) => buf + id;

	internal void Grow()
	{
		if (cap == 0)
		{
			cap = DEFAULT_CAPACITY;
			buf = (T*)NativeMemory.Alloc(cap * (uint)Unsafe.SizeOf<T>());
			return;
		}

		cap *= 2;
		buf = (T*)NativeMemory.Realloc(buf, cap);
	}

	public T this[uint index]
	{
		get => buf[index];
		set => buf[index] = value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly uint Length() => len;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly uint Capacity() => cap;

	public enum ResultType : uint { Hit, Miss }
	public readonly record struct Result(ResultType Type, uint Index);

	public readonly void Dispose() => NativeMemory.Free(buf);
}

public static class VecExtension
{
	public static void InsertionSort<T>(ref this Vec<T> instance) where T : unmanaged, IComparable<T>
	{
		for (uint i = 1; i < instance.len; ++i)
		{
			var key = instance[i];
			uint j = i - 1;

			while (j >= 0 && instance[j].CompareTo(key) > 0)
			{
				instance[j + 1] = instance[j];
				j--;
			}

			instance[j + 1] = key;
		}
	}

	public static Vec<T>.Result BinarySearch<T>(ref this Vec<T> instance, T value) where T : unmanaged, IComparable<T>
	{
		int left = 0;
		int right = (int)instance.len - 1;

		while (left <= right)
		{
			var mid = (uint)(left + (right - left) / 2);

			var comparison = instance[mid].CompareTo(value);

			if (comparison == 0)
				return new(Vec<T>.ResultType.Hit, mid);
			else if (comparison < 0)
				left = (int)mid + 1;
			else
				right = (int)mid - 1;
		}

		return new(Vec<T>.ResultType.Miss, (uint)left);
	}

	public static uint BinaryInsert<T>(ref this Vec<T> instance, T value) where T : unmanaged, IComparable<T>
	{
		var res = instance.BinarySearch(value);

		if (res.Type == Vec<T>.ResultType.Hit)
			return res.Index;

		instance[res.Index] = value;
		instance.len += 1;

		return res.Index;
	}
}
