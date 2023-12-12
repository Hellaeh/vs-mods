using System.Collections.Generic;

using ProtoBuf;

using Vintagestory.API.MathTools;

using SourceDestIds = (int, int);

namespace HelQuickStack;

[ProtoContract]
class StackPacket
{
	[ProtoMember(1)]
	public List<(BlockPos, List<SourceDestIds>)> Payload;
}

[ProtoContract]
class RadiusPacket
{
	[ProtoMember(1)]
	public int Payload;
}
