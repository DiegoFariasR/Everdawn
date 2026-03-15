$f = "c:\Users\Diego\Projects\Everdawn\GameCore.Tests\Battle\ThermalTests.cs"
$raw = Get-Content $f -Raw

Write-Host "File length: $($raw.Length)"
Write-Host "Has CRLF: $($raw.Contains("`r`n"))"
Write-Host "Summary count: $([Text.RegularExpressions.Regex]::Matches($raw, '/// <summary>').Count)"

# Show what comes after the first summary close
$m = [Text.RegularExpressions.Regex]::Match($raw, '/// <summary>.*?</summary>(.{0,80})', 'Singleline')
if ($m.Success) {
    Write-Host "After first close tag: [$($m.Groups[1].Value.Replace("`r","<CR>").Replace("`n","<LF>"))]"
}

# Test pattern
$p = '[ \t]*/// <summary>.*?</summary>\r?\n(?=[ \t]*(?:(?:public|private|protected|internal|sealed|abstract|static|unsafe)\s+)*(?:class|record|interface|enum)\b)'
$matches = [Text.RegularExpressions.Regex]::Matches($raw, $p, 'Singleline')
Write-Host "Pattern matches: $($matches.Count)"
foreach ($m in $matches) { Write-Host "  Match at $($m.Index): [$($m.Value.Substring(0,[Math]::Min(60,$m.Length)).Replace("`r","<CR>").Replace("`n","<LF>"))]" }
