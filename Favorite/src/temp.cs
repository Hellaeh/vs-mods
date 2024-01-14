// ISSUE: https://github.com/anegostudios/VintageStory-Issues/issues/3305
// FIXME: Remove this file once https://github.com/anegostudios/vsapi/pull/15 merged
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using Cairo;
using HarmonyLib;

using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace HelFavorite;

[HarmonyPatch]
static class GuiElementItemSlotGridBasePatchTemp
{
	static readonly FieldInfo slotTextureIdsByBgIconAndColor = AccessTools.Field(typeof(GuiElementItemSlotGridBase), "slotTextureIdsByBgIconAndColor");

	static readonly OpCode[] sigToLook = [OpCodes.Ldloc_S, OpCodes.Ldfld, OpCodes.Ldstr, OpCodes.Ldloc_S, OpCodes.Ldfld, OpCodes.Call, OpCodes.Stloc_S];

	static int SigScan(List<CodeInstruction> codeInstructions, OpCode[] sig)
	{
		int i = 0;

		return codeInstructions.FindIndex(inst =>
		{
			if (inst.opcode.Equals(sig[i]))
			{
				if (++i == sig.Length)
					return true;
			}
			else
				i = 0;

			return false;
		});
	}

	[HarmonyTranspiler]
	[HarmonyPatch(typeof(GuiElementItemSlotGridBase), nameof(GuiElementItemSlotGridBase.RenderInteractiveElements))]
	internal static IEnumerable<CodeInstruction> RenderInteractiveElementsTranspiler(IEnumerable<CodeInstruction> instructions)
	{
		var insts = new List<CodeInstruction>(instructions);

		var insertIdx = SigScan(insts, sigToLook);

		if (insertIdx < 0)
			goto Ret;

		CodeInstruction[] patchInsts = [
			// push `this`
			new(OpCodes.Ldarg_0),
			// push `slot`
			new(OpCodes.Ldloc_S, 8),
			// push `key`
			new(OpCodes.Ldloc_S, 12),
			// call extension method
			new(OpCodes.Call, AccessTools.Method(typeof(GuiElementItemSlotGridBasePatchTemp), nameof(DrawSlotBackgrounds))),
		];

		var alreadyPatched = SigScan(insts, patchInsts.Select(inst => inst.opcode).ToArray()) - insertIdx == patchInsts.Length;

		if (alreadyPatched)
			goto Ret;

		insts.InsertRange(insertIdx + 1, patchInsts);

	Ret:
		return insts;
	}

	static void DrawSlotBackgrounds(this GuiElementItemSlotGridBase __this, ItemSlot slot, string key)
	{
		var dic = slotTextureIdsByBgIconAndColor.GetValue(__this) as Dictionary<string, int>;

		if (dic.ContainsKey(key))
			return;

		var absSlotPadding = GuiElement.scaled(GuiElementItemSlotGridBase.unscaledSlotPadding);
		var absSlotWidth = GuiElement.scaled(GuiElementPassiveItemSlot.unscaledSlotSize);
		var absSlotHeight = absSlotWidth;

		ImageSurface slotSurface = new(Format.Argb32, (int)absSlotWidth, (int)absSlotWidth);

		Context slotCtx = new(slotSurface);
		slotCtx.SetSourceRGBA(0, 0, 0, 0);
		slotCtx.Paint();
		slotCtx.Antialias = Antialias.Best;

		double[] bgcolor;
		double[] fontcolor;

		if (slot.HexBackgroundColor != null)
		{
			bgcolor = ColorUtil.Hex2Doubles(slot.HexBackgroundColor);
			fontcolor = [bgcolor[0] * 0.25, bgcolor[1] * 0.25, bgcolor[2] * 0.25, 1];
		}
		else
		{
			bgcolor = GuiStyle.DialogSlotBackColor;
			fontcolor = GuiStyle.DialogSlotFrontColor;
		}

		slotCtx.SetSourceRGBA(bgcolor);
		GuiElement.RoundRectangle(slotCtx, 0, 0, absSlotWidth, absSlotHeight, GuiStyle.ElementBGRadius);
		slotCtx.Fill();

		slotCtx.SetSourceRGBA(fontcolor);
		GuiElement.RoundRectangle(slotCtx, 0, 0, absSlotWidth, absSlotHeight, GuiStyle.ElementBGRadius);
		slotCtx.LineWidth = GuiElement.scaled(4.5);
		slotCtx.Stroke();

		slotSurface.BlurFull(GuiElement.scaled(4));
		slotSurface.BlurFull(GuiElement.scaled(4));

		slotCtx.SetSourceRGBA(0, 0, 0, 0.8);
		GuiElement.RoundRectangle(slotCtx, 0, 0, absSlotWidth, absSlotHeight, 1);
		slotCtx.LineWidth = GuiElement.scaled(4.5);
		slotCtx.Stroke();

		if (slot.BackgroundIcon != null)
			__this.DrawIconHandler?.Invoke(
					slotCtx, slot.BackgroundIcon, 2 * (int)absSlotPadding, 2 * (int)absSlotPadding,
					(int)(absSlotWidth - 4 * absSlotPadding), (int)(absSlotHeight - 4 * absSlotPadding),
					[0, 0, 0, 0.2]
			);

		int texId = Core.Instance.Api.Gui.LoadCairoTexture(slotSurface, true);

		slotCtx.Dispose();
		slotSurface.Dispose();

		dic.Add(key, texId);
	}
}
