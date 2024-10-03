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
	public Operation Operation;
	[ProtoMember(2)]
	public List<(BlockPos, List<SourceDestIds>)> Payload;
}

[ProtoContract]
class RadiusPacket
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

