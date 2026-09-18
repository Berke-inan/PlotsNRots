# Run from the repository root. Uses the installed Unity compiler and generated project references.
$ErrorActionPreference = 'Stop'
$weatherRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$weatherVersion = (Get-Content (Join-Path $weatherRoot 'ProjectSettings/ProjectVersion.txt') -First 1).Split(':')[1].Trim()
$weatherEditor = "C:/Program Files/Unity/Hub/Editor/$weatherVersion/Editor/Data"
$weatherOutput = Join-Path $weatherRoot 'Temp/WeatherValidation'
New-Item -ItemType Directory -Force $weatherOutput | Out-Null
function Compile-WeatherAssembly($projectFile, $sources, $outputName, $extraReference) {
    [xml]$project = Get-Content -LiteralPath (Join-Path $weatherRoot $projectFile)
    $references = $project.SelectNodes("//*[local-name()='HintPath']") | ForEach-Object { $_.InnerText } | Where-Object { (Test-Path -LiteralPath $_) -and ([IO.Path]::GetFileName($_) -ne 'Assembly-CSharp.dll') }
    $arguments = @('-nologo', '-target:library', '-langversion:9', '-nostdlib+', '-define:UNITY_EDITOR;UNITY_6000_3_OR_NEWER;ENABLE_INPUT_SYSTEM', ('-out:"' + (Join-Path $weatherOutput $outputName) + '"'))
    $arguments += $references | ForEach-Object { '-r:"' + $_ + '"' }
    if ($extraReference) { $arguments += '-r:"' + $extraReference + '"' }
    $arguments += $sources | ForEach-Object { '"' + $_.FullName + '"' }
    $response = Join-Path $weatherOutput ($outputName + '.rsp')
    $arguments | Set-Content -LiteralPath $response -Encoding utf8
    & (Join-Path $weatherEditor 'NetCoreRuntime/dotnet.exe') (Join-Path $weatherEditor 'DotNetSdkRoslyn/csc.dll') ('@' + $response)
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $outputName" }
}
$weatherSources = Get-ChildItem -LiteralPath (Join-Path $weatherRoot 'Assets') -Recurse -Filter '*.cs'
Compile-WeatherAssembly 'Assembly-CSharp.csproj' ($weatherSources | Where-Object { $_.FullName -notmatch '[\\/]Editor[\\/]' }) 'Assembly-CSharp.dll' $null
Compile-WeatherAssembly 'Assembly-CSharp-Editor.csproj' ($weatherSources | Where-Object { $_.FullName -match '[\\/]Editor[\\/]' }) 'Assembly-CSharp-Editor.dll' (Join-Path $weatherOutput 'Assembly-CSharp.dll')
Write-Output 'Runtime and Editor C# compilation passed. Run the Unity regression menu for engine/serialization/shader import checks.'
