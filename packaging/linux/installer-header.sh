#!/usr/bin/env sh
#
# Lego2STL @VERSION@ for Linux.
#
#   ./Lego2STL-@VERSION@-linux-x64.run                for you only, under ~/.local
#   sudo ./Lego2STL-@VERSION@-linux-x64.run --system  for everyone, under /usr/local
#
#   --prefix <dir>   somewhere else entirely
#   --no-runtime     never fetch .NET; stop instead if it is missing
#   --uninstall      remove what was installed
#
# This file is a script with a tarball attached to the end of it. .NET 10 is fetched only if
# this machine has not got it, from a fixed address whose contents are checked against a
# fingerprint built into this file - so a substituted download is refused rather than unpacked.
set -eu

VERSION="@VERSION@"
RUNTIME_VERSION="@RUNTIME_VERSION@"
RUNTIME_URL="@RUNTIME_URL@"
RUNTIME_SHA512="@RUNTIME_SHA512@"
PAYLOAD_LINE=@PAYLOAD_LINE@

prefix="$HOME/.local"
runtime_root=""
fetch_runtime=yes
action=install

while [ $# -gt 0 ]; do
    case "$1" in
        --system)     prefix="/usr/local"; shift ;;
        --prefix)     prefix="${2:?--prefix needs a directory}"; shift 2 ;;
        --no-runtime) fetch_runtime=no; shift ;;
        --uninstall)  action=uninstall; shift ;;
        # Stopping at the last line of the notice above rather than reading on, because
        # everything after it is a script with 24 MB of tarball stapled to the end.
        -h|--help)    sed -n '3,14p;14q' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *)            echo "unknown option: $1" >&2; exit 2 ;;
    esac
done

say() { printf '%s\n' "$*"; }
step() { printf '==> %s\n' "$*"; }

# ---- the runtime probe, inserted here when this installer was built --------------------
@RUNTIME_PROBE@
# ---------------------------------------------------------------------------------------

if [ "$action" = uninstall ]; then
    step "Removing Lego2STL from $prefix"
    rm -rf "$prefix/lib/lego2stl"
    rm -f "$prefix/bin/lego2stl" "$prefix/bin/lego2stl-gui"
    rm -f "$prefix/share/applications/lego2stl.desktop"
    say "Done. The .NET runtime was left alone; other programs may be using it."
    exit 0
fi

step "Installing Lego2STL $VERSION into $prefix"

# ---- .NET 10, if this machine has not got it ------------------------------------------

if runtime_root="$(runtime_find)"; then
    say "    .NET 10 is already here: $runtime_root"
    private_runtime=no
else
    if [ "$fetch_runtime" = no ]; then
        say "No .NET 10 runtime was found, and --no-runtime was given." >&2
        say "Install .NET 10 - your distribution's package, or" >&2
        say "$RUNTIME_URL - and run this again." >&2
        exit 1
    fi

    if [ "$prefix" = "/usr/local" ]; then
        runtime_root="/usr/share/dotnet"
    else
        runtime_root="$HOME/.dotnet"
    fi

    step "Downloading .NET $RUNTIME_VERSION into $runtime_root"
    say "    from $RUNTIME_URL"

    tarball="$(mktemp)"
    if command -v curl >/dev/null 2>&1; then
        curl -fSL --progress-bar "$RUNTIME_URL" -o "$tarball"
    elif command -v wget >/dev/null 2>&1; then
        wget -q --show-progress -O "$tarball" "$RUNTIME_URL"
    else
        rm -f "$tarball"
        say "Neither curl nor wget is here, so .NET cannot be fetched." >&2
        say "Install one of them, or install .NET 10 yourself and run this again." >&2
        exit 1
    fi

    runtime_verify "$tarball" "$RUNTIME_SHA512" || exit 1
    say "    fingerprint checked"

    mkdir -p "$runtime_root"
    tar -xzf "$tarball" -C "$runtime_root"
    rm -f "$tarball"
    private_runtime=yes
fi

# ---- the programs ---------------------------------------------------------------------

step "Unpacking"
mkdir -p "$prefix/lib/lego2stl" "$prefix/bin" "$prefix/share/applications"
tail -n +"$PAYLOAD_LINE" "$0" | tar -xz -C "$prefix/lib/lego2stl"

install -m 0644 "$prefix/lib/lego2stl/lego2stl.desktop" \
                "$prefix/share/applications/lego2stl.desktop"
rm -f "$prefix/lib/lego2stl/lego2stl.desktop"

# The programs are started through a small script each. A runtime this installer put under a
# home directory is not anywhere the programs would look on their own, so the script says
# where it is; when the machine already had .NET, there is nothing to say and the script only
# stands in the way of the program by a few microseconds.
for pair in "lego2stl lego2stl" "lego2stl-gui Lego2STL.Gui"; do
    name="${pair% *}"
    program="${pair#* }"
    {
        printf '#!/usr/bin/env sh\n'
        if [ "$private_runtime" = yes ]; then
            printf 'DOTNET_ROOT="%s"\n' "$runtime_root"
            printf 'export DOTNET_ROOT\n'
            printf 'PATH="$DOTNET_ROOT:$PATH"\n'
            printf 'export PATH\n'
        fi
        printf 'exec "%s/lib/lego2stl/%s" "$@"\n' "$prefix" "$program"
    } > "$prefix/bin/$name"
    chmod 0755 "$prefix/bin/$name"
done

# So the new menu entry appears without logging out. Not every system has this, and it not
# being there is not a failure.
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$prefix/share/applications" 2>/dev/null || true
fi

say ""
say "Done."
case ":${PATH}:" in
    *":$prefix/bin:"*) say "Run 'lego2stl --help' to begin." ;;
    *) say "$prefix/bin is not on your PATH. Add this to your shell's startup file:"
       say "    export PATH=\"\$PATH:$prefix/bin\"" ;;
esac

exit 0
__PAYLOAD_BELOW__
