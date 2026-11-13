[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$')][string]$Version,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9-]+$')][string]$Publisher,
    [Parameter(Mandatory)][string]$PublishDirectory,
    [Parameter(Mandatory)][string]$RedistPath,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$publish = (Resolve-Path -LiteralPath $PublishDirectory).Path
$redist = (Resolve-Path -LiteralPath $RedistPath).Path
if (!(Test-Path -LiteralPath "$publish/FolderToISO.exe")) { throw 'Publish the Windows application first.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler = Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source
if (!$compiler) {
    $compiler = Get-ChildItem "${env:ProgramFiles(x86)}/Inno Setup*/ISCC.exe", "$env:ProgramFiles/Inno Setup*/ISCC.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (!$compiler) { throw 'Install Inno Setup before building the installer.' }
& $compiler "/DAppVersion=$Version" "/DPublisher=$Publisher" "/DPublishDir=$publish" "/DRedistPath=$redist" "/DOutputDir=$output" "$PSScriptRoot/FolderToISO.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }
