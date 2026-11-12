# Runs solution scripts (tools/solutions/<scene>.txt) one by one in the muted harness and prints a summary.
#   tools/solve_runs.ps1 R-001-1 R-001-2 ...
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Levels)
$index = @{ 'R-001-1'=1; 'R-001-2'=2; 'R-001-3'=3; 'R-001-4'=4; 'R-001-5'=5; 'R-002-1'=6; 'R-002-2'=7; 'R-002-3'=8; 'R-002-4'=9;
            'R-003-1'=10; 'R-003-2'=11; 'R-003-3'=12; 'R-003-4'=13; 'R-Event'=14; 'R-004-1'=15; 'R-004-2'=16; 'R-004-3'=17 }
foreach ($lv in $Levels) {
    $out = "$PSScriptRoot\..\captures\solve\$lv"
    if (Test-Path $out) { Get-ChildItem $out -File | Remove-Item -Force -ErrorAction SilentlyContinue }
    & "$PSScriptRoot\run_harness.ps1" -Scene Assets/Remake/Scenes/RemakeBase.unity -Level $lv -Index $index[$lv] -Scenario script `
        -ScriptFile "$PSScriptRoot\..\tools\solutions\$lv.txt" -Out $out | Out-Null
    $log = Join-Path $out 'unity.log'
    if (-not (Test-Path $log)) { "== $lv :: no run (missing solution file?)"; continue }
    $audio = (Select-String -Path $log -Pattern 'AUDIO-PRECHECK\(edit.*ok=True' -Quiet)
    $res = Select-String -Path $log -Pattern 'level cleared by script' | Select-Object -Last 1
    "== $lv audioMuted=$audio :: $($res.Line -replace '\[Harness\] ','')"
}

