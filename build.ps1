import-module "$PSScriptRoot\utils.psm1" -scope local -force;

$path = get-mod $args[0];

$modname = $(split-path $path -leaf) -replace '\s+', '';
$modbuild = "$path\release";
$moddest = "$env:APPDATA\VintagestoryData\Mods\$modname.zip";

rm -recurse "$modbuild\*";

dotnet build --no-incremental $path &&
	compress-archive "$modbuild\*" -destinationpath "$moddest" -force;
