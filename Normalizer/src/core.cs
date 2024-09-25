using System;

using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace HelNormalizer;

public class Core : ModSystem
{
	public static GridRecipe Recipe { get; private set; }
	public static int ChiseledBlockId { get; private set; }

	private Harmony harmony;
	private const string harmonyId = "helnormalizerharmony";

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

	public override void StartServerSide(ICoreServerAPI api)
	{
		RegisterRecipe(api);

		// FIXME: Remove once duping issue is fixed
		TempDisableMultiblockCraftingRecipe(api);

		harmony = new Harmony(harmonyId);
		harmony.PatchAll();
	}

	// FIXME: Remove once duping issue is fixed
	private static void TempDisableMultiblockCraftingRecipe(ICoreServerAPI api)
	{
		foreach (var recipe in api.World.GridRecipes)
		{
			if (!recipe.Shapeless) continue;

			var assetName = recipe.Name.ToString();

			if (!assetName.EndsWith("chiseledblockcombine.json", true, null))
				continue;

			recipe.Enabled = false;
			break;
		}
	}

	private static void RegisterRecipe(ICoreServerAPI api)
	{
		const string CB = "chiseledblock";
		const string PATTERN = "P";

		var block = Array.Find(api.World.SearchBlocks(new(CB)), static block => block.Code.GetName() == CB);

		if (block == null)
			return;

		var ingr = new CraftingRecipeIngredient()
		{
			Code = block.Code,
			ResolvedItemstack = new(block)
		};

		var resolved = ingr.CloneTo<GridRecipeIngredient>();
		resolved.PatternCode = PATTERN;

		Recipe = new GridRecipe()
		{
			Shapeless = false,
			IngredientPattern = PATTERN,
			Width = 1,
			Height = 1,
			Ingredients = new()
			{
				[PATTERN] = ingr
			},
			// Placeholder output will be replaced in `MatchedGridRecipe` event handler
			Output = ingr,
			Name = block.Code,
			resolvedIngredients = [resolved]
		};

		api.RegisterCraftingRecipe(Recipe);
		ChiseledBlockId = block.Id;
	}

	public override void Dispose()
	{
		harmony?.UnpatchAll(harmonyId);
		Recipe = null;
		base.Dispose();
	}
}

