#!/bin/zsh
# Renders every mockup SVG to a PNG of the same size using headless Chrome.
cd "$(dirname "$0")"
mkdir -p png
for f in *.svg; do
  w=$(sed -n 's/.*<svg[^>]*width="\([0-9]*\)".*/\1/p' "$f" | head -1)
  h=$(sed -n 's/.*<svg[^>]*height="\([0-9]*\)".*/\1/p' "$f" | head -1)
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu \
     --hide-scrollbars --force-device-scale-factor=2 --window-size=${w},${h} \
     --screenshot="png/${f%.svg}.png" "file://$PWD/$f" 2>/dev/null
  echo "  ${f%.svg}.png  (${w}x${h} @2x)"
done
