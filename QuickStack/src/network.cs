using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.MathTools;

namespace HelQuickStack;

[ProtoContract]
class StackPacket
{
	[ProtoMember(1)]
	public Dictionary<BlockPos, List<int>> Payload;
}

[ProtoContract]
class RadiusPacket
{
	[ProtoMember(1)]
	public int Payload;
}
