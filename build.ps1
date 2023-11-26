import-module "$PSScriptRoot\utils.psm1" -scope local -force;

$path = get-mod $args[0];
$modinfo = get-modinfo $path;
$modname = $(split-path $path -leaf) -replace '\s+', '';
$modbuild = "$path\release";
$moddest = "$env:APPDATA\VintagestoryData\Mods\$modname-$($modinfo.version).zip";
$isCode = $modinfo.type -eq "code";

if (test-path $modbuild) {
	rm -recurse "$modbuild\*";
}

$(
	if ($isCode) {
		dotnet build --no-incremental -c release /p:Optimize=true /p:DebugType=None /p:DebugSymbols=false $path;
	} else {
		$modbuild = $path;
	}
) && 
	compress-archive "$modbuild\*" -destinationpath "$moddest" -force;

