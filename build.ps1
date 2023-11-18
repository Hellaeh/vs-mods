import-module "$PSScriptRoot\utils.psm1" -scope local -force;

$path = get-mod $args[0];
$isCode = $(get-modtype $path) -eq "code";

$modname = $(split-path $path -leaf) -replace '\s+', '';
$modbuild = "$path\release";
$moddest = "$env:APPDATA\VintagestoryData\Mods\$modname.zip";

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

