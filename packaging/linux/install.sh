#!/usr/bin/env sh
#
# Puts Lego2STL where the system expects it.
#
#   ./install.sh              for you only, under ~/.local
#   sudo ./install.sh --system   for everyone, under /usr/local
#
# Both are a copy and nothing more. The programs need .NET 10 to be on this machine already;
# the .run installer is the one that fetches it when it is missing.
#
# Removing them means deleting what was copied: the lego2stl folder under lib, the two names
# under bin, and the menu entry under share/applications.

set -eu

prefix="$HOME/.local"
if [ "${1:-}" = "--system" ]; then
    prefix="/usr/local"
fi

here="$(cd "$(dirname "$0")" && pwd)"

# The same probe the .run installer uses, but only to report: someone who unpacked a tarball
# by hand did not ask this script to download 36 MB.
. "$here/runtime-probe.sh"
if ! runtime_find >/dev/null; then
    echo "Note: no .NET 10 runtime was found, and the programs need one." >&2
    echo "      Install .NET 10, or use the .run installer, which fetches it." >&2
fi

echo "Installing into $prefix"

mkdir -p "$prefix/lib/lego2stl" "$prefix/bin" "$prefix/share/applications"
cp -R "$here/." "$prefix/lib/lego2stl/"
rm -f "$prefix/lib/lego2stl/install.sh" "$prefix/lib/lego2stl/lego2stl.desktop"
ln -sf "$prefix/lib/lego2stl/lego2stl"     "$prefix/bin/lego2stl"
ln -sf "$prefix/lib/lego2stl/Lego2STL.Gui" "$prefix/bin/lego2stl-gui"
install -m 0644 "$here/lego2stl.desktop" "$prefix/share/applications/lego2stl.desktop"

# So the new menu entry appears without logging out. Not every system has this, and it not
# being there is not a failure.
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database "$prefix/share/applications" 2>/dev/null || true
fi

echo "Done."
echo

case ":$PATH:" in
    *":$prefix/bin:"*) echo "Run 'lego2stl --help' to begin." ;;
    *) echo "$prefix/bin is not on your PATH. Add this to your shell's startup file:"
       echo "    export PATH=\"\$PATH:$prefix/bin\"" ;;
esac
