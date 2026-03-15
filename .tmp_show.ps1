$base = "c:\Users\Diego\Projects\Everdawn"

# Check all files for remaining summary tags and show context
$roots = @(
    "$base\GameCore.Tests",
    "$base\GameCore.Scenarios",
    "$base\BattleSandbox.Web\Battle"
)

foreach ($root in $roots) {
    $files = Get-ChildItem $root -Recurse -Filter "*.cs"
    foreach ($f in $files) {
        $lines = Get-Content $f.FullName
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match '/// <summary>') {
                $context = $lines[$i..([Math]::Min($i+4, $lines.Count-1))]
                Write-Host "=== $($f.Name):$($i+1) ==="
                $context | ForEach-Object { Write-Host "  $_" }
                Write-Host ""
            }
        }
    }
}
