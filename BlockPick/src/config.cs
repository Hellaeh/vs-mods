using System;

using Newtonsoft.Json;

namespace HelBlockPick;

public class Config
{
	public bool IgnoreToolSlot { get; set; } = true;

	public bool PreferCurrentActiveSlot { get; set; } = true;
	public bool PreferEmptySlot { get; set; } = true;

	public int FallbackSlot { get => fallbackSlot; set => fallbackSlot = Math.Clamp(value, 0, RightmostHotbarSlot); }

	[JsonIgnore]
	private int fallbackSlot = RightmostHotbarSlot;
	[JsonIgnore]
	const int RightmostHotbarSlot = 9;
}
