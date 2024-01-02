using HarmonyLib;

using Vintagestory.GameContent;

namespace HelCrateStandard;

[HarmonyPatch]
class BlockEntityCratePatch
{
	[HarmonyPrefix]
	[HarmonyPatch(typeof(BlockEntityCrate), "rndScale", MethodType.Getter)]
	public static bool rndScaleGetterPostfix(ref float __result)
	{
		__result = 1f;
		return false;
	}
}
