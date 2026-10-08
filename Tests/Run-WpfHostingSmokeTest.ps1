param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$visualStudio = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $visualStudio) { throw 'Visual Studio with the C++ build tools is required.' }
& (Join-Path $visualStudio 'Common7\Tools\Launch-VsDevShell.ps1') -VsInstallationPath $visualStudio -Arch amd64 -HostArch amd64 -SkipAutomaticLocation -NoLogo
$project = Join-Path $workspace 'Veloxap.AddIn.Erwin\Veloxap.AddIn.Erwin.csproj'
& (Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe') $project /t:Build "/p:Configuration=$Configuration" /p:Platform=x64 /p:RegisterForComInterop=false /nologo /verbosity:quiet /clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) { throw 'Add-in build failed.' }

$outputDirectory = Join-Path $workspace 'Veloxap.AddIn.Erwin\obj\HostingSmokeTest'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$testLibrary = Join-Path $outputDirectory 'WpfHostingSmokeTest.dll'
$nativeHost = Join-Path $outputDirectory 'NativeClrHost.exe'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll', 'WPF\WindowsFormsIntegration.dll')
$compilerArguments = @('/nologo', '/target:library', '/platform:x64', "/out:$testLibrary")
$compilerArguments += $references | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$compilerArguments += Join-Path $PSScriptRoot 'WpfHostingSmokeTest.cs'
& (Join-Path $framework 'csc.exe') @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Managed test library build failed.' }
& cl.exe /nologo /EHsc "/Fe:$nativeHost" "/Fo:$outputDirectory\NativeClrHost.obj" (Join-Path $PSScriptRoot 'NativeClrHost.cpp') /link /MANIFEST:NO
if ($LASTEXITCODE -ne 0) { throw 'Native CLR host build failed.' }

$addin = Join-Path $workspace "Veloxap.AddIn.Erwin\bin\x64\$Configuration\Veloxap.AddIn.Erwin.dll"
foreach ($mode in @('baseline', 'preserved', 'system-aware', 'per-monitor-v2')) {
    & $nativeHost $testLibrary $addin $mode
    if ($LASTEXITCODE -ne 0) { throw "WPF hosting test failed: $mode" }
}
