using System.Runtime.CompilerServices;

using Vintagestory.API.Common;

namespace SharedLib;

public static class ConfigLoader
{
	public static T Inner<T>(ICoreAPI api, string filename, T init)
		where T : new()
	{
		T config;

		try
		{
			config = api.LoadModConfig<T>(filename) ?? init;
		}
		catch
		{
			config = init;
		}

		return config;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T LoadConfig<T>(ICoreAPI api, string filename)
		where T : new() => Inner<T>(api, filename, new());

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T LoadConfig<T>(ICoreAPI api, string filename, T def)
		where T : new() => Inner(api, filename, def);
}
