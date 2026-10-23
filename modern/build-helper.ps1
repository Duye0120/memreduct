param([Parameter(Mandatory)][string]$OutputDirectory, [Parameter(Mandatory)][string]$IntermediateDirectory, [string]$DotNet = 'dotnet')
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$IntermediateDirectory = [IO.Path]::GetFullPath($IntermediateDirectory)
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$sdk = & $DotNet --version
if ($LASTEXITCODE -ne 0) { throw 'The existing .NET SDK is required to compile the cleanup helper.' }
$compiler = Join-Path (Split-Path -Parent (Get-Command $DotNet).Source) "sdk/$sdk/Roslyn/bincore/csc.dll"
New-Item -ItemType Directory -Force -Path $OutputDirectory, $IntermediateDirectory | Out-Null
[xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'MemReduct.Fluent.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
$keys = @('WorkingSetCheck.Content','SystemCacheCheck.Content','ModifiedFileCheck.Content','ModifiedPagesCheck.Content',
    'FullStandbyCheck.Content','StandbyCheck.Content','RegistryCheck.Content','CombineCheck.Content','InvalidAreas','PermissionRequired','UserAccessUnavailable')
$maps = foreach ($language in @('en-US', 'zh-CN')) {
    [xml]$document = Get-Content -LiteralPath (Join-Path $PSScriptRoot "Strings/$language/Resources.resw") -Raw
    $map = @{}
    foreach ($key in $keys) { $map[$key] = [string]($document.root.data | Where-Object name -eq $key).value }
    $map
}
function Quote-CSharp([string]$Value) { return '@"' + $Value.Replace('"', '""') + '"' }
$cases = foreach ($key in $keys) { 'case ' + (Quote-CSharp $key) + ': return Chinese ? ' + (Quote-CSharp $maps[1][$key]) + ' : ' + (Quote-CSharp $maps[0][$key]) + ';' }
$operationCases = for ($i = 0; $i -lt 8; $i++) { 'case ' + @(1,2,8,16,32,4,64,128)[$i] + ': return Get(' + (Quote-CSharp $keys[$i]) + ');' }
$source = @"
using System;
[assembly: System.Reflection.AssemblyVersion("$version.0")]
[assembly: System.Reflection.AssemblyFileVersion("$version.0")]
namespace MemReduct.Fluent {
internal static class UiText {
internal static bool Chinese;
internal static string Get(string key) { switch(key) { $($cases -join "`n") default: return key; } }
internal static string OperationName(CleanAreas area) { switch((int)area) { $($operationCases -join "`n") default: throw new ArgumentOutOfRangeException("area"); } }
} }
"@
$generated = Join-Path $IntermediateDirectory 'HelperText.g.cs'
Set-Content -LiteralPath $generated -Value $source -Encoding utf8BOM
$executable = Join-Path $OutputDirectory 'MemReduct.Helper.exe'
$files = @('MemoryEngine.cs','HelperTrust.cs','HelperProtocol.cs','HelperTasks.cs','UserDataAccess.cs','helper/Program.cs') |
    ForEach-Object { Join-Path $PSScriptRoot $_ }
$references = @('mscorlib','System','System.Core','System.Xml','System.Xml.Linq','Microsoft.CSharp') |
    ForEach-Object { '/reference:' + (Join-Path $framework ($_ + '.dll')) }
& $DotNet $compiler /nologo /nostdlib+ /target:winexe /platform:x64 /optimize+ /define:HELPER /langversion:6 @references `
    ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')) ('/out:' + $executable) @files $generated
if ($LASTEXITCODE -ne 0) { throw 'Cleanup helper compilation failed.' }
Set-Content -LiteralPath ($executable + '.config') -Encoding utf8NoBOM -Value '<?xml version="1.0" encoding="utf-8"?><configuration><startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" /></startup></configuration>'
$check = Start-Process -FilePath $executable -ArgumentList '--self-check' -WindowStyle Hidden -Wait -PassThru
if ($check.ExitCode -ne 0) { throw 'Packaged cleanup-helper contract checks failed.' }
Write-Host "Helper ready: $executable ($((Get-Item -LiteralPath $executable).Length) bytes; Windows .NET Framework runtime)"
