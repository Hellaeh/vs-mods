$ErrorActionPreference = "Stop"

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

rm "$vsdata\Mods\*" -r;

&"$root\watch.ps1" $path --dataPath $vsdata --tracelog -o DebugMods -p creativebuilding

