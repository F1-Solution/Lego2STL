# Finds a bash that can read a Windows path.
#
# Two traps here, and both report the script as simply missing, which is a baffling way to be
# told the wrong shell was picked. PowerShell cannot run a .sh at all - it calls it a document.
# And bash.exe on the path is WSL's on most machines, which cannot see a C:\ path.
#
# Dot-source this file, then call Find-GitBash. It returns a path, or $null.

function Find-GitBash {
    $git = Get-Command git -ErrorAction SilentlyContinue
    if (-not $git) { return $null }

    $gitRoot = Split-Path -Parent (Split-Path -Parent $git.Source)
    foreach ($candidate in @('bin\bash.exe', 'usr\bin\bash.exe')) {
        $path = Join-Path $gitRoot $candidate
        if (Test-Path $path) { return $path }
    }
    return $null
}

# A path bash will read: forward slashes, because it takes a backslash as an escape and would
# otherwise be handed a name with every separator eaten out of it.
function ConvertTo-BashPath([string]$path) {
    return ($path -replace '\\', '/')
}
