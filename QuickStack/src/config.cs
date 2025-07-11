using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Newtonsoft.Json;

namespace HelQuickStack;

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

	[JsonIgnore]
	private int radius = Consts.DefaultRadius;
	public int Radius
	{
		get => radius;
		set => radius = Math.Min(value, Math.Min(Consts.MaxRadius, Core.SConfig.MaxRadius));
	}

	public Mode Mode { get; set; } = Mode.Whitelist;

	// string = inventory classname
	public Dictionary<string, Rule> Blacklist { get; set; } = [];
	public Dictionary<string, Rule> Whitelist { get; set; } = [];

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Dictionary<string, Rule> GetRules() => Mode == Mode.Whitelist ? Whitelist : Blacklist;
}

public class ServerConfig
{
	[JsonIgnore]
	private int maxRadius = Consts.MaxRadius;
	public int MaxRadius
	{
		get => maxRadius;
		set => maxRadius = Math.Min(value, Math.Min((Core.sApi?.Server.Config.MaxChunkRadius ?? 8) * SharedLib.Consts.ChunkSize, Consts.MaxRadius));
	}
}

