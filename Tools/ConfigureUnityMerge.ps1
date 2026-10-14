$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    git config --local merge.unityyaml.name 'Unity semantic YAML merge'
    git config --local merge.unityyaml.driver 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File Tools/UnityMergeDriver.ps1 %O %B %A'
    git config --local rerere.enabled true
    git config --local rerere.autoupdate false
} finally { Pop-Location }
