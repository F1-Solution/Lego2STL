#!/usr/bin/env sh
#
# Puts Lego2STL where the system expects it.
#
#   ./install.sh              for you only, under ~/.local
#   sudo ./install.sh --system   for everyone, under /usr/local
#
# Both are a copy and nothing more: the programs are self-contained, so there is nothing to
# configure and nothing else to install. Removing them is a matter of deleting the same files,
# which uninstall.sh does.

set -eu

prefix="$HOME/.local"
if [ "${1:-}" = "--system" ]; then
    prefix="/usr/local"
fi

here="$(cd "$(dirname "$0")" && pwd)"

echo "Installing into $prefix"

mkdir -p "$prefix/bin" "$prefix/share/applications"

install -m 0755 "$here/bin/lego2stl"     "$prefix/bin/lego2stl"
install -m 0755 "$here/bin/lego2stl-gui" "$prefix/bin/lego2stl-gui"
install -m 0644 "$here/share/applications/lego2stl.desktop" \
                "$prefix/share/applications/lego2stl.desktop"

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
