if ($args.Count -lt 1) {
	echo "Provide arguments";
	exit -1;
}

$env:MOD_PATH = $args[0];
$root = $(resolve-path "$($args[0])\..").path;
$env:MOD_NAME = $(split-path $args[0] -leaf);
dotnet watch --non-interactive --project "$root\Launcher" -- $args[1..$args.count];
