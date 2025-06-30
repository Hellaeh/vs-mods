using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.MathTools;

using SourceDestIds = (int, int);

namespace HelQuickStack;

internal enum Operation
{
	QuickRefillBackpack,
	QuickRefillHotbar,
	QuickStack,
}

[ProtoContract]
class BulkMoveItemsPacket
{
	[ProtoMember(1)]
	/// Direction: 
	/// QuickStack = true - quick stack to nearby inventories
	/// QuickStack = false - quick refill from nearby inventories
	public required Operation Operation;
	[ProtoMember(2)]
	public required List<(BlockPos, List<SourceDestIds>)> Payload;
}

[ProtoContract]
class MaxRadiusPacket
{
	[ProtoMember(1)]
	public int Payload;
}

[ProtoContract]
class SuccessPacket
{
	// Positions of containers that took\gave at least one item
	[ProtoMember(1)]
	public List<BlockPos> Payload = [];
}

