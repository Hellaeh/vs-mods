function test-modinfo($arg) {
	if (-not (test-path "$arg\modinfo.json")) {
		throw [System.IO.FileNotFoundException] """modinfo.json"" not found."
	}
}

function get-modinfo($arg) {
	$modinfo;

	.{
		test-modinfo $arg;

		$modinfo = cat "$arg\modinfo.json" | convertfrom-json;
	} | out-null;

	return $modinfo;
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
		$modinfo = get-modinfo $arg;
		$type = $modinfo.type;
	} | out-null

	return $type;
}
