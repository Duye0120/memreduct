param(
    [string]$BuildRoot = 'D:\tmp\memreduct-tray-build',
    [string]$Compiler,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\..\MemReduct-安装包')
)
$ErrorActionPreference = 'Stop'
$BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
if (!$Compiler) {
    $candidates = @(
        (Join-Path $BuildRoot 'inno\ISCC.exe'),
        (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe')
    )
    $Compiler = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
}
# Check prerequisites before creating a publish copy. No automatic tool installation.
if (!$Compiler -or !(Test-Path -LiteralPath $Compiler -PathType Leaf)) { throw 'Inno Setup 7 is required. Install it manually or pass -Compiler <ISCC.exe>.' }
if (Get-Process -Name 'MemReduct.Fluent' -ErrorAction SilentlyContinue) { throw 'Exit Mem Reduct from its tray menu first.' }
$dotnet = Join-Path $BuildRoot 'dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw 'The existing .NET SDK was not found.' }
$project = Join-Path $PSScriptRoot 'MemReduct.Fluent.csproj'
[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use a three-part stable Version in the project.' }
$parsed = [version]$version
$stage = [IO.Path]::GetFullPath((Join-Path $BuildRoot 'installer-stage'))
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $stage) {
    if ((Get-Item -LiteralPath $stage).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Staging cannot be a symbolic link.' }
    $entries = @(Get-ChildItem -LiteralPath $stage -Force -Recurse)
    if ($entries | Where-Object { $_.Name -like 'settings.json*' -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) }) { throw 'Unexpected settings or symbolic link in staging; inspect it manually.' }
    if ((Split-Path -Parent $stage) -ne $BuildRoot -or (Split-Path -Leaf $stage) -ne 'installer-stage') { throw 'Staging path escaped the build directory.' }
    Remove-Item -LiteralPath $stage -Recurse -Force
}
$preview = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\MemReduct-WinUI3-多语言预览'))
$settingsPath = Join-Path $preview 'settings.json'
$settingsHash = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { $null }
$env:DOTNET_CLI_HOME = Join-Path $BuildRoot 'cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:NUGET_PACKAGES = Join-Path $BuildRoot 'nuget'
try {
    & $dotnet publish $project -c Release --no-restore --nologo -v:minimal '-p:Platform=x64' `
        ('-p:BaseIntermediateOutputPath=' + (Join-Path $BuildRoot 'obj') + '\') `
        ('-p:OutputPath=' + $preview + '\') '-p:AppendTargetFrameworkToOutputPath=false' '-p:AppendRuntimeIdentifierToOutputPath=false' `
        ('-p:PublishDir=' + $stage + '\')
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
} finally {
    $after = if (Test-Path -LiteralPath $settingsPath) { (Get-FileHash -LiteralPath $settingsPath).Hash } else { $null }
    if ($settingsHash -ne $after) { throw 'Settings changed during publish; inspect them before proceeding.' }
}
if (Get-ChildItem -LiteralPath $stage -Filter 'settings.json*' -Recurse) { throw 'User settings must never be packaged.' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
& $Compiler ('/DAppSource=' + $stage) ('/DInstallerOutput=' + $OutputDirectory) ('/DAppVersion=' + $version) `
    ('/DVersionMajor=' + $parsed.Major) ('/DVersionMinor=' + $parsed.Minor) ('/DVersionPatch=' + $parsed.Build) `
    (Join-Path $PSScriptRoot 'installer\MemReductFluent.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$installer = Join-Path $OutputDirectory "MemReductFluent-$version-win-x64-setup.exe"
if (!(Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Installer output missing.' }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath ($installer + '.sha256') -Value "$hash  $([IO.Path]::GetFileName($installer))" -Encoding utf8NoBOM
Write-Host "Ready: $installer"
Write-Host "Release tag: fluent-v$version. Nothing has been uploaded or installed."
