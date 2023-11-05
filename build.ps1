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
$moddest = "$($env:APPDATA)\VintagestoryData\Mods\$($modname).zip";

dotnet build --no-incremental $path &&
	compress-archive $path\release\* -destinationpath "$moddest" -force;
