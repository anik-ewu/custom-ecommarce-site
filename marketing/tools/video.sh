#!/bin/bash
# Builds the promo video: per-scene clips, then crossfades them together.
set -euo pipefail
SCENES=$1; SHOTS=$2; OUT=$3; WORK=$4
mkdir -p "$WORK"
FPS=30; ENC="-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -r $FPS"

still() { # name duration zoom(0/1)
  local f=$((${2%.*} * FPS))
  if [ "$3" = 1 ]; then
    ffmpeg -y -v error -loop 1 -i "$SCENES/$1.png" -t "$2" \
      -vf "scale=3840:2160,zoompan=z='1+0.05*on/$f':d=1:x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':s=1920x1080:fps=$FPS" \
      $ENC "$WORK/$1.mp4"
  else
    ffmpeg -y -v error -loop 1 -i "$SCENES/$1.png" -t "$2" -vf "fps=$FPS" $ENC "$WORK/$1.mp4"
  fi
}

# Home page: eased scroll down the full-page screenshot, caption on top.
ffmpeg -y -v error -loop 1 -i "$SHOTS/desktop-home-full.png" -loop 1 -i "$SCENES/02-home-cap.png" -t 10 \
  -filter_complex "[0]scale=1920:-1,crop=1920:1080:0:'(ih-1080)*(1-cos(PI*min(max(t-0.8,0)/8.4,1)))/2'[bg];[bg][1]overlay=0:0,fps=$FPS" \
  $ENC "$WORK/02-home.mp4"

still 01-intro 4 0
for s in 03-shop 04-product 05-cart 06-checkout 07-mobile 08-admin 09-admin-products; do still $s 5 1; done
still 10-quality 6 0
still 11-outro 5 0

# Crossfade chain.
CLIPS=(01-intro 02-home 03-shop 04-product 05-cart 06-checkout 07-mobile 08-admin 09-admin-products 10-quality 11-outro)
XF=0.6; inputs=(); filter=""; offset=0; prev="0:v"
for i in "${!CLIPS[@]}"; do
  inputs+=(-i "$WORK/${CLIPS[$i]}.mp4")
  d=$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$WORK/${CLIPS[$i]}.mp4")
  if [ "$i" -gt 0 ]; then
    filter+="[$prev][$i:v]xfade=transition=fade:duration=$XF:offset=$offset[v$i];"; prev="v$i"
  fi
  offset=$(echo "$offset + $d - $XF" | bc)
done
filter+="[$prev]fade=t=out:st=$(echo "$offset + $XF - 0.8" | bc):d=0.8[vout]"
ffmpeg -y -v error "${inputs[@]}" -f lavfi -i anullsrc=r=48000:cl=stereo \
  -filter_complex "$filter" -map "[vout]" -map "${#CLIPS[@]}:a" -shortest \
  $ENC -c:a aac -b:a 128k -movflags +faststart "$OUT"
ffprobe -v error -show_entries format=duration,size -of default=nw=1 "$OUT"
