using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace HelCrateStandard;

public class Core : ModSystem
{
	private const string harmonyId = "helcratestandard" + "harmony";
	private Harmony harmony;

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Client;

	public override void StartClientSide(ICoreClientAPI api)
	{
		harmony = new(harmonyId);
		harmony.PatchAll();
	}

	public override void Dispose()
	{
		harmony.UnpatchAll(harmonyId);
		base.Dispose();
	}
}
