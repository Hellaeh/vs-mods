using System;
using System.Linq;

using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace HelNormalizer;

public class Core : ModSystem
{
	public static GridRecipe Recipe { get; private set; }

	private int chiseledBlockId;
	private ICoreServerAPI sApi;

	private Harmony harmony;
	private const string harmonyId = "helnormalizerharmony";

	public override bool ShouldLoad(EnumAppSide forSide) => forSide == EnumAppSide.Server;

	public override void StartServerSide(ICoreServerAPI api)
	{
		base.StartServerSide(api);

		RegisterRecipe(api);

		harmony = new Harmony(harmonyId);
		harmony.PatchAll();

		api.Event.MatchesGridRecipe += OnGridRecipeMatch;

		sApi = api;
	}

	private void RegisterRecipe(ICoreServerAPI api)
	{
		const string CB = "chiseledblock";
		const string PATTERN = "S";

		var block = Array.Find(api.World.SearchBlocks(new(CB)), block => block.Code.GetName() == CB);

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
			Shapeless = true,
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
			resolvedIngredients = new GridRecipeIngredient[1] { resolved }
		};

		api.RegisterCraftingRecipe(Recipe);
		chiseledBlockId = block.Id;
	}

	private bool OnGridRecipeMatch(IPlayer player, GridRecipe recipe, ItemSlot[] inputSlots, int gridWidth)
	{
		// `MatchesGridRecipe` will fire for every single recipe in game, so we filter em
		if (Recipe != recipe)
			return true;

		var count = 0;
		ItemSlot match = null;

		foreach (var slot in inputSlots)
		{
			if (slot.Empty)
				continue;

			++count;

			if (slot.Itemstack.Id == chiseledBlockId)
				match = slot;
		}

		if (match == null || count > 1)
			return true;

		var materials = (match.Itemstack.Attributes["materials"] as IntArrayAttribute).value;

		// First id is initial block, which could be a variant `ew`, `ud` etc, that should not be obtained in survival
		var initialBlock = sApi.World.GetBlock(materials[0]);
		foreach (var drop in initialBlock.Drops.Take(1))
			initialBlock = drop.ResolvedItemstack?.Block ?? initialBlock;

		recipe.Output = new()
		{
			Code = initialBlock.Code,
			ResolvedItemstack = new(initialBlock),
		};

		return true;
	}

	public override void Dispose()
	{
		harmony.UnpatchAll(harmonyId);
		Recipe = null;
		base.Dispose();
	}
}

