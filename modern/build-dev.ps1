param(
    [string]$BuildRoot = 'D:\tmp\memreduct-tray-build',
    [switch]$Restore
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'MemReduct.Fluent.csproj'
$output = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\MemReduct-WinUI3-多语言预览'))
$BuildRoot = [IO.Path]::GetFullPath($BuildRoot)
$dotnet = Join-Path $BuildRoot 'dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnet)) { throw "SDK not found: $dotnet. Pass -BuildRoot pointing to the existing toolchain." }
. (Join-Path $PSScriptRoot 'build-common.ps1')
Assert-BuildOutputsAvailable -Directories @($output)
$env:DOTNET_CLI_HOME = Join-Path $BuildRoot 'cli'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:NUGET_PACKAGES = Join-Path $BuildRoot 'nuget'
$properties = @(
    '-p:Platform=x64',
    ('-p:BaseIntermediateOutputPath=' + (Join-Path $BuildRoot 'obj') + '\'),
    ('-p:OutputPath=' + $output + '\'),
    '-p:AppendTargetFrameworkToOutputPath=false',
    '-p:AppendRuntimeIdentifierToOutputPath=false'
)
$settings = Join-Path $output 'settings.json'
$settingsBefore = if (Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
try {
    if ($Restore -or !(Test-Path -LiteralPath (Join-Path $BuildRoot 'obj\project.assets.json'))) {
        & $dotnet restore $project @properties --locked-mode --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    }
    & $dotnet build $project @properties -c Release --no-restore --nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Incremental build failed. The application was not launched.' }
    if (!(Test-Path -LiteralPath (Join-Path $output 'MemReduct.Fluent.exe'))) { throw 'Build did not produce the expected executable.' }
}
finally {
    $settingsAfter = if (Test-Path -LiteralPath $settings) { (Get-FileHash -LiteralPath $settings).Hash } else { $null }
    if ($settingsBefore -ne $settingsAfter) { throw 'Settings changed during build. Inspect them before running the app; no automatic rollback was attempted.' }
}
Write-Host "Ready: $(Join-Path $output 'MemReduct.Fluent.exe')"
Write-Host 'Settings preserved. Run this executable directly; publish is not needed for development.'
