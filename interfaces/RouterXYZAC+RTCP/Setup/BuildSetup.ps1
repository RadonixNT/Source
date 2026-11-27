#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+(\.\d+)?$')]
    [string] $Version = '1.0.0',
    [string] $InnoSetupCompiler,
    [string] $OutputDirectory = (Join-Path $PSScriptRoot 'Output')
)

$ErrorActionPreference = 'Stop'
$interface = 'RouterXYZAC+RTCP'
$delivery = Split-Path -Parent $PSScriptRoot
$project = Join-Path $delivery "$interface/$interface.csproj"
$config = Join-Path $delivery 'NuGet.Config'
$definition = Join-Path $PSScriptRoot 'Setup.iss'

if (!$InnoSetupCompiler) {
    $InnoSetupCompiler = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source
}
if (!$InnoSetupCompiler) {
    $programFiles = [Environment]::GetFolderPath('ProgramFilesX86')
    $InnoSetupCompiler = Join-Path $programFiles 'Inno Setup 6/ISCC.exe'
}
if (!(Test-Path -LiteralPath $InnoSetupCompiler -PathType Leaf)) {
    throw 'The Inno Setup compiler was not found. Install Inno Setup 6 or pass its ISCC.exe path.'
}
$InnoSetupCompiler = (Resolve-Path -LiteralPath $InnoSetupCompiler).Path
if (!(Test-Path -LiteralPath $project -PathType Leaf) -or !(Test-Path -LiteralPath $config -PathType Leaf) -or
    !(Test-Path -LiteralPath (Join-Path $delivery 'Reference/index.html') -PathType Leaf) -or
    !(Get-ChildItem -LiteralPath (Join-Path $delivery 'NT-ESI') -Filter '*.xml' -File -ErrorAction SilentlyContinue)) {
    throw 'The customer source delivery is incomplete. Extract the full delivery ZIP.'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
$work = Join-Path $temporaryRoot ('Radonix-CustomerSetup-' + [guid]::NewGuid().ToString('N'))
$payload = Join-Path $work 'app'
$artifacts = Join-Path $work 'build'
[IO.Directory]::CreateDirectory($work) | Out-Null
try {
    & dotnet restore $project --configfile $config --artifacts-path $artifacts --nologo -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw "The interface restore failed (exit code $LASTEXITCODE)." }
    & dotnet publish $project -c Release --no-restore --nologo --artifacts-path $artifacts -o $payload -p:Platform=x64 -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "The interface publish failed (exit code $LASTEXITCODE)." }

    foreach ($executable in "$interface.exe",'NT-Core.exe','NT-EniBuilder.exe','NT-EniScanner.exe') {
        if (!(Test-Path -LiteralPath (Join-Path $payload $executable) -PathType Leaf)) {
            throw "The installer payload is missing $executable."
        }
    }
    [IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    & $InnoSetupCompiler "/DAppName=$interface" "/DAppVersion=$Version" "/DPayloadDir=$payload" "/O$OutputDirectory" $definition
    if ($LASTEXITCODE -ne 0) { throw "The installer compilation failed (exit code $LASTEXITCODE)." }
} finally {
    $target = [IO.Path]::GetFullPath($work)
    if ([IO.Path]::GetDirectoryName($target) -eq $temporaryRoot -and
        [IO.Path]::GetFileName($target) -match '^Radonix-CustomerSetup-[a-f0-9]{32}$' -and
        [IO.Directory]::Exists($target)) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}
