using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SharedLib;

[StructLayout(LayoutKind.Sequential)]
internal struct Alignment<T> where T : unmanaged
{
	public byte Padding;
	public T Target;

	public static unsafe uint AlignmentOf()
	{
		Alignment<T> t = default;
		var p1 = Unsafe.AsPointer(ref t);
		var p2 = Unsafe.AsPointer(ref t.Target);

		return (uint)((IntPtr)p2 - (IntPtr)p1);
	}
}

public unsafe struct Arena(uint size = 4096)
{
	private byte* buffer = (byte*)NativeMemory.Alloc(size);
	private uint offset;
	private int length;
	private readonly uint capacity = size;

	public readonly IntPtr Address => (IntPtr)buffer;

	public T* Allocate<T>() where T : unmanaged
	{
		var alignment = Alignment<T>.AlignmentOf();
		var size = (uint)Unsafe.SizeOf<T>();
		var alignedOffset = (offset + alignment - 1) & ~(alignment - 1);

		if (alignedOffset + size > capacity)
			throw new ArgumentOutOfRangeException("Arena is out of space");

		var ptr = (T*)(buffer + alignedOffset);

		offset = alignedOffset + size;
		length += 1;

		return ptr;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly int Length() => length;

	public void Reset()
	{
		offset = 0;
		length = 0;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly void* Get(uint offset) => buffer + offset;
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public readonly T* Get<T>(uint offset) where T : unmanaged => (T*)Get(offset);

	public readonly uint GetByteOffsetFor(void* p) => (uint)((IntPtr)p - (IntPtr)buffer);

	public void Dispose()
	{
		NativeMemory.Free(buffer);
		buffer = null;
	}
}
