using Newtonsoft.Json;

using Vintagestory.API.Common;

namespace HelQuickStack;

public class Helper
{
	public static T LoadConfig<T>(ICoreAPI api, string filename)
	where T : new()
	{
		T config;

		try
		{
			config = api.LoadModConfig<T>(filename) ?? new();
		}
		catch
		{
			config = new();
		}

		return config;
	}
}

public class ServerConfig
{
	[JsonIgnore]
	public const int DefaultRadius = 10;

	public int Radius { get; set; } = DefaultRadius;
}

