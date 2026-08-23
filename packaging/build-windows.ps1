<#
.SYNOPSIS
  Builds the Windows package: two self-contained programs and an installer for them.

.DESCRIPTION
  Produces artifacts/dist:
    Lego2STL-<version>-win-x64.msi   an installer, when the WiX tool is available
    Lego2STL-<version>-win-x64.zip   the same programs, to unpack anywhere

  The zip is always produced. An installer is a convenience; a folder that can be copied to a
  machine and run is what makes the tool portable, and is what any script wants.

  Two programs, deliberately: a console one that can be scripted and write to a pipe, and a
  windowed one that does not flash a console when it starts. One executable cannot be both.

.NOTES
  Needs: the .NET SDK. For the installer also:
      dotnet tool install --global wix --version 6.0.1
  Run on Windows. The Windows build is the only one that can read a document, because the
  text recogniser it uses is part of Windows.
#>

[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [string]$Configuration = 'Release',
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$staging = Join-Path $root 'artifacts\staging\win-x64'
$publish = Join-Path $root 'artifacts\publish\win-x64'
$dist = Join-Path $root 'artifacts\dist'

# The Windows target framework, which is what carries the text recogniser.
$framework = 'net10.0-windows10.0.19041.0'

function Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }

Step "Publishing for win-x64 ($framework)"

Remove-Item $staging, $publish -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $publish, $dist | Out-Null

# Each project publishes into a folder of its own. Publishing two into one folder does not
# work: the second run clears what the first put there, and the package silently loses a
# program. They are gathered afterwards instead.
$projects = [ordered]@{
    'cli' = 'src\Lego2STL.Cli\Lego2STL.Cli.csproj'
    'gui' = 'src\Lego2STL.Gui\Lego2STL.Gui.csproj'
}

foreach ($name in $projects.Keys) {
    $into = Join-Path $staging $name

    dotnet publish (Join-Path $root $projects[$name]) `
        -c $Configuration -f $framework -r win-x64 `
        -p:Version=$Version `
        -o $into --nologo
    if ($LASTEXITCODE -ne 0) { throw "publish failed for $($projects[$name])" }

    Copy-Item (Join-Path $into '*.exe') $publish -Force
}

# Both programs have to be here. Windows compares file names without regard to case, so two
# programs whose names differ only in capitalisation quietly become one. Checked, not assumed.
$expected = 'lego2stl.exe', 'Lego2STL.Gui.exe'
foreach ($name in $expected) {
    if (-not (Test-Path (Join-Path $publish $name))) {
        throw "$name is missing from the package. Do the two programs share a name?"
    }
}

$actual = @(Get-ChildItem $publish -Filter *.exe)
if ($actual.Count -ne $expected.Count) {
    throw "expected $($expected.Count) programs, found $($actual.Count): $($actual.Name -join ', ')"
}

foreach ($file in $actual) {
    Write-Host ("    {0,-20} {1,12:N0} bytes" -f $file.Name, $file.Length)
}

Step 'Packing the zip'
$zip = Join-Path $dist "Lego2STL-$Version-win-x64.zip"
Remove-Item $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zip
Write-Host "    $zip"

if ($SkipInstaller) {
    Step 'Installer skipped'
    exit 0
}

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Warning 'The WiX tool was not found, so no installer was built.'
    Write-Warning 'Install it with:  dotnet tool install --global wix --version 6.0.1'
    exit 0
}

Step 'Building the installer'
$msi = Join-Path $dist "Lego2STL-$Version-win-x64.msi"

# Pinned to the toolset's own version: the extension and the tool are released together, and
# an unpinned add resolves to a newer one the tool refuses.
wix extension add --global WixToolset.UI.wixext/6.0.1
if ($LASTEXITCODE -ne 0) { throw 'could not add the WiX interface extension' }

# Worked out first and quoted: an expression written inline after -d is split into a separate
# argument, and the toolset then reads the licence as another source file.
$source = Join-Path $PSScriptRoot 'windows\Lego2STL.wxs'
$license = Join-Path $PSScriptRoot 'windows\License.rtf'

wix build $source `
    -ext WixToolset.UI.wixext `
    -d "Version=$Version" `
    -d "Publish=$publish" `
    -d "LicenseRtf=$license" `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw 'the installer did not build' }

# The toolset writes a debugging database beside the installer; it is not part of the
# package and only confuses anyone looking at the folder.
Remove-Item (Join-Path $dist '*.wixpdb') -Force -ErrorAction SilentlyContinue

Write-Host "    $msi"
Step 'Done'
