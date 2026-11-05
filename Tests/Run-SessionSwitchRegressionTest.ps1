param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
if (-not $SkipBuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $visualStudio = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -property installationPath
    if (-not $visualStudio) { throw 'Visual Studio MSBuild is required.' }
    $project = Join-Path $workspace 'Veloxap.AddIn.Erwin\Veloxap.AddIn.Erwin.csproj'
    & (Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe') $project /t:Build "/p:Configuration=$Configuration" /p:Platform=x64 /p:RegisterForComInterop=false /nologo /verbosity:quiet /clp:ErrorsOnly
    if ($LASTEXITCODE -ne 0) { throw 'Add-in build failed.' }
}

# All configuration writes target this disposable copy, never the installed add-in.
$outputDirectory = Join-Path $workspace ('Veloxap.AddIn.Erwin\obj\SessionSwitchTest\' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$addin = Join-Path $outputDirectory 'Veloxap.AddIn.Erwin.dll'
Copy-Item -LiteralPath (Join-Path $workspace "Veloxap.AddIn.Erwin\bin\x64\$Configuration\Veloxap.AddIn.Erwin.dll") -Destination $addin
$testExecutable = Join-Path $outputDirectory 'SessionSwitchRegressionTest.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('System.Core.dll', 'System.Configuration.dll', 'System.Net.Http.dll', 'System.Web.Extensions.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll')
$compilerArguments = @('/nologo', '/target:exe', '/platform:x64', "/out:$testExecutable")
$compilerArguments += $references | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$compilerArguments += Join-Path $PSScriptRoot 'SessionSwitchRegressionTest.cs'
& (Join-Path $framework 'csc.exe') @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Session-switch test build failed.' }
@'
<configuration>
  <appSettings>
    <add key="AuthUsername" value="host-old-user" />
    <add key="AuthPassword" value="host-old-password" />
  </appSettings>
</configuration>
'@ | Set-Content -LiteralPath ($testExecutable + '.config') -Encoding UTF8
& $testExecutable $addin
if ($LASTEXITCODE -ne 0) { throw 'Session-switch regression test failed.' }
