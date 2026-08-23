<#
.SYNOPSIS
  Runs the buildable part of the packaging workflow locally, in Docker, using act.

.DESCRIPTION
  Builds the Linux tarball and .deb exactly the way the real workflow does, without needing
  GitHub. Prints what the packages contain, and leaves them under .act-artifacts.

  Only the version and linux jobs run. act cannot run Windows or macOS containers, so the
  test, windows, macos and release jobs are out of reach locally; see packaging/act/README.md.

  Needs Docker Desktop running with the Linux engine, and act on the path.
  The first run pulls about 1.1 GB of image and then downloads the .NET SDK inside it, so
  allow ten to fifteen minutes. Later runs are much quicker.

.PARAMETER Version
  The version to stamp on the packages. Must look like 1.2.0, or 1.2.0-rc1.

.PARAMETER Job
  Run only one job: version, or linux. Omit to run both.

.PARAMETER DryRun
  List what would run and stop, without starting a container.

.EXAMPLE
  ./packaging/act/run.ps1
  ./packaging/act/run.ps1 -Version 1.2.0
  ./packaging/act/run.ps1 -Job version
  ./packaging/act/run.ps1 -DryRun
#>

[CmdletBinding()]
param(
    [string]$Version = '0.0.0-local',
    [ValidateSet('version', 'linux')]
    [string]$Job,
    [switch]$DryRun,
    [switch]$SkipActVersionCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$here = $PSScriptRoot
$root = Split-Path -Parent (Split-Path -Parent $here)
$workflow = Join-Path $here 'local-package.yml'

function Step($message) { Write-Host "==> $message" -ForegroundColor Cyan }
function Problem($message) { Write-Host "!!  $message" -ForegroundColor Red }

# ---- Check the things whose absence gives an unhelpful error later ---------------------

Step 'Checking what is needed'

if (-not (Get-Command act -ErrorAction SilentlyContinue)) {
    Problem 'act is not on the path. Install it with:  winget install nektos.act'
    exit 1
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Problem 'docker is not on the path. Install Docker Desktop.'
    exit 1
}

# docker info fails, rather than reporting something useful, when the engine is not up.
docker info --format '{{.OSType}}' 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Problem 'Docker is installed but not running. Start Docker Desktop and wait for it to settle.'
    exit 1
}

$osType = (docker info --format '{{.OSType}}' 2>$null)
if ($osType -ne 'linux') {
    Problem "Docker is in $osType container mode. act needs Linux containers: right-click the Docker tray icon and choose 'Switch to Linux containers'."
    exit 1
}

Write-Host "    act    $(act --version)"
Write-Host "    docker $((docker --version) -replace '^Docker version ', '')"

# act 0.2.84 and earlier carry CVE-2026-34041 and CVE-2026-34042, which act itself reports on
# every run. Worth stopping for: this hands a container a copy of the repository.
$actVersion = [regex]::Match((act --version), '(\d+\.\d+\.\d+)').Groups[1].Value
if ($actVersion -and [version]$actVersion -lt [version]'0.2.86') {
    Problem "act $actVersion is affected by CVE-2026-34041 and CVE-2026-34042."
    Write-Host '    Upgrade first:  winget upgrade nektos.act'
    Write-Host '    To run anyway, pass -SkipActVersionCheck.'
    if (-not $SkipActVersionCheck) { exit 1 }
    Write-Host '    Continuing anyway, as asked.' -ForegroundColor Yellow
}

# ---- Refuse a version the workflow would refuse, before spending ten minutes on it -----

& (Join-Path $root 'packaging/version.sh') $Version | Out-Null
if ($LASTEXITCODE -ne 0) {
    Problem "'$Version' is not a version the packages can carry. Use 1.2.0, or 1.2.0-rc1."
    exit 1
}

# ---- Warn if the two workflows have drifted apart --------------------------------------

# They share their scripts, so the one thing that can quietly differ is the SDK they ask for.
$realWorkflow = Join-Path $root '.github/workflows/package.yml'
$pattern = "dotnet-version:\s*'([^']+)'"

$realVersions = ([regex]::Matches((Get-Content $realWorkflow -Raw), $pattern) |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
$localVersions = ([regex]::Matches((Get-Content $workflow -Raw), $pattern) |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)

if (Compare-Object $realVersions $localVersions) {
    Write-Host "    note: the real workflow asks for .NET $($realVersions -join ', ') and this one for $($localVersions -join ', ')" -ForegroundColor Yellow
}

# ---- Run --------------------------------------------------------------------------------

# The event file carries the default; a version given here has to override it, which is what
# --input does. Both are passed so the workflow's own default is still exercised.
$arguments = @(
    'workflow_dispatch'
    '-W', $workflow
    '-e', (Join-Path $here 'event.json')
    '--input', "version=$Version"
)

if ($Job) { $arguments += @('-j', $Job) }
if ($DryRun) { $arguments += '--dryrun' }

Step "Running act$(if ($Job) { " (job: $Job)" }) at version $Version"
Write-Host "    act $($arguments -join ' ')" -ForegroundColor DarkGray
Write-Host ''

Push-Location $root
try {
    & act @arguments
    $code = $LASTEXITCODE
}
finally {
    Pop-Location
}

Write-Host ''

if ($code -ne 0) {
    Problem "act finished with exit code $code."
    Write-Host '    A first failure is usually the image pull or the SDK download; run it again before reading too much into it.'
    exit $code
}

Step 'Done'

$artifacts = Join-Path $root '.act-artifacts'
if (Test-Path $artifacts) {
    Write-Host '    The packages are under .act-artifacts:'
    Get-ChildItem -Path $artifacts -Recurse -File |
        ForEach-Object { Write-Host ("      {0,10:N0} KB  {1}" -f ($_.Length / 1KB), $_.FullName.Substring($root.Length + 1)) }
}
else {
    Write-Host '    No artifacts were kept, which is expected for a dry run or for -Job version.'
}
