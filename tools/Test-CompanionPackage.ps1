param([string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\2021.3.16f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$npcRepo = Split-Path $PSScriptRoot -Parent
$npcOutput = Join-Path $npcRepo 'Logs/CompanionSmoke'
$npcTemplate = Join-Path $PSScriptRoot 'FrameworkSmoke'
$npcSample = Join-Path $npcRepo 'FrameworkPackages/com.mysterygame.companion-reasoning/Samples~/RepairWorkshop'
if (!(Test-Path -LiteralPath $UnityPath)) { throw 'Unity editor not found. Supply -UnityPath.' }
# Only copy fixture files into this known test project. No deletes or user-project rewrites.
New-Item -ItemType Directory -Force -Path $npcOutput | Out-Null
foreach ($npcSource in Get-ChildItem -LiteralPath $npcTemplate -Recurse -File) {
    $npcRelative = $npcSource.FullName.Substring($npcTemplate.Length).TrimStart('\', '/')
    $npcTarget = Join-Path $npcOutput $npcRelative
    New-Item -ItemType Directory -Force -Path (Split-Path $npcTarget -Parent) | Out-Null
    Copy-Item -LiteralPath $npcSource.FullName -Destination $npcTarget -Force
}
$npcWorkshop = Join-Path $npcOutput 'Assets/Workshop'
New-Item -ItemType Directory -Force -Path $npcWorkshop | Out-Null
foreach ($npcSource in Get-ChildItem -LiteralPath $npcSample -File) {
    Copy-Item -LiteralPath $npcSource.FullName -Destination (Join-Path $npcWorkshop $npcSource.Name) -Force
}
foreach ($npcPlatform in @('EditMode', 'PlayMode')) {
    $npcXml = Join-Path $npcOutput ($npcPlatform + '.xml')
    $npcLog = Join-Path $npcOutput ($npcPlatform + '.log')
    $npcStarted = Get-Date
    $npcProcess = Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode', '-nographics',
        '-job-worker-count', '2', '-projectPath', ('"' + $npcOutput + '"'),
        '-runTests', '-testPlatform', $npcPlatform, '-testResults', ('"' + $npcXml + '"'),
        '-logFile', ('"' + $npcLog + '"')) -WindowStyle Hidden -PassThru
    $npcProcess.WaitForExit()
    if (!(Test-Path -LiteralPath $npcXml) -or (Get-Item -LiteralPath $npcXml).LastWriteTime -lt $npcStarted) {
        throw "No fresh test results. See $npcLog"
    }
    [xml]$npcResults = Get-Content -LiteralPath $npcXml
    $npcRun = $npcResults.'test-run'
    Write-Output "$npcPlatform total=$($npcRun.total) passed=$($npcRun.passed) failed=$($npcRun.failed) skipped=$($npcRun.skipped)"
    if ($npcRun.result -ne 'Passed') { throw "Test failed. See $npcXml" }
}
