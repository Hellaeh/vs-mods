function get-mod($arg) {
	$mod = $arg;

	# i hate pwsh
	.{
		if (-not $mod) {
			$mod = $(pwd).path;
		} else {
			$mod = $(resolve-path $mod).path;
		}

		if (-not (test-path "$mod\modinfo.json")) {
			throw [System.IO.FileNotFoundException] """modinfo.json"" not found."
		}
	} | out-null;

	return $mod;
}
