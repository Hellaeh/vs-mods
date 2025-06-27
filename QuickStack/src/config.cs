using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

using Vintagestory.API.Common;

namespace HelQuickStack;

public static class Helper
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

	public static T LoadConfig<T>(ICoreAPI api, string filename)
		where T : new() => Inner<T>(api, filename, new());

	public static T LoadConfig<T>(ICoreAPI api, string filename, T def)
		where T : new() => Inner(api, filename, def);
}

public enum Mode
{
	Whitelist = 0,
	Blacklist,
}

public enum Applicable
{
	All,
	// TODO: below
	// BlockPos
}

public class Rule
{
	public Rule() { }

	public Applicable ApplicableTo { get; set; } = Applicable.All;

	// TODO: below
	// public HashSet<BlockPos> Positions { get; set; } = [];
}

public class ClientConfig
{
	public static ClientConfig Default()
	{
		var config = new ClientConfig();

		config.Whitelist.Add("chest", new());
		config.Whitelist.Add("barrel", new());
		config.Whitelist.Add("crate", new());

		return config;
	}

	public int Radius { get; set; } = 10;

	public Mode Mode { get; set; } = Mode.Whitelist;

	// Dictionary<InventoryClassName, Rule>
	public Dictionary<string, Rule> Blacklist { get; set; } = [];
	public Dictionary<string, Rule> Whitelist { get; set; } = [];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Dictionary<string, Rule> GetRules() => Mode == Mode.Whitelist ? Whitelist : Blacklist;
}

public class ServerConfig
{
	[JsonIgnore]
	public const int DefaultRadius = 10;

	public int MaxRadius { get; set; } = DefaultRadius;
}

