using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using Container = Vintagestory.GameContent.BlockEntityContainer;

namespace HelQuickStack;

public static class ChatCommands
{
	static ICoreClientAPI? cApi;

	public static void Register(ICoreClientAPI api)
	{
		api.ChatCommands.GetOrCreate("quickstack")
			.WithAlias("qs")
			.WithDescription("Allows you to add/remove containers you are currently looking at to whitelist/blacklist respectively")
			.RequiresPlayer()
			.HandleWith(DefaultHandler)

			.BeginSubCommand("mode")
				.WithDescription("Toggle between whitelist/blacklist modes")
				.HandleWith(ModeHandler)
				.EndSubCommand()

			.BeginSubCommand("add")
				.WithDescription("Add container you are currently looking at to whitelist/blacklist")
				.HandleWith(AdderHandler)
				.EndSubCommand()

			.BeginSubCommand("remove")
				.WithDescription("Remove container you are currently looking at from whitelist/blacklist")
				.HandleWith(RemoverHandler)
				.EndSubCommand();

		cApi = api;
	}

	static Container? SelectedContainer()
		=> cApi?.World.Player?.CurrentBlockSelection?.Position is BlockPos pos
			? cApi?.World.BlockAccessor.GetBlockEntity(pos) as Container
			: null;

	static TextCommandResult ModeHandler(TextCommandCallingArgs args)
	{
		Core.Config!.Mode = Core.Config!.Mode == Mode.Blacklist ? Mode.Whitelist : Mode.Blacklist;
		return TextCommandResult.Success($"Switched to {Core.Config!.Mode} mode");
	}

	static TextCommandResult DefaultHandler(TextCommandCallingArgs _)
	{
		if (SelectedContainer() is not Container container)
			return Result.NoContainer;

		var classname = container.InventoryClassName;
		var rules = Core.Config!.GetRules();

		if (rules.Remove(classname))
			return Result.Removed(classname);

		rules.Add(classname, new());
		return Result.Added(classname);
	}

	static TextCommandResult AdderHandler(TextCommandCallingArgs _)
	{
		if (SelectedContainer() is not Container container)
			return Result.NoContainer;

		var classname = container.InventoryClassName;

		Core.Config!.GetRules().TryAdd(classname, new());

		return Result.Added(classname);
	}

	static TextCommandResult RemoverHandler(TextCommandCallingArgs _)
	{
		if (SelectedContainer() is not Container container)
			return Result.NoContainer;

		var classname = container.InventoryClassName;

		Core.Config!.GetRules().Remove(classname);

		return Result.Removed(classname);
	}

	public static void Dispose()
	{
		cApi = null;
	}

	static class Result
	{
		public static TextCommandResult NoContainer = TextCommandResult.Error("No container selected");

		public static TextCommandResult Added(string classname) => TextCommandResult.Success(Format(Op.Added, classname));
		public static TextCommandResult Removed(string classname) => TextCommandResult.Success(Format(Op.Removed, classname));

		static string Format(Op op, string classname) =>
			$"{(op == Op.Added ? "Added" : "Removed")} \"{classname}\" {(op == Op.Added ? "to" : "from")} {Core.Config!.Mode}";

		enum Op
		{
			Added,
			Removed
		}
	}
}
