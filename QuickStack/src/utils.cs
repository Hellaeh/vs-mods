using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace HelQuickStack;

public class Utils
{
	// Math.log2(32)
	private const int CHUNK_SHIFT = 5;

	/// <summary>
	/// Will walk nearby containers in "radius"(manhattan distance)
	/// </summary>
	public static void WalkNearbyContainers(IPlayer player, int radius, ActionConsumable<BlockEntityContainer> onInventory)
	{
		var ba = player.Entity.World.BlockAccessor;
		var r = radius;

		var plPos = player.Entity.Pos;
		var center = new BlockPos((int)plPos.X, (int)Math.Ceiling(plPos.Y), (int)plPos.Z);

		var min = center.AddCopy(-r, -r, -r);
		var max = center.AddCopy(r, r, r);

		var xcmin = min.X >> CHUNK_SHIFT;
		var ycmin = min.Y >> CHUNK_SHIFT;
		var zcmin = min.Z >> CHUNK_SHIFT;
		var xcmax = max.X >> CHUNK_SHIFT;
		var zcmax = max.Z >> CHUNK_SHIFT;

		var xccount = xcmax - xcmin + 1;
		var zccount = zcmax - zcmin + 1;

		var chunks = GetChunksInArea(ba, min, max);

		bool doWork(int x, int y, int z)
		{
			var cpos = center.AddCopy(x, y, z);

			var i = ((cpos.Y >> CHUNK_SHIFT) - ycmin) * zccount - zcmin;
			i = (i + (cpos.Z >> CHUNK_SHIFT)) * xccount;
			i += (cpos.X >> CHUNK_SHIFT) - xcmin;

			var chunk = chunks[i];

			return chunk == null ||
				!chunk.BlockEntities.TryGetValue(cpos, out var entity) ||
				entity is not BlockEntityContainer container ||
				onInventory(container);
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
		var xmin = min.X >> CHUNK_SHIFT;
		var ymin = min.Y >> CHUNK_SHIFT;
		var zmin = min.Z >> CHUNK_SHIFT;
		var xmax = max.X >> CHUNK_SHIFT;
		var ymax = max.Y >> CHUNK_SHIFT;
		var zmax = max.Z >> CHUNK_SHIFT;

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
