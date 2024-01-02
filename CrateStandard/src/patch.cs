using HarmonyLib;

using Vintagestory.GameContent;

namespace HelCrateStandard;

[HarmonyPatch]
class BlockEntityCratePatch
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(BlockEntityCrate), "rndScale", MethodType.Getter)]
	public static void rndScaleGetterPostfix(ref float __result) => __result = 1f;
}
