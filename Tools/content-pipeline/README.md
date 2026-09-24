# Farm Fury Arcade - shorts content pipeline

Step 1 of the TikTok / YouTube Shorts pipeline: record gameplay from the Android
phone and find the moments worth cutting into clips. Later steps (vertical
video, captions, scheduling) will read the `clips.json` this step writes.

## One-time setup
1. Portable copies of scrcpy 4.1 and ffmpeg live in `bin/` (gitignored, not in the
   repo). The scripts find them there, nothing to install. On a new PC, unzip
   `scrcpy-win64-*.zip` (github.com/Genymobile/scrcpy/releases) and the gyan.dev
   `ffmpeg-release-essentials.zip` into `bin/`.
2. Build a **Development Build** APK (Build App Bundle OFF) and install it:
   `adb install -r <file>.apk`. The highlight markers only exist in development
   builds; the Play Store build has none.
3. Phone: USB debugging on, plugged in, `adb devices` shows it.

## Record a session
```
python record_session.py          # add --show to see the mirror window on the PC
```
Play on the phone, press Enter in the terminal to stop. You get a folder in
`sessions/<date-time>/` with:
- `raw.mkv` - scrcpy's raw recording
- `video.mp4` - the same video re-encoded so it can be cut at any second (made by
  `normalize_video.py`, runs automatically)
- `logcat.txt` - raw Unity log
- `session.json` - recording start time and phone clock offset
- `markers.json` - every highlight, in seconds from the start of the video
- `clips.json` - candidate clips, best first (also printed to the terminal)

Re-run the ranking at any time (e.g. after changing the weights):
`python parse_markers.py sessions/<date-time>`

## Markers the game writes
`level_start`, `level_complete`, `level_failed` (reason timeout / out_of_lives),
`player_death`, `revive`, `power_pellet`, `robot_defeated` (robot, chain,
power), `full_chain`, `combo`, `ability`, `near_miss` (hostile robot within 1.4
tiles), `character_unlock`, `world_unlock`. Source:
`Assets/_Project/Scripts/Core/HighlightMarkers.cs`.

Scoring and clip padding are the `WEIGHTS` table at the top of
`parse_markers.py`. A near miss followed by a death within 2s is dropped; clips
that contain a death are ranked lower but kept.

## Accuracy
Marker times come from the phone clock, shifted by the offset measured at the
start of the session. The recording start is taken from scrcpy's "Recording
started" message, so expect about half a second of error; the clip padding
covers it.

## Recording tips
- The script opens the game before recording starts, and the recording is locked
  to that (sideways) orientation.
- Phones that silence app logs (the Honor test phone does): the script runs
  `adb shell setprop log.tag.Unity V` each session so the markers get through.
- One level per clip idea: start recording, play 2-4 levels, stop. Shorter
  sessions are easier to review.
- Turn on Do Not Disturb so notifications don't land in the video.
