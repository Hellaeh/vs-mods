import-module "$PSScriptRoot\utils.psm1" -scope local -force;

$path = get-mod $args[0];

$modfolder = split-path $path -leaf;

if (test-path "$path\release") {
	rm -recurse "$path\release\*";
} 

&dotnet build $path | out-default;

$root = (resolve-path "$path\..").path;
$vsdata = "$root\vsdata";

if (-not (test-path $vsdata)) {
	throw "No vsdata found";
}

rm "$vsdata\Mods\*";

if (-not (new-item -type junction -path "$vsdata\Mods\$modfolder" -target "$path\release")) {
	throw "Could not make a junction for a $modfolder";
}

start-process pwsh -wait -argumentlist "-file ""$root\launch.ps1""", "--dataPath $vsdata", "--tracelog", "-o DebugMods", "-p creativebuilding";

