$base = "c:\Users\Diego\Projects\Everdawn"

$batchFiles = @(
    (Get-ChildItem "$base\GameCore.Tests" -Recurse -Filter "*.cs").FullName
    "$base\GameCore.Scenarios\AllArchetypesScenario.cs",
    "$base\GameCore.Scenarios\BattleSandboxEngine.cs",
    "$base\GameCore.Scenarios\SampleScenario.cs",
    "$base\GameCore.Scenarios\SampleScenarioWatch.cs",
    "$base\GameCore.Scenarios\WeaponArchetypeScenario.cs",
    "$base\GameCore.Scenarios\SpellArchetypeScenario.cs",
    "$base\GameCore.Scenarios\ScenarioRegistry.cs",
    "$base\BattleSandbox.Web\Battle\BattleHelpers.cs"
)

# Removes /// <summary>...(multi or single line)...</summary> blocks that appear
# immediately before a class/record/interface/enum declaration.
$pattern = '[ \t]*/// <summary>.*?</summary>\r?\n(?=[ \t]*(?:(?:public|private|protected|internal|sealed|abstract|static|unsafe)\s+)*(?:class|record|interface|enum)\b)'
$opts = [System.Text.RegularExpressions.RegexOptions]::Singleline

$changed = 0
foreach ($f in $batchFiles) {
    if (-not (Test-Path $f)) { Write-Host "MISSING: $f"; continue }
    $old = [System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8)
    $new = [System.Text.RegularExpressions.Regex]::Replace($old, $pattern, '', $opts)
    if ($old -ne $new) {
        [System.IO.File]::WriteAllText($f, $new, [System.Text.Encoding]::UTF8)
        $changed++
        $before = [System.Text.RegularExpressions.Regex]::Matches($old, '/// <summary>').Count
        $after  = [System.Text.RegularExpressions.Regex]::Matches($new, '/// <summary>').Count
        Write-Host "Cleaned $([System.IO.Path]::GetFileName($f)): $before -> $after summary tags"
    }
}
Write-Host ""
Write-Host "Total changed: $changed files"
Write-Host ""
Write-Host "Remaining summary tags in all targets:"
$allTargets = $batchFiles + @(
    "$base\GameCore.Scenarios\IBattleSandboxEngine.cs",
    "$base\GameCore.Scenarios\IBattleScenario.cs",
    "$base\GameCore.Scenarios\IRegressionScenario.cs"
)
foreach ($f in $allTargets) {
    if (-not (Test-Path $f)) { continue }
    $n = [System.Text.RegularExpressions.Regex]::Matches([System.IO.File]::ReadAllText($f), '/// <summary>').Count
    if ($n -gt 0) { Write-Host "  $n $([System.IO.Path]::GetFileName($f))" }
}
