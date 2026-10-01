# Farm Fury Arcade - shorts content pipeline

Steps built so far: 1) record + find highlights, 2) turn the best clips into
vertical shorts. Captions and scheduling come next.

## Step 2: make vertical shorts
```
python make_shorts.py                   # newest session
python make_shorts.py sessions/<id> --count 5 --include-deaths
```
Writes to `sessions/<id>/shorts/` (files are overwritten by name), all 1080x1920,
30fps, loudness-normalised. Every short opens with a 2s intro (the Farm Fury logo fading
in and out over the blurred poster, Theme music) and ends with a 3s end card (logo,
"PLAY FREE NOW!", "GET IT ON GOOGLE PLAY", search "Farm Fury Arcade", FARMFURYGAMES.COM):
- `mix_01.mp4` - highlight mix: the best 3 moments, strongest first (~25s)
- `short_01.mp4`... - one moment each, lengthened to 12-18s (mostly extra lead-in)
- `ability_<name>.mp4` - one per animal special move seen in the session
  (Cluck's Egg Drop, Bessie's Ground Slam, ...)
- `power_crop.mp4` - a power crop sending the robots running
- `combo_<name>.mp4` - any combo triggered
- `mix_02_whole_game.mp4` - one of each: collecting/dodging, power crop, special move, combo
- `swap_<character>.mp4` - the Choose Character screen, then the new animal in the maze
- `mix_03_swaps.mp4` - up to 5 swaps back to back
- `unlock_<character>.mp4` - the level finishing, then the unlock card frozen for
  2.5s (the card appears ~3.3s after the unlock marker; tune `UNLOCK_CARD_DELAY_SECONDS`)

The dodge-and-collect shorts (`mix_01`, `short_NN`) are the main message; the
ability/power/combo shorts show the rest of the game. Clips with a death are
skipped unless `--include-deaths`. Headlines come from each
clip's best moment (`hook_text`). Layout: headline at the top, the centre of the
game screen (the maze) in the middle, logo + "FREE ON GOOGLE PLAY" above the area
TikTok/Shorts cover with their own buttons, blurred gameplay behind.

## Step 3: captions and a posting plan
```
python plan_posts.py                      # 7 days from tomorrow, 2 a day (07:30, 18:30)
python plan_posts.py --per-day 3 --days 5 --dry-run
```
Writes a caption per short (kid-safe wording checked with `check_wording`), alternates
dodge-and-collect shorts with ability/power/unlock/combo ones, and writes
`plans/<start>.html` (time, video file, caption with a Copy button) for scheduling in
TikTok Studio (up to 10 days ahead). `ledger.json` stops a short being planned twice;
both are gitignored. TikTok account: @farmfurygames.

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
`player_death`, `revive`, `power_pellet`, `character_swap` (from, to; added 2026-10-01,
needs a new Development Build - for older sessions put the swap times in
`sessions/<id>/swaps.json` as `[{"t": 61.0, "character": "Percy"}, ...]`), `robot_defeated` (robot, chain,
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
