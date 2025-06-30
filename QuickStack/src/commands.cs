using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

using Container = Vintagestory.GameContent.BlockEntityContainer;

namespace HelQuickStack;

public static class ChatCommands
{
	public static void Register(ICoreAPI api)
	{
		var command = api.ChatCommands.GetOrCreate("quickstack").WithAlias("qs");

		if (api is ICoreServerAPI)
		{
			command
				.RequiresPrivilege(Privilege.root)

				.BeginSubCommand("maxRadius")
					.WithDescription("Sets or prints \"MaxRadius\"")
					.WithArgs(api.ChatCommands.Parsers.OptionalWord("radius"))
					.HandleWith(MaxRadiusHandler)
					.EndSubCommand();

			return;
		}

		command
			.WithDescription("Allows you to add/remove containers you are currently looking at to/from whitelist/blacklist respectively. Also a prefix for other subcommands.")
			.RequiresPlayer()
			.HandleWith(DefaultHandler)

			.BeginSubCommand("mode")
				.WithDescription("Toggle between whitelist and blacklist modes")
				.HandleWith(ModeHandler)
				.EndSubCommand()

			.BeginSubCommand("add")
				.WithDescription("Add container you are looking at to whitelist/blacklist")
				.HandleWith(AdderHandler)
				.EndSubCommand()

			.BeginSubCommand("remove")
				.WithDescription("Remove container you are looking at from whitelist/blacklist")
				.HandleWith(RemoverHandler)
				.EndSubCommand()

			.BeginSubCommand("radius")
				.WithDescription("Sets or prints \"Radius\"")
				.WithArgs(api.ChatCommands.Parsers.OptionalWord("radius"))
				.HandleWith(RadiusHandler)
				.EndSubCommand();
	}

	static Container? SelectedContainer()
		=> Core.cApi!.World.Player?.CurrentBlockSelection?.Position is BlockPos pos
			? Core.cApi!.World.BlockAccessor.GetBlockEntity(pos) as Container
			: null;

	static TextCommandResult ModeHandler(TextCommandCallingArgs args)
	{
		Core.CConfig!.Mode = Core.CConfig!.Mode == Mode.Blacklist ? Mode.Whitelist : Mode.Blacklist;
		return TextCommandResult.Success($"Switched to {Core.CConfig!.Mode} mode");
	}

	static TextCommandResult DefaultHandler(TextCommandCallingArgs _)
	{
		if (SelectedContainer() is not Container container)
			return Result.NoContainer;

		var classname = container.InventoryClassName;
		var rules = Core.CConfig!.GetRules();

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

		Core.CConfig!.GetRules().TryAdd(classname, new());

		return Result.Added(classname);
	}

	static TextCommandResult RemoverHandler(TextCommandCallingArgs _)
	{
		if (SelectedContainer() is not Container container)
			return Result.NoContainer;

		var classname = container.InventoryClassName;

		Core.CConfig!.GetRules().Remove(classname);

		return Result.Removed(classname);
	}

	static TextCommandResult MaxRadiusHandler(TextCommandCallingArgs args)
	{
		if (!int.TryParse((string)args.LastArg, out var value))
			return TextCommandResult.Success($"\"MaxRadius\" is {Core.SConfig.MaxRadius}");

		// cuz well do a round trip to server and back - we don't know if value is valid
		var sc = new ServerConfig { MaxRadius = value };
		Core.cApi?.Network.GetChannel(Consts.Channel).SendPacket<MaxRadiusPacket>(new() { Payload = sc.MaxRadius });

		return TextCommandResult.Success($"\"MaxRadius\" is {sc.MaxRadius}");
	}

	static TextCommandResult RadiusHandler(TextCommandCallingArgs args)
	{
		if (!int.TryParse((string)args.LastArg, out var value))
			return TextCommandResult.Success($"\"Radius\" is {Core.CConfig!.Radius}");

		Core.CConfig.Radius = value;

		return TextCommandResult.Success($"\"Radius\" is {Core.CConfig.Radius}");
	}

	static class Result
	{
		public static TextCommandResult NoContainer = TextCommandResult.Error("No container selected");

		public static TextCommandResult Added(string classname) => TextCommandResult.Success(Format(Op.Added, classname));
		public static TextCommandResult Removed(string classname) => TextCommandResult.Success(Format(Op.Removed, classname));

		static string Format(Op op, string classname) =>
			$"{(op == Op.Added ? "Added" : "Removed")} \"{classname}\" {(op == Op.Added ? "to" : "from")} {Core.CConfig!.Mode}";

		enum Op
		{
			Added,
			Removed
		}
	}
}
