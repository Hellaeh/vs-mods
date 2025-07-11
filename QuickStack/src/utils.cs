using System;
using System.Runtime.CompilerServices;

using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace HelQuickStack;

public static class BlockEntityContainerExtensiton
{
	/// <summary>
	/// Checks if container is allowed by user config
	/// </summary>
	// 
	// truth table W(hitelist) B(lacklist) F(ound) N(ot found)
	//         |-----|
	//         |-|W|B|
	//         |F|1|0|
	//         |N|0|1|
	//         |-----|
	// 
	public static bool IsAllowed(this BlockEntityContainer container)
		=> Core.CConfig!.GetRules().ContainsKey(container.InventoryClassName) == (Core.CConfig.Mode == Mode.Whitelist);

	[Obsolete("Use `IsSuitable`. Will be removed in 1.0")]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsQuickStackSuitable(this BlockEntityContainer container) => IsSuitable(container);

	/// <summary>
	/// Checks if container is suitable for quick stack/refill
	/// </summary>
	public static bool IsSuitable(this BlockEntityContainer container) => container switch
	{
		// TODO: Allow stack/refill to ground storage, once it fixed
		BlockEntityGroundStorage => false,
		// Disallow stack/refill to sealed barrels
		BlockEntityBarrel barrel => !barrel.Sealed,
		_ => true
	};
}

public class Utils
{
	// Math.log2(32)
	public const int ChunkShift = 5;

	/// <summary>
	/// Walks nearby containers in "radius"(manhattan distance)
	/// </summary>
	public static void WalkNearbyContainers(IPlayer player, int radius, ActionConsumable<BlockEntityContainer> onInventory)
	{
		var ba = player.Entity.World.BlockAccessor;
		var r = radius;

		var plPos = player.Entity.Pos;
		var center = plPos.AsBlockPos.Copy();
		// in case player standing on a non full block like a chest
		center.Y = (int)Math.Ceiling(plPos.Y);

		var min = center.AddCopy(-r, -r, -r);
		var max = center.AddCopy(r, r, r);

		var xcmin = min.X >> ChunkShift;
		var ycmin = min.Y >> ChunkShift;
		var zcmin = min.Z >> ChunkShift;
		var xcmax = max.X >> ChunkShift;
		var zcmax = max.Z >> ChunkShift;

		var xccount = xcmax - xcmin + 1;
		var zccount = zcmax - zcmin + 1;

		var chunks = GetChunksInArea(ba, min, max);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		bool doWork(int x, int y, int z)
		{
			var cpos = center.AddCopy(x, y, z);

			var i = ((cpos.Y >> ChunkShift) - ycmin) * zccount - zcmin;
			i = (i + (cpos.Z >> ChunkShift)) * xccount;
			i += (cpos.X >> ChunkShift) - xcmin;

			var chunk = chunks[i];

			// true - continue, false - break
			return chunk == null
				|| !chunk.BlockEntities.TryGetValue(cpos, out var entity)
				|| entity is not BlockEntityContainer container
				|| !container.IsSuitable()
				|| !container.IsAllowed()
				|| onInventory(container);
		}

		int x, y, z, d;
		// start from 1 to skip player (0,0,0) position
		for (d = 1; d <= r; ++d)
			for (x = 0; x <= d; ++x)
				for (y = 0; y <= d - x; ++y)
				{
					z = d - x - y;

					if (!doWork(x, y, z)) return;
					if (x != 0 && !doWork(-x, y, z)) return;
					if (y != 0 && !doWork(x, -y, z)) return;
					if (z != 0 && !doWork(x, y, -z)) return;
					if (x != 0 && y != 0 && !doWork(-x, -y, z)) return;
					if (x != 0 && z != 0 && !doWork(-x, y, -z)) return;
					if (y != 0 && z != 0 && !doWork(x, -y, -z)) return;
					if (x != 0 && y != 0 && z != 0 && !doWork(-x, -y, -z)) return;
				}
	}

	public static IWorldChunk[] GetChunksInArea(IBlockAccessor blockAccessor, BlockPos min, BlockPos max)
	{
		var xmin = min.X >> ChunkShift;
		var ymin = min.Y >> ChunkShift;
		var zmin = min.Z >> ChunkShift;
		var xmax = max.X >> ChunkShift;
		var ymax = max.Y >> ChunkShift;
		var zmax = max.Z >> ChunkShift;

		var xcount = xmax - xmin + 1;
		var ycount = ymax - ymin + 1;
		var zcount = zmax - zmin + 1;

		var chunks = new IWorldChunk[xcount * ycount * zcount];

		for (int y = ymin, i = 0; y <= ymax; ++y, ++i)
			for (int z = zmin, j = 0; z <= zmax; ++z, ++j)
				for (int x = xmin, k = 0; x <= xmax; ++x, ++k)
				{
					var chunk = blockAccessor.GetChunk(x, y, z);

					chunks[xcount * ((zcount * i) + j) + k] = chunk;
				}

		return chunks;
	}
}
