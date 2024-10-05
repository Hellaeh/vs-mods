$ErrorActionPreference = "Stop"

import-module "$PSScriptRoot\utils.psm1" -scope local -force;

$path = get-mod $args[0];
$modinfo = get-modinfo $path;
$modname = $(split-path $path -leaf) -replace '\s+', '';
$modbuild = "$path\release";
$zipname = "$modname-$($modinfo.version).zip";
$zipdest = "releases\$zipname";
$moddest = "$env:APPDATA\VintagestoryData\Mods\$zipname";
$isCode = $modinfo.type -eq "code";

if (test-path $modbuild) {
	rm -recurse "$modbuild\*";
}

if (-not (test-path "releases")) {
	mkdir "releases"
}

$(
	if ($isCode) {
		dotnet build --no-incremental -c release /p:Optimize=true /p:DebugType=PdbOnly $path;
	} else {
		$modbuild = $path;
	}
) && 
	compress-archive "$modbuild\*" -destinationpath $zipdest -force; 

cp $zipdest $moddest;
