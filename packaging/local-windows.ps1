<#
.SYNOPSIS
  Builds the Windows package on this machine, the way the workflow's windows job does.

.DESCRIPTION
  Not act, deliberately. act runs Linux containers, and a Windows runner is not a container at
  all, so there is nothing for it to run the windows job in. This does what that job does,
  step for step, against the same scripts - which is what makes a green run here mean
  something about the real one.

  Ends by looking inside the installer, because the thing most worth checking is invisible
  from the outside: that the runtime is fetched when needed rather than carried, from the
  address and with the fingerprint the pin says.

  The version is read from Lego2STL.Core's <Version> element, the same as the real workflow -
  nothing here takes one as a parameter.

.PARAMETER SkipTests
  Skip the test suite. The workflow runs it in a job of its own before packaging.

.EXAMPLE
  ./packaging/local-windows.ps1
  ./packaging/local-windows.ps1 -SkipTests
#>

[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot

. (Join-Path $PSScriptRoot 'lib/find-git-bash.ps1')

function Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Problem($message) { Write-Host "!!  $message" -ForegroundColor Red }

# ---- The things whose absence gives an unhelpful error later ---------------------------

Step 'Checking what is needed'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Problem 'The .NET SDK is not on the path.'
    exit 1
}
Write-Host "    dotnet $(dotnet --version)"

if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Problem 'The WiX toolset is not on the path. Install it with:'
    Write-Host '    dotnet tool install --global wix --version 6.0.1'
    exit 1
}
Write-Host "    wix    $(wix --version)"

# The version rule is a shell script, shared with the workflow so the two cannot disagree, so
# a shell is needed to read it. Which one takes care; see the helper.
$gitBash = Find-GitBash
if (-not $gitBash) {
    Problem 'Git for Windows is needed: its bash is what runs the version rule the workflow uses.'
    Write-Host '    winget install Git.Git'
    exit 1
}
Write-Host "    bash   $gitBash"

# Read the version, before spending minutes on a build that would only fail on it too.
$versionOutput = & $gitBash (ConvertTo-BashPath (Join-Path $PSScriptRoot 'version.sh'))
if ($LASTEXITCODE -ne 0) {
    Problem 'Lego2STL.Core has no usable <Version>. See packaging/version.sh.'
    exit 1
}
$Version = ($versionOutput | Select-String '^number=(.+)$').Matches[0].Groups[1].Value
Write-Host "    version $Version, read from Lego2STL.Core"

# ---- What the workflow's jobs do -------------------------------------------------------

if (-not $SkipTests) {
    Step 'Running the tests, as the test job does'
    dotnet test --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) { Problem 'the tests failed'; exit 1 }
}

Step 'Building the package, as the windows job does'
& (Join-Path $PSScriptRoot 'build-windows.ps1') -Version $Version
if ($LASTEXITCODE -ne 0) { Problem 'the build failed'; exit 1 }

# ---- The part CI cannot show anyone ----------------------------------------------------

Step 'Looking inside the installer'

# By version, not by pattern. Whatever was built earlier is still in that folder, and taking
# the first match would happily bless an installer this run did not produce.
$exe = Get-ChildItem (Join-Path $root 'artifacts\dist') -Filter "*-$Version-win-x64.exe" |
    Select-Object -First 1
if (-not $exe) { Problem "no installer for $Version was produced"; exit 1 }

# Two folders, because the two halves of a bundle come out separately: -o holds the packages
# it carries, and -oba the bootstrapper, which is where the manifest describing them lives.
$unpacked = Join-Path $root 'artifacts\staging\bundle'
$bootstrapper = Join-Path $root 'artifacts\staging\bundle-ba'
Remove-Item -LiteralPath $unpacked, $bootstrapper -Recurse -Force -ErrorAction SilentlyContinue
wix burn extract $exe.FullName -o $unpacked -oba $bootstrapper | Out-Null
if ($LASTEXITCODE -ne 0) { Problem 'the installer could not be taken apart'; exit 1 }

$manifest = Get-ChildItem $bootstrapper -Recurse -Filter 'manifest.xml' | Select-Object -First 1
if (-not $manifest) { Problem 'the installer has no manifest to read'; exit 1 }

$xml = [xml](Get-Content $manifest.FullName -Raw)
$runtime = $xml.BurnManifest.Payload |
    Where-Object { $_.GetAttribute('FilePath') -like '*dotnet-runtime*' }

if (-not $runtime) {
    Problem 'the installer does not mention the .NET runtime at all.'
    exit 1
}

$packaging = $runtime.GetAttribute('Packaging')
if ($packaging -ne 'external') {
    Problem "the runtime is '$packaging' rather than external - it is being carried, not fetched."
    exit 1
}

# The bundle holds its own copy of the fingerprint. If it has drifted from runtime.json, the
# installer would refuse the very download it was told to make, and only a stranger with no
# .NET would ever find out.
$pin = Get-Content (Join-Path $PSScriptRoot 'runtime.json') -Raw | ConvertFrom-Json
$expected = $pin.platforms.'win-x64'.sha512
if ($runtime.GetAttribute('Hash') -ne $expected) {
    Problem 'the fingerprint in the installer is not the one in runtime.json.'
    Write-Host "    installer: $($runtime.GetAttribute('Hash'))"
    Write-Host "    pin:       $expected"
    exit 1
}

Write-Host "    the runtime is fetched, not carried: $($runtime.GetAttribute('DownloadUrl'))"
Write-Host "    its fingerprint matches runtime.json"
Write-Host ("    installer: {0}  ({1} MB)" -f $exe.Name, [math]::Round($exe.Length / 1MB, 1))

Step 'Done'
Get-ChildItem (Join-Path $root 'artifacts\dist') |
    ForEach-Object { Write-Host ("      {0,8:N1} MB  {1}" -f ($_.Length / 1MB), $_.Name) }
