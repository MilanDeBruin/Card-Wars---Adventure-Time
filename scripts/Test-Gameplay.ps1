param([string]$UnityPath = $env:UNITY_EDITOR_PATH)

$ErrorActionPreference = 'Stop'
$projectPath = Split-Path $PSScriptRoot -Parent
$versionText = Get-Content -LiteralPath (Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt') -Raw
$version = [regex]::Match($versionText, 'm_EditorVersion:\s*(\S+)').Groups[1].Value
if (!$UnityPath) {
    $UnityPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if (!(Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $version was not found. Pass -UnityPath or set UNITY_EDITOR_PATH."
}

$logDirectory = Join-Path $projectPath 'Logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$logPath = Join-Path $logDirectory 'GameplayRegression-unity.log'
$resultPath = Join-Path $logDirectory 'GameplayRegression.xml'
# Never accept a successful report left over from an earlier invocation.
if (Test-Path -LiteralPath $resultPath) { Remove-Item -LiteralPath $resultPath }

$unityArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'CardWars.Tests.GameplayRegressionAutomation.RunBatch',
    '-logFile', ('"' + $logPath + '"'))
$process = Start-Process -FilePath $UnityPath -ArgumentList $unityArguments -PassThru -Wait -WindowStyle Hidden
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $resultPath)) {
    throw "Gameplay tests did not finish successfully (Unity exit $($process.ExitCode)). See $logPath. If this project is already open, use Tools > Card Wars > Run Gameplay Regression Tests in that editor."
}
[xml]$results = Get-Content -LiteralPath $resultPath -Raw
$suite = $results.'test-suite'
if (!$suite -or $suite.result -ne 'Passed' -or [int]$suite.total -lt 1 -or
    [int]$suite.failed -gt 0 -or [int]$suite.skipped -gt 0 -or [int]$suite.inconclusive -gt 0) {
    throw "Gameplay regressions failed or were skipped. See $resultPath."
}
Write-Output "Gameplay regression: $($suite.passed) passed. Report: $resultPath"
