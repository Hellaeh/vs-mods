if ($args.Count -ne 1) { 
	echo "Provide mod folder";
	exit -1; 
}

$path = ($args[0] -replace '\.', '') -replace '\\', '';

if (-not (test-path $path)) {
	echo "Mod folder does not exist";
	exit -1;
}

$modname = $path -replace '\s+', '';
$modbuild = "$path\release";
$moddest = "$env:APPDATA\VintagestoryData\Mods\$modname.zip";

rm -recurse -force $modbuild &&
	dotnet build --no-incremental $path &&
	compress-archive "$modbuild\*" -destinationpath "$moddest" -force;
