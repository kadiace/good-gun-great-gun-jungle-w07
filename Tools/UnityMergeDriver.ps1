param([Parameter(Mandatory=$true)][string]$Base,[Parameter(Mandatory=$true)][string]$Theirs,[Parameter(Mandatory=$true)][string]$Ours)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$version = (Select-String -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$').Matches[0].Groups[1].Value
$merger = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$version/Editor/Data/Tools/UnityYAMLMerge.exe"
if (-not (Test-Path -LiteralPath $merger)) { throw "UnityYAMLMerge for $version is missing: $merger" }
# Git's temporary filenames lack an extension, which UnityYAMLMerge needs.
$mergeFolder = Join-Path $projectRoot ('Temp/UnityMerge/' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $mergeFolder -Force | Out-Null
$baseFile = Join-Path $mergeFolder 'Base.unity'
$theirFile = Join-Path $mergeFolder 'Theirs.unity'
$ourFile = Join-Path $mergeFolder 'Ours.unity'
$resultFile = Join-Path $mergeFolder 'Result.unity'
Copy-Item -LiteralPath $Base -Destination $baseFile
Copy-Item -LiteralPath $Theirs -Destination $theirFile
Copy-Item -LiteralPath $Ours -Destination $ourFile
& $merger merge -p -h --fallback none $baseFile $theirFile $ourFile $resultFile
$mergeExitCode = $LASTEXITCODE
if ($mergeExitCode -eq 0 -and (Test-Path -LiteralPath $resultFile)) { Copy-Item -LiteralPath $resultFile -Destination $Ours }
exit $mergeExitCode
