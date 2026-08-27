<#
.SYNOPSIS
  Builds the Windows package: an installer that fetches .NET 10 when it is missing, and a zip.

.DESCRIPTION
  Produces artifacts/dist:
    Lego2STL-<version>-win-x64.exe   the installer, when the WiX toolset is available
    Lego2STL-<version>-win-x64.zip   the same programs in a folder, to unpack anywhere

  The zip is always produced. An installer is a convenience; a folder that can be copied to a
  machine and run is what makes the tool portable, and is what any script wants. Both need
  .NET 10 on the machine - the installer puts it there, the zip expects it.

  Two programs, deliberately: a console one that can be scripted and write to a pipe, and a
  windowed one that does not flash a console when it starts. One executable cannot be both.
  They publish separately and are then gathered into one folder, so everything they both use
  is carried once rather than twice.

.NOTES
  Needs the .NET SDK. For the installer also:
      dotnet tool install --global wix --version 6.0.1
      wix extension add --global WixToolset.UI.wixext/6.0.1
      wix extension add --global WixToolset.Netfx.wixext/6.0.1
      wix extension add --global WixToolset.BootstrapperApplications.wixext/6.0.1
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
$payload = Join-Path $root 'artifacts\publish\win-x64'
$dist = Join-Path $root 'artifacts\dist'

# The Windows target framework, which is what carries the text recogniser.
$framework = 'net10.0-windows10.0.19041.0'

# An installer that grew past this is carrying the runtime again, which is the one thing this
# packaging exists to stop. The measured payload packs to 24.4 MB; the ceiling leaves room to
# grow without leaving room to regress.
$ceilingMb = 40

# The extension that provides the standard installer window. WiX 6 renamed it: added under the
# old name, WixToolset.Bal.wixext, it installs a copy the toolset then reports as damaged.
$extensions = @(
    'WixToolset.UI.wixext/6.0.1'
    'WixToolset.Netfx.wixext/6.0.1'
    'WixToolset.BootstrapperApplications.wixext/6.0.1'
)

function Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }

Step "Publishing for win-x64 ($framework)"

Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $payload -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $payload, $dist | Out-Null

# Each project publishes into a folder of its own. Publishing two into one folder does not
# work: the second run clears what the first put there. They are gathered afterwards instead,
# which is also what lets them share one copy of everything they both use.
foreach ($name in 'Cli', 'Gui') {
    dotnet publish (Join-Path $root "src\Lego2STL.$name\Lego2STL.$name.csproj") `
        -c $Configuration -f $framework -r win-x64 `
        -p:Version=$Version `
        -o (Join-Path $staging $name) --nologo
    if ($LASTEXITCODE -ne 0) { throw "publish failed for Lego2STL.$name" }
}

Copy-Item (Join-Path $staging 'Cli\*') $payload -Recurse -Force
Copy-Item (Join-Path $staging 'Gui\*') $payload -Recurse -Force

# Debug databases are published beside the assemblies and are of no use to anyone installing
# this. The Skia one alone is 85 MB, more than the rest of the payload together.
Get-ChildItem $payload -Recurse -Filter *.pdb | Remove-Item -Force

# Both programs have to be here. Windows compares file names without regard to case, so two
# programs whose names differ only in capitalisation quietly become one. Checked, not assumed.
foreach ($name in 'lego2stl.exe', 'Lego2STL.Gui.exe') {
    if (-not (Test-Path (Join-Path $payload $name))) {
        throw "$name is missing from the payload. Do the two programs share a name?"
    }
}

$payloadFiles = @(Get-ChildItem $payload -Recurse -File)
$payloadMb = [math]::Round((($payloadFiles | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host ("    {0} files, {1} MB" -f $payloadFiles.Count, $payloadMb)

Step 'Packing the zip'
$zip = Join-Path $dist "Lego2STL-$Version-win-x64.zip"
Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $payload '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host ("    {0}  ({1} MB)" -f $zip, [math]::Round(((Get-Item $zip).Length / 1MB), 1))

if ($SkipInstaller) {
    Step 'Installer skipped'
    exit 0
}

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Warning 'The WiX toolset was not found, so no installer was built.'
    Write-Warning 'Install it with:  dotnet tool install --global wix --version 6.0.1'
    exit 0
}

# Pinned to the toolset's own version: the extension and the tool are released together, and
# an unpinned add resolves to a newer one the tool refuses.
foreach ($extension in $extensions) {
    wix extension add --global $extension
    if ($LASTEXITCODE -ne 0) { throw "could not add $extension" }
}

# ---- The application, as an installer that needs no administrator ----------------------

Step 'Building the application installer'

# An intermediate, not something to hand anyone: on its own it would install programs that
# cannot start on a machine without .NET. What people download is the bundle below.
$msi = Join-Path $staging "Lego2STL-$Version-win-x64.msi"

# Worked out first and quoted: an expression written inline after -d is split into a separate
# argument, and the toolset then reads the licence as another source file.
$license = Join-Path $PSScriptRoot 'windows\License.rtf'

wix build (Join-Path $PSScriptRoot 'windows\Lego2STL.wxs') `
    -ext WixToolset.UI.wixext `
    -d "Version=$Version" `
    -d "Publish=$payload" `
    -d "LicenseRtf=$license" `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw 'the application installer did not build' }

# ---- The bundle, which puts .NET in place first when it has to -------------------------

Step 'Building the installer'

$pin = Get-Content (Join-Path $PSScriptRoot 'runtime.json') -Raw | ConvertFrom-Json
$platform = $pin.platforms.'win-x64'
$runtimeUrl = "$($pin.urlBase)/$($pin.version)/$($platform.file)"
Write-Host "    .NET $($pin.version), fetched from $runtimeUrl only when missing"

$exe = Join-Path $dist "Lego2STL-$Version-win-x64.exe"
Remove-Item -LiteralPath $exe -Force -ErrorAction SilentlyContinue

wix build (Join-Path $PSScriptRoot 'windows\Bundle.wxs') `
    -ext WixToolset.BootstrapperApplications.wixext `
    -ext WixToolset.Netfx.wixext `
    -d "Version=$Version" `
    -d "RuntimeVersion=$($pin.version)" `
    -d "RuntimeUrl=$runtimeUrl" `
    -d "RuntimeSha512=$($platform.sha512)" `
    -d "RuntimeSize=$($platform.size)" `
    -d "MsiPath=$msi" `
    -d "LicenseRtf=$license" `
    -o $exe
if ($LASTEXITCODE -ne 0) { throw 'the installer did not build' }

# The toolset writes a debugging database beside its output; it is not part of the package and
# only confuses anyone looking at the folder.
Get-ChildItem $dist -Filter *.wixpdb | Remove-Item -Force

$exeMb = [math]::Round(((Get-Item $exe).Length / 1MB), 1)
Write-Host ("    {0}  ({1} MB)" -f $exe, $exeMb)

if ($exeMb -gt $ceilingMb) {
    throw "the installer is $exeMb MB, over the $ceilingMb MB ceiling. Is it carrying the runtime again?"
}

Step 'Done'
