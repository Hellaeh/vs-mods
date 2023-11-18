function test-modinfo($arg) {
	if (-not (test-path "$arg\modinfo.json")) {
		throw [System.IO.FileNotFoundException] """modinfo.json"" not found."
	}
}

function get-mod($arg) {
	$mod = $arg;

	# i hate pwsh
	.{
		if (-not $mod) {
			$mod = $(pwd).path;
		} else {
			$mod = $(resolve-path $mod).path;
		}

		test-modinfo $mod;
	} | out-null;

	return $mod;
}

function get-modtype($arg) {
	$type = "code";

	.{
		test-modinfo $arg;

		$modinfo = cat "$arg\modinfo.json" | convertfrom-json;
		$type = $modinfo.type;
	} | out-null

	return $type;
}
