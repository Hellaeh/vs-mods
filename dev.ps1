import-module "$PSScriptRoot\utils.psm1" -scope local -force;

$path = get-mod $args[0];
$isCode = $(get-modtype $path) -eq "code";

$modname = split-path $path -leaf;
$modrelease = if ($isCode) { "$path\release" } else { $path };

if ($isCode -and -not (test-path "$modrelease")) {
	mkdir "$modrelease";
}

$root = (resolve-path "$path\..").path;
$vsdata = "$root\vsdata";

if (-not (test-path $vsdata)) {
	throw "No ""vsdata"" found";
}

rm "$vsdata\Mods\*";

if (-not (new-item -type junction -path "$vsdata\Mods\$modname" -target "$modrelease")) {
	throw "Could not make a junction for a $modname";
}

&"$root\watch.ps1" $path --dataPath $vsdata --tracelog -o DebugMods -p creativebuilding

