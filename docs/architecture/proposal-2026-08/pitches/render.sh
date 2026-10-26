#!/bin/zsh
cd "$(dirname "$0")"; mkdir -p png
for f in *.svg; do
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu \
    --hide-scrollbars --force-device-scale-factor=2 --window-size=1600,900 \
    --screenshot="png/${f%.svg}.png" "file://$PWD/$f" 2>/dev/null
done
echo "$(ls png | wc -l | tr -d ' ') slides rendered"
