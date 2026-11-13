[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Installer,
    [Parameter(Mandatory)][string]$InstallDirectory
)
$ErrorActionPreference = 'Stop'
$installerPath = (Resolve-Path -LiteralPath $Installer).Path
$installDir = [IO.Path]::GetFullPath($InstallDirectory)
$verb = 'HKLM:\Software\Classes\Directory\shell\FolderToISO'
$expectedCommand = '"' + $installDir + '\FolderToISO.exe" --source "%1"'

# Check the fresh-install default, opt-in, removal on upgrade, and uninstall cleanup.
$taskSelections = @($null, '/TASKS=contextmenu', '/TASKS=""', '/TASKS=contextmenu')
for ($index = 0; $index -lt $taskSelections.Count; $index++) {
    $installerArguments = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$installDir`"")
    if ($null -ne $taskSelections[$index]) { $installerArguments += $taskSelections[$index] }
    $result = Start-Process -FilePath $installerPath -ArgumentList $installerArguments -Wait -PassThru -WindowStyle Hidden
    if ($result.ExitCode -notin 0, 3010) { throw "Installer failed: $($result.ExitCode)" }
    if (!(Test-Path -LiteralPath "$installDir/FolderToISO.exe")) { throw 'Installed executable is missing.' }
    if ($index -in 1, 3) {
        if (!(Test-Path -LiteralPath "$verb/command")) { throw 'Selected context-menu registration is missing.' }
        $command = (Get-Item -LiteralPath "$verb/command").GetValue('')
        if ($command -cne $expectedCommand) { throw "Incorrect context-menu command: $command" }
        if ((Get-Item -LiteralPath $verb).GetValue('MultiSelectModel') -ne 'Single') { throw 'Context menu must select a single folder.' }
    }
    elseif (Test-Path -LiteralPath $verb) { throw 'The context menu was registered when the option was unchecked.' }
}
$result = Start-Process -FilePath "$installDir/unins000.exe" -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru -WindowStyle Hidden
if ($result.ExitCode -ne 0) { throw "Uninstaller failed: $($result.ExitCode)" }
if (Test-Path -LiteralPath $verb) { throw 'Uninstall left the context menu registered.' }
Write-Output 'PASS: Context-menu default, opt-in, upgrade opt-out, command quoting, and uninstall cleanup.'
