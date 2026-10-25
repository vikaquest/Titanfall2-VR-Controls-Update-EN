$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $taskRoot 'payload.json'))) { throw 'The private payload.json is not published. This repository contains only the public part of the patcher.' }
$taskCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $taskCompiler)) { throw '.NET Framework C# compiler not found.' }
& $taskCompiler /nologo /define:EN /target:winexe /platform:x64 /optimize+ "/out:$taskRoot\dist\Titanfall2VR-QuestPatch.exe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll "/resource:$taskRoot\payload.json,payload.json" "/resource:$taskRoot\features.json,features.json" "/resource:$taskRoot\messages.json,messages.json" "$taskRoot\QuestPatch.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
