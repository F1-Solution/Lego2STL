# Finding an existing .NET 10, and checking a download.
#
# Sourced by the Linux .run installer, by install.sh inside the tarball, and by the macOS
# preinstall script. Defines functions and does nothing else, so sourcing it is free.
#
# The registry-style shortcuts do not work here and are not attempted: patches of the runtime
# install side by side, so the question is never "which version" but "is any 10.0.x present".

# Where a .NET install root might be. RUNTIME_SEARCH_ROOTS overrides the list, which is what
# lets the tests ask about a machine they can control.
runtime_search_roots() {
    if [ -n "${RUNTIME_SEARCH_ROOTS:-}" ]; then
        printf '%s\n' "$RUNTIME_SEARCH_ROOTS" | tr ':' '\n'
        return 0
    fi
    printf '%s\n' \
        "$HOME/.dotnet" \
        "/usr/share/dotnet" \
        "/usr/lib/dotnet" \
        "/usr/local/share/dotnet"
}

# Does this root hold a 10.x runtime? A directory named 10.something under the shared
# framework is the only thing that matters; its patch number never does, because the app asks
# for 10.0.0 and the host picks the newest 10.0.x it finds.
runtime_root_has_ten() {
    _root="$1"
    [ -n "$_root" ] || return 1
    [ -d "$_root/shared/Microsoft.NETCore.App" ] || return 1
    for _candidate in "$_root"/shared/Microsoft.NETCore.App/10.*; do
        [ -d "$_candidate" ] && return 0
    done
    return 1
}

# Prints the root holding a .NET 10 runtime, or nothing.
runtime_find() {
    if runtime_root_has_ten "${DOTNET_ROOT:-}"; then
        printf '%s\n' "$DOTNET_ROOT"
        return 0
    fi

    # Fed in by redirection rather than through a pipe: a loop on the right of a pipe runs in
    # a subshell, and what it found would not survive the loop ending.
    _found=""
    _roots="$(runtime_search_roots)"
    while IFS= read -r _root; do
        [ -n "$_root" ] || continue
        if runtime_root_has_ten "$_root"; then
            _found="$_root"
            break
        fi
    done <<ROOTS
$_roots
ROOTS

    if [ -n "$_found" ]; then
        printf '%s\n' "$_found"
        return 0
    fi

    # Nothing where one is normally kept. A dotnet on the path may still know better - a
    # distribution package, or an install somewhere of the user's choosing. Asking it is the
    # authoritative answer, so it is asked last rather than not at all.
    if command -v dotnet >/dev/null 2>&1; then
        if dotnet --list-runtimes 2>/dev/null | grep -q '^Microsoft\.NETCore\.App 10\.'; then
            _exe="$(command -v dotnet)"
            if command -v readlink >/dev/null 2>&1; then
                _resolved="$(readlink -f "$_exe" 2>/dev/null || printf '%s' "$_exe")"
            else
                _resolved="$_exe"
            fi
            printf '%s\n' "$(dirname "$_resolved")"
            return 0
        fi
    fi

    return 1
}

# The digest of a file, whichever tool this system carries. Linux has sha512sum; macOS has
# shasum and not the other.
runtime_sha512() {
    if command -v sha512sum >/dev/null 2>&1; then
        sha512sum "$1" | cut -d' ' -f1
    elif command -v shasum >/dev/null 2>&1; then
        shasum -a 512 "$1" | cut -d' ' -f1
    else
        echo "no sha512 tool found (looked for sha512sum and shasum)" >&2
        return 1
    fi
}

# Checks a download against the digest built into this installer. A mismatch means the file
# is not the one this installer was built to trust, so it is deleted rather than left to be
# unpacked by anything else.
runtime_verify() {
    _file="$1"
    _expected="$(printf '%s' "$2" | tr 'A-Z' 'a-z')"
    _actual="$(runtime_sha512 "$_file" | tr 'A-Z' 'a-z')" || return 1

    if [ "$_actual" = "$_expected" ]; then
        return 0
    fi

    echo "the download does not match what this installer expects:" >&2
    echo "  expected $_expected" >&2
    echo "  actual   $_actual" >&2
    rm -f "$_file"
    return 1
}
