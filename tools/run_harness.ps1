# Runs the Chronos remake verification harness in background (batch-mode) Play Mode.
# Audio is muted and verified inside Unity before any scene is opened (see HarnessRunner.cs).
param(
    [string]$Scene = "Assets/Scenes/LevelBaseScene.unity",
    [string]$Level = "L-001-3",
    [string]$Scenario = "solve",
    [string]$Out = "$PSScriptRoot\..\captures\run",
    [string]$ScriptFile = "",
    [int]$Index = 3,
    [switch]$Campaign,
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe"
)
$project = (Resolve-Path "$PSScriptRoot\..\Chronos").Path
New-Item -ItemType Directory -Force $Out | Out-Null
$Out = (Resolve-Path $Out).Path
$log = Join-Path $Out "unity.log"
$argList = @(
    '-batchmode', '-projectPath', $project, '-executeMethod', 'HarnessRunner.Run',
    '-remakeHarness', '-harnessScene', $Scene, '-harnessLevel', $Level,
    '-harnessScenario', $Scenario, '-harnessOut', $Out, '-logFile', $log)
$argList += @('-harnessIndex', "$Index")
if ($Campaign) { $argList += @('-harnessCampaign') }
if ($ScriptFile) { $argList += @('-harnessScript', (Resolve-Path $ScriptFile).Path) }
$p = Start-Process -FilePath $Unity -PassThru -Wait -ArgumentList $argList
Select-String -Path $log -Pattern '\[Harness\]|error CS|Exception' | ForEach-Object { $_.Line }
"exit code: $($p.ExitCode)"
exit $p.ExitCode
