using System.Text.Json.Serialization;

namespace HelQuickStack;

public class ClientConfig
{
	public int[] FavoriteSlots { get; set; } = [];
}

public class ServerConfig
{
	[JsonIgnore]
	public const int DefaultRadius = 10;

	public int Radius { get; set; } = DefaultRadius;
}
