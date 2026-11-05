"""Storyline episodes from Kling clips: each clip trimmed to its slot, joined in order, voice lines
dropped in at set times with burned-in captions, the clips' own Kling sound kept as quiet ambience, game
sound effects on the big moments, and a music bed that dips under the voices. 9:16, 1080x1920.

Each episode opens on a flash-forward with a big text hook, ends on a few seconds of real gameplay (from a
recorded content-pipeline session) and a card with the Farm Fury Arcade logo and the App Store / Google
Play badges. Each episode's shots, lines and music are one entry in EPISODES.

Clips: <Video>/EpNN_Clips/. Voices: <Video>/Eleven_Audio, robot lines from <Video>/Robot_Voices (run
robot_fx.py first). Gameplay: Tools/content-pipeline/sessions/<id>/video.mp4.
Output: <Video>/Episodes/<episode name>.mp4

Usage:
    python make_episode.py [--episode N]   # default episode 1
    python make_episode.py --play-only     # Google Play badge only (to post before the iOS app is live)
    python make_episode.py --episode N --opener-only   # re-add the opener without re-rendering the episode

Every episode starts with the same 1.6 s opener (the logo over all eight animals, "Farm Fury!" shout). The render
without it is kept in Episodes/no_opener/.
"""

import random
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "content-pipeline"))
from PIL import Image, ImageDraw, ImageFilter, ImageFont  # noqa: E402

from make_shorts import (ACCENT_FILL, AUDIO_OUT, CROP_ASPECT, FONT, FPS, H, PICTURE_CENTER_Y,  # noqa: E402
                         POSTER, REPO, STROKE_FILL, TEXT_FILL, VIDEO_OUT, W, check_wording, fit_font)
from make_animatic import ROBOT_VOICE_DIR, VIDEO_DIR, VOICE_DIR, build_caption, prepare_voice, probe_duration, run  # noqa: E402
from normalize_video import find_ffmpeg  # noqa: E402

MUSIC_DIR = REPO / "Assets/_Project/Audio/Music"
THEME = MUSIC_DIR / "Theme.mp3"
# The game's power-pellet music ("robots run"): the more urgent bed for each episode's action stretch.
CHASE_MUSIC = REPO / "Assets/_Project/Audio/SFX/EatRobot.mp3"
SFX = REPO / "Assets/_Project/Audio/SFX"
SESSION_VIDEO = HERE.parent / "content-pipeline/sessions/20261001-150832/video.mp4"

XFADE = 0.4               # crossfade between clips
AMBIENCE = 0.45           # Kling's own clip sound, under everything
MUSIC_GAIN = 0.16
FADE_OUT = 0.6
END_CARD_SECONDS = 4.5    # minimum; stretched to fit the end-card voices

# End card voices (every episode): all eight animals shout "Farm Fury!" together (<Character>_farmfury.mp3 in
# Eleven_Audio, any capitalisation; any missing file is skipped), each a few hundredths of a second apart so it reads as a crowd,
# then Bessie's E.1 with its two halves swapped: "Come help us out." (1.05-2.12s) then "Free to play." (0-0.80s).
CHORUS = ["cluck", "bessie", "percy", "woolly", "ducky", "horace", "gerald", "billy"]
CHORUS_STAGGER = 0.035
END_CHORUS_AT = 0.5
WRAP_CARD_FADE = 0.3     # wrapped shorts: the end card snaps in over this, at the end of the audio crossfade,
WRAP_SHOUT_DELAY = 0.97   # the shout starts this long after the card lands (user-tuned, 2026-11-05)
                         # and the shout starts as it lands (a slow dissolve made the shout read as early/late)
END_LINE = ("bessie_E.1", [(1.05, 2.12), (0.0, 0.80)])
END_LINE_GAP = 0.35       # after the chorus, and between the two swapped halves
END_TAIL = 1.0            # card stays up this long after the last word
END_CARD_XFADE = 0.8
APP_STORE_BADGE = HERE / "assets" / "app-store-badge.png"
PLAY_BADGE = HERE / "assets" / "google-play-badge.png"
HOOK_TOP = 200

# The franchise logos (2026-11-03): one marketing push for every Farm Fury game, so the opener and end card show
# the FARM FURY logo with Arcade and Stampede stacked beneath it.
FARMFURY_LOGO = HERE / "assets" / "farmfury-logo.png"
ARCADE_LOGO = HERE / "assets" / "arcade-logo.png"
STAMPEDE_LOGO = HERE / "assets" / "stampede-logo.png"

# Opener: the shout with FARM FURY punching in over the line-up, then Arcade (OPENER_ARCADE_AT) and Stampede
# (OPENER_STAMPEDE_AT) fade in stacked while the scene dims, and everything fades before the story.
OPENER_STILL = VIDEO_DIR / "Ep04_Clips/stills/4.1_meeting.png"
OPENER_SECONDS = 3.2
OPENER_SHOUT_AT = 0.08
OPENER_ARCADE_AT = 1.2
OPENER_STAMPEDE_AT = 2.0
OPENER_FADE = 0.35
OPENER_SPEED = {"bessie": 1.5}   # her take is about 2 s, the others about 1 s

# Each shot: clip file (in the episode's clip_dir), seconds cut off its start, seconds used after that, extra
# seconds to hold the last frame ("hold_zoom": True adds a slow push-in over the hold, for a comic freeze-frame),
# voice lines (file stem, caption, seconds after the shot starts, optionally (from, to) seconds to use only part of
# the recording), and sound effects (file in SFX, seconds after the shot starts, gain). "chase_music": True switches
# the music to CHASE_MUSIC from that shot until the gameplay. A "gameplay" shot is (start, seconds) of the
# episode's gameplay video, framed 9:16 like the shorts, with its own game sound.
EPISODES = {
    1: {
        "name": "ep01_the_night_the_robots_came",
        "clip_dir": VIDEO_DIR / "Ep01_Clips",
        "music": THEME,
        "gameplay": SESSION_VIDEO,
        # Big text over the cold open, for viewers watching with the sound off.
        "hook": ["THEY CAME", "FOR THE CROPS"],
        "shots": [
            # Cold open (2026-10-19): a flash-forward to the Combine's eye and the end of its line, so the first two
            # seconds hook the viewer before the slow farm intro.
            {"file": "ep01_C_eye.mp4", "skip": 3.6, "use": 2.4, "hook": True,
             "sfx": [("Bessie-ability.mp3", 0.0, 0.9)],
             "lines": [("robot_combine_1.4", "Every. Single. One.", 0.05, (4.6, 6.9))]},
            {"file": "ep01_A_farm.mp4", "use": 14.0, "lines": [
                ("cluck_1.1", "It was harvest night. The corn was golden, the carrots were crunchy, "
                              "and everybody was fast asleep…", 0.6),
                ("bessie_1.1", "…except one chicken who'd had too much corn before bed.", 9.2)]},
            # The Combine touches down ~5.6s into the clip.
            {"file": "ep01_B_landing.mp4", "use": 10.0, "sfx": [("Bessie-ability.mp3", 5.5, 1.0)],
             "lines": [("robot_combine_1.4", "Target: farm. Instruction: collect every crop. Every. Single. One.", 7.0)]},
            {"file": "ep01_C_eye.mp4", "skip": 1.4, "use": 4.6, "sfx": [("RobotSpawn.mp3", 0.3, 0.7)], "lines": []},
            # The clip opens on the scarecrow shot again (it was started from B's last frame) and only cuts to the hatch at
            # ~1.9s; starting there avoids a jump back after the eye close-up. The last frame is held to fit Bessie's line.
            {"file": "ep01_D_robots.mp4", "skip": 1.9, "use": 3.1, "hold": 1.3, "chase_music": True,
             "sfx": [("RobotSpawn.mp3", 0.1, 0.8), ("RobotSpawn.mp3", 1.4, 0.6)],
             "lines": [("cluck_1.5_a", "And out came… them.", 0.6),
                       ("bessie_1.5", "The Harvest Robots.", 3.3)]},
            # Gameplay was taken out of the episodes on 2026-11-03 (it gets its own posts), so Cluck's closing line
            # now plays over the held maze shot.
            {"file": "ep01_E_maze.mp4", "use": 8.0, "hold": 5.0, "lines": [
                ("bessie_1.6", "In one night they turned our fields into mazes. Harvest corridors, they called them.", 1.2),
                ("cluck_1.6_a", "Rude, is what I called them.", 7.6),
                ("cluck_6.11", "Robots, meet the Farm Fury.", 10.0)]},
        ],
    },
    # Ep 2 (2026-10-19): one 5s Kling 3.0 Turbo clip per animal, each animated from a 1-credit still made from the
    # animal's game art (stills and job ids in Ep02_Clips/). All voice lines were already recorded.
    2: {
        "name": "ep02_chaos_on_the_farm",
        "clip_dir": VIDEO_DIR / "Ep02_Clips",
        "music": MUSIC_DIR / "CornField.mp3",
        "gameplay": SESSION_VIDEO,
        "hook": ["THE ROBOTS", "TOOK EVERYTHING"],
        "shots": [
            # Opens on the robots leaving the Combine (Ep 1's footage, reused) with the hook text, to tie the episodes
            # together, then straight into Gerald. (A flash-forward to Gerald's own line made it play twice.)
            {"path": VIDEO_DIR / "Ep01_Clips/ep01_D_robots.mp4", "skip": 1.9, "use": 3.1, "hook": True,
             "sfx": [("RobotSpawn.mp3", 0.1, 0.8), ("RobotSpawn.mp3", 1.4, 0.6)], "lines": []},
            {"file": "ep02_2.4_gerald.mp4", "use": 5.0, "lines": [
                ("gerald_2.4", "That. Was. My. FENCE.", 0.5)]},
            {"file": "ep02_2.1_percy.mp4", "use": 5.0, "hold": 0.8, "lines": [
                ("cluck_2.1", "Percy lost his midnight snack.", 0.2),
                ("percy_2.1", "…Hey! That was for later!", 3.0)]},
            {"file": "ep02_2.2_woolly.mp4", "use": 5.0, "hold": 1.6, "sfx": [("RobotSpawn.mp3", 0.2, 0.6)], "lines": [
                ("woolly_2.2", "Not the wool! It took me a YEAR to grow this!", 0.2),
                ("robot_drone_2.2", "Hee hee. Fluffy crop.", 4.8)]},
            {"file": "ep02_2.3_ducky.mp4", "use": 5.0, "lines": [
                ("ducky_2.3", "…Dude. Not cool.", 2.3)]},
            {"file": "ep02_2.5_horace.mp4", "use": 5.0, "hold": 0.3, "lines": [
                ("horace_2.5", "…Did that tin can just take my trophies?", 1.4)]},
            # Billy hits the robot ~2.5s in and lands on his back ~3.5s.
            {"file": "ep02_2.6_billy.mp4", "use": 5.0, "hold": 2.2, "sfx": [("Bessie-ability.mp3", 2.5, 0.9)], "lines": [
                ("billy_2.6", "Head first… questions… later…", 3.0)]},
            # Chaos, then the punchline over a freeze-frame of Cluck mid-scream (1.55s into the same clip, where she's
            # closest to camera) with a slow push-in.
            {"file": "ep02_2.7_chaos.mp4", "use": 5.0, "chase_music": True, "lines": [
                ("cluck_2.7a", "It was CHAOS. Feathers! Wool! Panic! Somebody screamed!", 0.1)]},
            {"file": "ep02_2.7_chaos.mp4", "skip": 1.55, "use": 0.1, "hold": 4.3, "hold_zoom": True,
             "caption_y": 560,   # Cluck fills the bottom of this frame; keep her face clear
             "lines": [
                ("bessie_2.7", "That was you, Clucky.", 0.6),
                ("cluck_2.7b", "…It was a brave scream.", 2.4)]},
        ],
    },
    # Ep 3 (2026-10-19): the Barnyard Breakdown talk show. Five Kling 3.0 Turbo clips animated from 1-credit stills;
    # 3.1's studio still is the set reference for the others, and 3.6 animates straight from it. 3.2 is not a Kling
    # job: make_tv_replay.py puts Ep 1's landing on the studio TV with chalk marks. No lip-sync, by choice.
    3: {
        "name": "ep03_the_debrief",
        "clip_dir": VIDEO_DIR / "Ep03_Clips",
        "music": THEME,
        "gameplay": SESSION_VIDEO,
        "hook": ["BARNYARD", "BREAKDOWN"],
        "shots": [
            {"file": "ep03_3.1_studio.mp4", "use": 6.0, "hold": 1.0, "hook": True, "hook_seconds": 2.6, "lines": [
                ("cluck_3.1", "Welcome back to Barnyard Breakdown, I'm Cluck—", 0.3),
                ("bessie_3.1", "—and I'm Bessie. Let's look at the replay.", 3.9)]},
            # Chalk marks in the replay appear at 0.9s (circle), 2.6s (arrow) and 5.6s (flattened rows).
            {"file": "ep03_3.2_tv.mp4", "use": 10.8, "hold": 0.3, "lines": [
                ("cluck_3.2", "Look at that landing! Right in the corn! Zero style points!", 0.6),
                ("bessie_3.2", "Flattened forty rows. Very efficient. Very rude.", 5.9)]},
            {"file": "ep03_3.3_cluck.mp4", "use": 6.0, "hold": 0.3, "lines": [
                ("cluck_3.3", "Bessie, they took the CORN. The corn, Bessie! What's next, the EGGS?", 0.1)]},
            {"file": "ep03_3.4_bessie.mp4", "use": 6.0, "hold": 2.8, "hold_zoom": True, "lines": [
                ("bessie_3.4", "Steady, Clucky. Robots are fast. Robots are strong. But robots can't think like a farm.", 0.2)]},
            {"file": "ep03_3.5_board.mp4", "use": 7.0, "hold": 4.1, "hold_zoom": True, "lines": [
                ("bessie_3.5a", "Eight of us. Eight ways of being stubborn.", 0.3),
                ("cluck_3.5", "Is this… a PLAN?", 4.3),
                ("bessie_3.5b", "It's the start of one. Ring the bell.", 8.2)]},
            {"file": "ep03_3.6_signoff.mp4", "use": 4.0, "hook": ["NEXT:", "THE BARN MEETING"], "lines": [
                ("cluck_3.6", "Stay tuned, farm fans!", 0.3)]},
        ],
    },
    # Ep 4 (2026-10-19): the barn meeting, the direct follow-on to Ep 3's "Ring the bell" (Meet the Metal moves later).
    # Cheaper version per the user: the animals are already gathered (no bell/arrival shots). Two Kling clips; the
    # Billy/Gerald reaction and the hayloft hook are 1-credit stills with a slow push. Every line was already recorded.
    4: {
        "name": "ep04_the_barn_meeting",
        "clip_dir": VIDEO_DIR / "Ep04_Clips",
        "music": THEME,
        "gameplay": SESSION_VIDEO,
        "hook": ["ONE FARM.", "EIGHT ANIMALS."],
        "shots": [
            {"file": "ep04_4.1_meeting.mp4", "use": 8.0, "hold": 3.6, "hold_zoom": True, "hook": True, "hook_seconds": 2.8,
             "lines": [
                ("cluck_5.5a", "They took our crops. They cut up our fields. They took WOOLLY'S WOOL!", 0.3),
                ("woolly_5.5", "It was a really good fleece.", 6.0),
                ("cluck_5.5b", "So who's with me?!", 9.2)]},
            {"image": "stills/4.2_billy_gerald.png", "use": 6.6, "lines": [
                ("billy_5.5", "Head first!", 0.3),
                ("gerald_5.5", "…Ugh. Fine.", 2.9)]},
            {"file": "ep04_4.3_bessie.mp4", "use": 5.0, "hold": 2.0, "hold_zoom": True, "lines": [
                ("bessie_7.5", "We swap in, we swap out, we work together. Nobody gets left in the maze.", 0.3)]},
            # The hook into the powers episode: a golden glow from the hayloft.
            {"image": "stills/4.4_hayloft.png", "use": 6.0, "push": 0.14, "hook": ["NEXT:", "THE GOLDEN GLOW"], "lines": [
                ("bessie_6.1a", "Every farm has a secret. Ours is up in the hayloft.", 0.5)]},
        ],
    },
    # Ep 5 (2026-11-03): The Golden Glow, the payoff to Ep 4's hayloft hook. Opens on Ep 4's hayloft still (reused,
    # no new job), then the sunflower, the golden circle and one 5s reveal per animal (each also works as a solo
    # short), ending on the eight in the barn doorway (a 1-credit still). Kling 3.0 Turbo 720p, no sound; jobs in
    # Ep05_Clips/jobs.txt. Woolly's line is three takes layered (only the first carries the caption).
    5: {
        "name": "ep05_the_golden_glow",
        "clip_dir": VIDEO_DIR / "Ep05_Clips",
        "music": THEME,
        "hook": ["THE GOLDEN", "GLOW"],
        "shots": [
            {"image": "stills/4.4_hayloft.png", "use": 5.0, "push": 0.12, "hook": True, "lines": [
                ("bessie_6.1a", "Every farm has a secret. Ours is up in the hayloft.", 0.5)]},
            {"file": "ep05_6.1_sunflower.mp4", "use": 7.0, "hold": 0.6, "lines": [
                ("cluck_6.1", "The Golden Sunflower. Older than the barn. Older than Bessie!", 0.3),
                ("bessie_6.1b", "Careful.", 6.5)]},
            {"file": "ep05_6.2_circle.mp4", "use": 5.0, "sfx": [("RarePellet_pickup.mp3", 0.6, 0.7)], "lines": [
                ("bessie_6.2", "When we stood together, the sunflower answered.", 0.8)]},
            {"file": "ep05_6.3_cluck.mp4", "use": 5.0, "chase_music": True,
             "sfx": [("Clucky_abiltiy.mp3", 0.4, 0.8), ("Robot_damage.mp3", 2.4, 0.7)], "lines": [
                ("bessie_6.3", "Clucky got Egg Drop.", 0.2),
                ("cluck_6.3", "And it's CLUCK!", 3.3)]},
            {"file": "ep05_6.4_bessie.mp4", "use": 5.0, "hold": 0.6, "sfx": [("Bessie-ability.mp3", 1.0, 0.9)], "lines": [
                ("cluck_6.4", "Bessie got Ground Slam.", 0.4),
                ("bessie_6.4", "Now that's what I call a ground rule.", 2.6)]},
            {"file": "ep05_6.5_percy.mp4", "use": 5.0, "sfx": [("Percy_ability.mp3", 0.3, 0.8)], "lines": [
                ("percy_6.5", "Roll with it!", 3.4)]},
            {"file": "ep05_6.6_woolly.mp4", "use": 5.0, "lines": [
                ("woolly_6.6", "Ewe can never have too many of me.", 2.2),
                ("woolly_6.6_b", "", 2.27),
                ("woolly_6.6_c", "", 2.15)]},
            {"file": "ep05_6.7_ducky.mp4", "use": 5.0, "sfx": [("DuckyTeleport.mp3", 0.3, 0.8)], "lines": [
                ("ducky_6.7", "Just go with the flow, dude.", 3.2)]},
            {"file": "ep05_6.8_horace.mp4", "use": 5.0, "sfx": [("Horace_ability.mp3", 0.8, 0.8)], "lines": [
                ("horace_6.8", "Ringer!", 3.8)]},
            {"file": "ep05_6.9_gerald.mp4", "use": 5.0, "hold": 0.3, "sfx": [("Gerald_ability.mp3", 1.6, 0.6)], "lines": [
                ("gerald_6.9", "You want feathers? I'll GIVE you feathers!", 0.4)]},
            {"file": "ep05_6.10_billy.mp4", "use": 5.0, "hold": 0.3, "sfx": [("billy_ability.mp3", 0.9, 0.7)], "lines": [
                ("billy_6.10", "Okay… maybe MUD first.", 0.6)]},
            {"image": "stills/6.11_doorway.png", "use": 5.0, "push": 0.1, "hook": ["NEXT:", "MEET THE METAL"], "lines": [
                ("cluck_6.11", "Robots, meet the Farm Fury.", 0.8)]},
        ],
    },
    # Ep 6 (2026-11-03): Meet the Metal. One "wanted poster" shot per robot (the per-shot hook is the stamp), then
    # the five lined up as the golden wheat sends them running. Each robot shot also works as a solo short. Cold
    # open reuses the first 2s of the line-up clip (no extra job). Robot lines come from robot_fx.py; the five-voice
    # error line has the Scout's take on top (no caption of its own) so the words stay clear.
    6: {
        "name": "ep06_meet_the_metal",
        "clip_dir": VIDEO_DIR / "Ep06_Clips",
        "music": THEME,
        "hook": ["MEET THE", "METAL"],
        "shots": [
            {"file": "ep06_4.6_lineup.mp4", "use": 2.2, "hook": True,
             "sfx": [("RobotSpawn.mp3", 0.2, 0.8)], "lines": []},
            {"file": "ep06_4.1_harvester.mp4", "use": 5.0, "hold": 4.6, "hold_zoom": True, "chase_music": True,
             "hook": ["THE HARVESTER", "DIRECT PURSUIT"], "sfx": [("RobotSpawn.mp3", 0.3, 0.6)], "lines": [
                ("bessie_4.1", "The Harvester. The boss. It sees you, it comes for you. No tricks.", 0.3),
                ("cluck_4.1", "Just like Gerald at dinner time.", 6.9)]},
            {"file": "ep06_4.2_scout.mp4", "use": 5.0, "hold": 3.0, "hook": ["THE SCOUT", "PREDICTS YOUR PATH"], "lines": [
                ("cluck_4.2", "The Scout. Thinks it knows where you're going.", 0.2),
                ("robot_scout_4.2", "Calculating your next move…", 3.5),
                ("bessie_4.2", "Usually it does.", 5.7)]},
            {"file": "ep06_4.3_patrol.mp4", "use": 4.0, "hold": 3.4, "hook": ["THE PATROL", "THE FLANKER"], "lines": [
                ("bessie_4.3", "The Patrol. Never works alone. Its partner comes from the front; it comes from behind.", 0.3)]},
            {"file": "ep06_4.4_drifter.mp4", "use": 5.0, "hold": 2.6, "hook": ["THE DRIFTER", "MOODY"], "lines": [
                ("robot_drifter_4.4", "I'm coming for y— …actually, never mind.", 0.2),
                ("cluck_4.4", "The Drifter. Big talk from far away.", 3.9)]},
            {"file": "ep06_4.5_drone.mp4", "use": 5.0, "hold": 3.0, "hook": ["THE DRONE", "IGNORES WALLS"], "lines": [
                ("robot_drone_4.5", "Walls? How quaint. Hee hee.", 0.3),
                ("bessie_4.5", "The Drone. Floats over walls. Watch the sky.", 3.4)]},
            {"file": "ep06_4.6_lineup.mp4", "use": 7.0, "hold": 3.4, "hook": ["NEXT:", "OPERATION HARVEST HOME"],
             "hook_at": 7.0, "hook_seconds": 2.8, "sfx": [("RarePellet_pickup.mp3", 2.6, 0.8)], "lines": [
                ("cluck_4.6", "But they've got one weakness…", 0.6),
                ("bessie_4.6", "…and it grows on our farm.", 3.0),
                ("robot_all_4.6", "ERROR. ERROR. GOLDEN PROTOCOL NOT FOUND.", 5.5),
                ("robot_scout_4.6", "", 5.5)]},
        ],
    },
    # Ep 7 (2026-11-03): Operation Harvest Home, the how-to-play briefing. Three new Kling clips (Bessie's beret
    # briefing, Cluck's salute, the huddle); the chalkboard objectives and map are 1-credit stills with a push, and
    # objective two reuses Ep 6's line-up clip (the robots turning blue at the golden wheat). Hook stamps name each
    # objective so it reads as a tutorial with the sound off. Ends on all eight shouting FARM FURY (the *_7.5 takes).
    7: {
        "name": "ep07_operation_harvest_home",
        "end_card": "arcade",   # about Arcade's mazes/combos, so the end card points at Arcade only
        "clip_dir": VIDEO_DIR / "Ep07_Clips",
        "music": THEME,
        "hook": ["OPERATION", "HARVEST HOME"],
        "shots": [
            {"file": "ep07_7.1_bessie.mp4", "use": 5.0, "hold": 0.2, "hook": True, "lines": [
                ("bessie_7.1", "Listen up, farm. This is Operation Harvest Home.", 0.4)]},
            {"file": "ep07_7.2_cluck.mp4", "use": 5.0, "hold": 0.4, "lines": [
                ("cluck_7.2", "Ma'am, yes ma'am!", 0.5),
                ("bessie_7.2", "Sit down, Clucky.", 3.4)]},
            {"image": "stills/7.3a_board.png", "use": 6.5, "push": 0.12, "hook": ["OBJECTIVE 1", "COLLECT EVERY CROP"],
             "sfx": [("CornPickup.mp3", 1.0, 0.6), ("CornPickup.mp3", 1.4, 0.6)], "lines": [
                ("bessie_7.3a", "Objective one: collect every crop in the maze. Every last kernel.", 0.4)]},
            {"path": VIDEO_DIR / "Ep06_Clips/ep06_4.6_lineup.mp4", "skip": 1.5, "use": 5.5, "hold": 1.9,
             "hook": ["OBJECTIVE 2", "GRAB THE GOLDEN CROP"], "chase_music": True,
             "sfx": [("RarePellet_pickup.mp3", 1.2, 0.8)], "lines": [
                ("bessie_7.3b", "Objective two: find the golden crop. Grab it, and the robots turn blue and run.", 0.3)]},
            {"image": "stills/7.3b_board.png", "use": 10.2, "push": 0.16, "hook": ["OBJECTIVE 3", "USE THE TUNNELS"],
             "lines": [
                ("bessie_7.3c", "Objective three: the tunnels on the edges take you to the other side. Use them.", 0.3),
                ("bessie_7.3d", "And watch the factory. That's where they come from.", 6.6)]},
            {"image": "stills/7.4_map.png", "use": 9.6, "push": 0.06, "hook": ["7 WORLDS"], "caption_y": 1690, "lines": [
                ("bessie_7.4", "Four fields to start. Three farms beyond the hills.", 0.3),
                ("cluck_7.4", "Frostbite Garden. Golden Sunset. Harvest Moon!", 4.9)]},
            {"file": "ep07_7.5_huddle.mp4", "use": 5.0, "hold": 0.6, "hook": ["NEXT:", "INTO THE MAZES"],
             "hook_at": 3.0, "lines": [
                ("cluck_7.5", "FARM FURY!", 1.4),
                ("bessie_7.5solo", "", 1.435),
                ("percy_7.5", "", 1.47),
                ("woolly_7.5", "", 1.505),
                ("ducky_7.5", "", 1.54),
                ("horace_7.5", "", 1.575),
                ("gerald_7.5", "", 1.61),
                ("billy_7.5", "", 1.645)]},
        ],
    },
    # Ep 8 (2026-11-03): Into the Mazes, one world per shot, each world stamped by name. Corn Field reuses Ep 5's
    # Cluck clip and the golden-crop beat reuses Ep 6's line-up; the six other worlds are new clips started from
    # stills that used the game's own world backdrops as references. Ends on the eight at dawn (a still).
    8: {
        "name": "ep08_into_the_mazes",
        "end_card": "arcade",   # about Arcade's mazes/combos, so the end card points at Arcade only
        "clip_dir": VIDEO_DIR / "Ep08_Clips",
        "music": THEME,
        "hook": ["INTO THE MAZES"],
        "shots": [
            {"path": VIDEO_DIR / "Ep05_Clips/ep05_6.3_cluck.mp4", "use": 5.0, "hook": ["INTO THE MAZES", "CORN FIELD"],
             "sfx": [("Clucky_abiltiy.mp3", 0.4, 0.8)], "lines": [
                ("cluck_8.1", "Corn Field. MY turf.", 2.6)]},
            {"file": "ep08_8.2_percy.mp4", "use": 5.0, "hook": ["VEGETABLE PATCH"], "chase_music": True,
             "sfx": [("Percy_ability.mp3", 0.3, 0.8)], "lines": [
                ("percy_6.5", "Roll with it!", 3.3)]},
            {"file": "ep08_8.3_horace.mp4", "use": 5.0, "hook": ["ORCHARD"],
             "sfx": [("Horace_ability.mp3", 0.6, 0.8)], "lines": [
                ("horace_6.8", "Ringer!", 3.6)]},
            {"file": "ep08_8.4_woolly.mp4", "use": 5.0, "hold": 0.3, "hook": ["WHEAT FIELD"], "lines": [
                ("woolly_8.4", "Which one's the real me? …Me neither.", 1.0)]},
            {"file": "ep08_8.5_ducky.mp4", "use": 5.0, "hold": 0.2, "hook": ["FROSTBITE GARDEN"],
             "sfx": [("DuckyTeleport.mp3", 0.3, 0.8)], "lines": [
                ("ducky_8.5", "Ice, ice, ducky.", 2.6)]},
            {"file": "ep08_8.6_billy.mp4", "use": 5.0, "hook": ["GOLDEN SUNSET"],
             "sfx": [("billy_ability.mp3", 0.6, 0.7)], "lines": [
                ("billy_8.6", "Incoming! MUD!", 0.4)]},
            {"file": "ep08_8.7_gerald_bessie.mp4", "use": 5.0, "hold": 0.4, "hook": ["HARVEST MOON"],
             "sfx": [("Bessie-ability.mp3", 0.8, 0.8), ("Gerald_ability.mp3", 1.0, 0.5)], "lines": [
                ("bessie_8.7", "Harvest Moon.", 0.3),
                ("gerald_8.7", "And I'm STILL mad.", 2.9)]},
            {"path": VIDEO_DIR / "Ep06_Clips/ep06_4.6_lineup.mp4", "skip": 2.0, "use": 5.0, "hold": 0.4,
             "sfx": [("RarePellet_pickup.mp3", 0.5, 0.8)], "lines": [
                ("cluck_8.8", "Golden crop! RUN, robots, RUN!", 0.6)]},
            {"image": "stills/8.9_dawn.png", "use": 7.2, "push": 0.1, "hook": ["NEXT:", "COMBO CHAOS"], "hook_at": 5.2,
             "lines": [
                ("bessie_8.9a", "We took back the corn that night.", 0.4),
                ("cluck_8.9", "But the Combine's still out there…", 2.7),
                ("bessie_8.9b", "Then so are we.", 5.4)]},
        ],
    },
    # Ep 9 (2026-11-03): Combo Chaos. No new Kling jobs: each combo is two of Ep 5's reveal clips cut back to back
    # (the tag-out, then the tag-in), the pair named in the hook text, then the game's own combo banner popping on
    # with its combo sound. Iron Stampede uses Ep 8's Gerald and Bessie clip; Full Fury the Ep 7 huddle.
    9: {
        "name": "ep09_combo_chaos",
        "end_card": "arcade",   # about Arcade's mazes/combos, so the end card points at Arcade only
        "clip_dir": VIDEO_DIR / "Ep05_Clips",
        "music": THEME,
        "hook": ["COMBO", "CHAOS"],
        "shots": [
            {"file": "ep05_6.2_circle.mp4", "use": 3.0, "hook": True, "lines": []},
            {"file": "ep05_6.3_cluck.mp4", "skip": 0.2, "use": 2.0, "hook": ["CLUCK + WOOLLY"], "chase_music": True,
             "sfx": [("Clucky_abiltiy.mp3", 0.2, 0.7)], "lines": []},
            {"file": "ep05_6.6_woolly.mp4", "skip": 1.0, "use": 4.0, "banner": ("Combo_Featherstorm.png", 1.0, 2.6),
             "lines": []},
            {"file": "ep05_6.4_bessie.mp4", "skip": 0.6, "use": 2.2, "hook": ["BESSIE + PERCY"],
             "sfx": [("Bessie-ability.mp3", 0.4, 0.8)], "lines": []},
            {"file": "ep05_6.5_percy.mp4", "skip": 1.0, "use": 4.0, "banner": ("Combo_EarthquakeRoll.png", 1.0, 2.6),
             "sfx": [("Percy_ability.mp3", 0.1, 0.7)], "lines": []},
            {"path": VIDEO_DIR / "Ep08_Clips/ep08_8.7_gerald_bessie.mp4", "use": 5.0, "hook": ["BESSIE + GERALD"],
             "banner": ("Combo_IronStampede.png", 2.2, 2.4),
             "sfx": [("Bessie-ability.mp3", 0.8, 0.8), ("Gerald_ability.mp3", 1.0, 0.5)], "lines": []},
            {"file": "ep05_6.10_billy.mp4", "skip": 0.6, "use": 2.2, "hook": ["BILLY + HORACE"],
             "sfx": [("billy_ability.mp3", 0.3, 0.7)], "lines": []},
            {"file": "ep05_6.8_horace.mp4", "skip": 0.6, "use": 4.0, "banner": ("Combo_CrossFire.png", 1.2, 2.6),
             "sfx": [("Horace_ability.mp3", 0.2, 0.7)], "lines": []},
            {"path": VIDEO_DIR / "Ep07_Clips/ep07_7.5_huddle.mp4", "use": 5.0, "hold": 1.4,
             "hook": ["5 ANIMALS, 1 MAZE"], "banner": ("Combo_FullFury.png", 2.6, 3.6), "lines": [
                ("cluck_9.5", "Swap. Combo. FURY!", 0.4)]},
        ],
    },
    # Singles (2026-11-03): the standalone Kling character clips, now with the franchise opener, captions and the
    # two-game end card like the episodes. Render with --episode <key> (gs, rc, cc). The square/landscape
    # clips are "fit" (whole picture on a blurred copy); "skip" cuts the ~1s reference sprite each square clip opens
    # on. Line times = the voice script's "At" minus skip. Lines not recorded yet are skipped until they are.
    # (Victory Dance, Bell Boogie and Egg Drop already exist as narrated videos in Video/Narrated; those are
    # wrapped with --wrap instead, see wrap_video.)
    "gs": {
        "name": "single_bessie_ground_slam",
        "clip_dir": VIDEO_DIR,
        "music": THEME,
        "hook": ["BESSIE'S", "GROUND SLAM!"],
        "shots": [
            {"file": "GroundSlam.mp4", "skip": 1.45, "use": 8.55, "hold": 0.8, "fit": True, "hook": True,
             "hook_seconds": 2.6, "sfx": [("Bessie-ability.mp3", 1.0, 0.9)], "lines": [
                ("bessie_gs1", "GROUND... SLAM!", 0.5),
                # cluck_gs2 ("Whoa! Did the whole farm just jump?") goes at 3.0 once re-recorded: the first take
                # (2026-11-03) was the ground-rule line instead.
                ("bessie_gs1_6.4", "Now that's what I call a ground rule.", 5.2)]},
        ],
    },
    "rc": {
        "name": "single_robot_celebration",
        "clip_dir": VIDEO_DIR / "Singles",
        "music": THEME,
        "hook": ["THE ROBOTS ARE", "CELEBRATING..."],
        "shots": [
            # Robot_Celebration.mp4 is on green screen; make_singles_prep.py keys it onto the Corn Field backdrop.
            {"file": "robot_celebration_keyed.mp4", "use": 4.0, "hold": 2.6, "fit": True, "hook": True,
             "hook_seconds": 3.0, "sfx": [("RobotSpawn.mp3", 0.3, 0.7)], "lines": [
                ("robot_harvester_rc1", "Crops collected. Celebration mode... ON.", 0.6),
                ("cluck_rc2", "Not for long.", 4.7)]},
        ],
    },
    "cc": {
        "name": "single_cluck_celebration",
        "clip_dir": VIDEO_DIR,
        "music": THEME,
        "hook": ["CLUCK'S", "BIG DAY!"],
        "shots": [
            # July clip: an older Cluck design, with a Kling watermark bottom-right (cropped off).
            {"file": "Cluck_Celebration.mp4", "use": 4.0, "hold": 1.6, "fit": True, "crop": (0.04, 0.0, 0.9, 0.9),
             "hook": True, "hook_seconds": 2.6, "lines": [
                ("cluck_cc1", "We did it! Every crop's home safe!", 0.5)]},
        ],
    },
}

GAMEPLAY_GAIN = 1.8
FIT_CENTER_Y = 860        # "fit" shots: centre of the picture, between the hook text and the captions       # game sound sits in the ambience track (x AMBIENCE); louder than Kling's


def _centered(draw, text, font, y, fill, stroke):
    draw.text(((W - draw.textlength(text, font=font)) / 2, y), text, font=font, fill=fill,
              stroke_width=stroke, stroke_fill=STROKE_FILL)


def _badge(path, width):
    """Badge scaled to a width, with Google's built-in clear-space padding trimmed so both badges
    line up by their visible edges."""
    im = Image.open(path).convert("RGBA")
    im = im.crop(im.getchannel("A").getbbox())
    return im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)


def has_audio(ffmpeg, clip):
    probe = Path(ffmpeg).with_name("ffprobe.exe" if ffmpeg.lower().endswith(".exe") else "ffprobe")
    out = subprocess.run([str(probe), "-v", "error", "-select_streams", "a", "-show_entries", "stream=index",
                          "-of", "csv=p=0", str(clip)], capture_output=True, text=True).stdout
    return bool(out.strip())


def build_hook(path, hook_lines):
    """Big yellow headline across the top, like the gameplay shorts' hook text."""
    check_wording(" ".join(hook_lines))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    longest = max(hook_lines, key=len)
    font, _, line_h = fit_font(draw, longest, (60, 0, W - 60, 200), max_size=170)
    y = HOOK_TOP
    for line in hook_lines:
        _centered(draw, line, font, y, ACCENT_FILL, 10)
        y += line_h
    img.save(path)


COMBO_BANNERS = REPO / "Assets/_Project/Sprites/UI"
BANNER_W = 920
BANNER_CENTER_Y = 880


def build_banner(path, banner_png):
    """One of the game's own combo banners (Combo_*.png), enlarged and centred between the hook text and the
    captions, as a full-frame overlay."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    _paste(img, _logo(banner_png, BANNER_W, True), W / 2, BANNER_CENTER_Y)
    img.save(path)


def _logo(path, width, shadow=False):
    """A logo trimmed to its artwork and scaled to width; shadow adds a soft dark glow so it reads over a busy
    background."""
    im = Image.open(path).convert("RGBA")
    im = im.crop(im.getbbox())
    im = im.resize((width, round(width * im.height / im.width)), Image.LANCZOS)
    if not shadow:
        return im
    pad = 60
    out = Image.new("RGBA", (im.width + 2 * pad, im.height + 2 * pad), (0, 0, 0, 0))
    alpha = Image.new("L", out.size, 0)
    alpha.paste(im.getchannel("A"), (pad, pad))
    glow = Image.new("RGBA", out.size, (0, 0, 0, 0))
    glow.putalpha(alpha.filter(ImageFilter.GaussianBlur(22)).point(lambda p: min(255, int(p * 1.4))))
    out.alpha_composite(glow)
    out.alpha_composite(im, (pad, pad))
    return out


def _paste(frame, logo, cx, cy, alpha=1.0, scale=1.0):
    """logo centred on (cx, cy) at the given opacity and scale."""
    if alpha <= 0:
        return
    if scale != 1:
        logo = logo.resize((max(1, round(logo.width * scale)), max(1, round(logo.height * scale))), Image.BILINEAR)
    if alpha < 1:
        logo = logo.copy()
        logo.putalpha(logo.getchannel("A").point(lambda p: int(p * alpha)))
    frame.alpha_composite(logo, (round(cx - logo.width / 2), round(cy - logo.height / 2)))


def _recorded(stem):
    """True if a voice line has been recorded; a missing one is skipped with a note, so a video can be rendered
    before every line exists and re-rendered once it does."""
    path = (ROBOT_VOICE_DIR / f"{stem}.wav") if stem.startswith("robot_") else VOICE_DIR / f"{stem}.mp3"
    if not path.exists():
        print(f"  (not recorded yet, skipped: {stem})")
    return path.exists()


def build_end_card(path, play_only=False, arcade_only=False):
    """Blurred, darkened game poster; FARM FURY with the Arcade and Stampede logos stacked beneath, 'play both
    free', the store badges side by side, the website. Everything sits above the bottom of the frame, which TikTok
    and Shorts cover with the caption. arcade_only (Eps 7-9, which are about Arcade's mazes, tunnels and combos):
    the Arcade logo alone and 'play free now', so a maze tutorial doesn't point viewers at Stampede."""
    card = Image.open(POSTER).convert("RGBA")
    scale = max(W / card.width, H / card.height)
    card = card.resize((round(card.width * scale), round(card.height * scale)), Image.LANCZOS)
    left, top = (card.width - W) // 2, (card.height - H) // 2
    card = card.crop((left, top, left + W, top + H)).filter(ImageFilter.GaussianBlur(40))
    card = Image.alpha_composite(card, Image.new("RGBA", (W, H), (0, 0, 0, 155)))
    draw = ImageDraw.Draw(card)

    if arcade_only:
        _paste(card, _logo(ARCADE_LOGO, 640), W / 2, 560)
        _centered(draw, "PLAY FREE NOW!", ImageFont.truetype(str(FONT), 112), 1050, TEXT_FILL, 8)
    else:
        _paste(card, _logo(FARMFURY_LOGO, 640), W / 2, 230)
        _paste(card, _logo(ARCADE_LOGO, 380), W / 2, 560)
        _paste(card, _logo(STAMPEDE_LOGO, 500), W / 2, 935)
        _centered(draw, "PLAY BOTH FREE!", ImageFont.truetype(str(FONT), 104), 1110, TEXT_FILL, 8)

    badge_w, gap, y = 455, 30, 1260
    badges = [_badge(p, badge_w) for p in ((PLAY_BADGE,) if play_only else (APP_STORE_BADGE, PLAY_BADGE))]
    x = (W - len(badges) * badge_w - (len(badges) - 1) * gap) // 2
    for k, badge in enumerate(badges):
        card.alpha_composite(badge, (x + k * (badge_w + gap), y))
    y += max(b.height for b in badges)
    _centered(draw, "FARMFURYGAMES.COM", ImageFont.truetype(str(FONT), 80), y + 20, ACCENT_FILL, 6)
    card.convert("RGB").save(path)


def build_end_voices(ffmpeg, work, chorus_at=END_CHORUS_AT):
    """The end card's audio: the 'Farm Fury!' chorus and Bessie's reordered line, as (wav, seconds into the card,
    duration). Voice only, no captions: a caption would cover the store badges."""
    items, at = [], chorus_at
    voices = [prepare_voice(ffmpeg, f"{c}_farmfury", work)[0] for c in CHORUS
              if (VOICE_DIR / f"{c}_farmfury.mp3").exists()]
    if voices:
        chorus = work / "chorus.wav"
        ins = [x for v in voices for x in ("-i", str(v))]
        delays = ";".join(f"[{k}:a]adelay={int(k * CHORUS_STAGGER * 1000)}|{int(k * CHORUS_STAGGER * 1000)}[c{k}]"
                          for k in range(len(voices)))
        mix = "".join(f"[c{k}]" for k in range(len(voices)))
        run([ffmpeg, "-v", "error", "-y", *ins, "-filter_complex",
             f"{delays};{mix}amix=inputs={len(voices)}:normalize=0,volume={1.6 / len(voices) ** 0.5:.3f}",
             str(chorus)])
        d = probe_duration(ffmpeg, chorus)
        items.append((chorus, at, d))
        at += d + END_LINE_GAP
        print(f"  chorus: {len(voices)} of {len(CHORUS)} 'Farm Fury' takes found")
    else:
        print("  chorus: no <character>_farmfury.mp3 takes found, skipped")

    stem, parts = END_LINE
    src, _ = prepare_voice(ffmpeg, stem, work)
    line = work / "end_line.wav"
    pieces = ";".join(f"[0:a]atrim={lo}:{hi},asetpts=PTS-STARTPTS,afade=t=in:d=0.02,"
                      f"afade=t=out:st={hi - lo - 0.06:.2f}:d=0.06[p{k}]" for k, (lo, hi) in enumerate(parts))
    gap = f"anullsrc=r=48000:cl=stereo,atrim=0:{END_LINE_GAP}[gap]"
    order = "[gap]".join(f"[p{k}]" for k in range(len(parts)))
    run([ffmpeg, "-v", "error", "-y", "-i", str(src), "-filter_complex",
         f"{pieces};{gap};{order}concat=n={2 * len(parts) - 1}:v=0:a=1", str(line)])
    d = probe_duration(ffmpeg, line)
    items.append((line, at, d))
    return items, max(END_CARD_SECONDS, at + d + END_TAIL)


def build_opener(ffmpeg, work):
    """The opener: the 'Farm Fury!' shout over Bessie's slam thud, with a fast punch-in and fading shake on the
    Ep 4 line-up still and the FARM FURY logo popping in; then the scene dims while the Arcade and Stampede logos
    fade in stacked, and everything fades out before the story."""
    frames_dir = work / "opener_frames"
    frames_dir.mkdir()
    still = Image.open(OPENER_STILL).convert("RGB")
    base = still.resize((W, int(W * still.height / still.width) + 2), Image.LANCZOS)
    ff, arcade, stampede = _logo(FARMFURY_LOGO, 900), _logo(ARCADE_LOGO, 470, True), _logo(STAMPEDE_LOGO, 600, True)

    def ease(x):
        x = max(0.0, min(1.0, x))
        return 1 - (1 - x) ** 3

    def shown(t, a, fin):
        """Opacity of something fading in at a and out with everything else at the end."""
        if t < a:
            return 0.0
        return max(0.0, min(1.0, (t - a) / fin, (OPENER_SECONDS - t) / 0.4))

    random.seed(4)
    cx, cy = base.width / 2, base.height * 0.60
    for i in range(int(OPENER_SECONDS * FPS)):
        t = i / FPS
        zoom = 1.06 + 0.14 * ease(t / 0.22) + 0.03 * t / OPENER_SECONDS
        shake = max(0, 1 - t / 0.45) * 14
        cw, ch = W / zoom, H / zoom
        x = min(max(0, cx - cw / 2 + random.uniform(-shake, shake)), base.width - cw)
        y = min(max(0, cy - ch / 2 + random.uniform(-shake, shake)), base.height - ch)
        frame = base.crop((int(x), int(y), int(x + cw), int(y + ch))).resize((W, H), Image.BILINEAR).convert("RGBA")
        pop = 0.85 + 0.15 * ease(t / 0.25)
        _paste(frame, ff, W / 2, 330, shown(t, 0, 0.08), pop)
        games = shown(t, OPENER_ARCADE_AT, OPENER_FADE)
        if games > 0:   # dim the busy scene so the game logos read; redraw FARM FURY on top of the dim
            frame.alpha_composite(Image.new("RGBA", (W, H), (0, 0, 0, int(150 * games))))
            _paste(frame, ff, W / 2, 330, games * shown(t, 0, 0.08), pop)
        _paste(frame, arcade, W / 2, 810, games, 0.9 + 0.1 * ease((t - OPENER_ARCADE_AT) / OPENER_FADE))
        _paste(frame, stampede, W / 2, 1250, shown(t, OPENER_STAMPEDE_AT, OPENER_FADE),
               0.9 + 0.1 * ease((t - OPENER_STAMPEDE_AT) / OPENER_FADE))
        frame.convert("RGB").save(frames_dir / f"{i:04d}.png")

    voices = []
    for c in CHORUS:
        if (VOICE_DIR / f"{c}_farmfury.mp3").exists():
            path = prepare_voice(ffmpeg, f"{c}_farmfury", work)[0]
            if c in OPENER_SPEED:   # a take too long for the opener
                fast = work / f"{c}_farmfury_fast.wav"
                run([ffmpeg, "-v", "error", "-y", "-i", str(path), "-af", f"atempo={OPENER_SPEED[c]}", str(fast)])
                path = fast
            voices.append(path)
    ins = [x for v in voices for x in ("-i", str(v))]
    delays = ";".join(f"[{k + 1}:a]adelay={int((OPENER_SHOUT_AT + k * CHORUS_STAGGER) * 1000)}|"
                      f"{int((OPENER_SHOUT_AT + k * CHORUS_STAGGER) * 1000)}[c{k}]" for k in range(len(voices)))
    mix = "".join(f"[c{k}]" for k in range(len(voices)))
    thud = len(voices) + 1      # input index, after the frames
    opener = work / "opener.mp4"
    music = thud + 1
    run([ffmpeg, "-v", "error", "-y", "-framerate", str(FPS), "-i", str(frames_dir / "%04d.png"), *ins,
         "-i", str(SFX / "Bessie-ability.mp3"), "-t", str(OPENER_SECONDS), "-i", str(THEME), "-filter_complex",
         f"{delays};{mix}amix=inputs={len(voices)}:normalize=0,volume={1.6 / len(voices) ** 0.5:.3f}[shout];"
         f"[{thud}:a]aformat=sample_rates=48000:channel_layouts=stereo,atrim=0:{OPENER_SECONDS},volume=0.5[thud];"
         f"[{music}:a]aformat=sample_rates=48000:channel_layouts=stereo,volume=0.25,afade=t=in:st=0.9:d=0.6,"
         f"afade=t=out:st={OPENER_SECONDS - 0.5}:d=0.5[mus];"
         f"[shout][thud][mus]amix=inputs=3:normalize=0,alimiter=limit=0.85:level=0,apad=whole_dur={OPENER_SECONDS},atrim=0:{OPENER_SECONDS}[a]",
         "-map", "0:v", "-map", "[a]", "-t", str(OPENER_SECONDS), *VIDEO_OUT, *AUDIO_OUT, str(opener)])
    return opener


def add_opener(ffmpeg, body, out_path):
    """body (the rendered episode) with the opener in front, written to out_path."""
    with tempfile.TemporaryDirectory() as tmp:
        opener = build_opener(ffmpeg, Path(tmp))
        run([ffmpeg, "-v", "error", "-y", "-i", str(opener), "-i", str(body), "-filter_complex",
             "[0:v]setsar=1[v0];[1:v]setsar=1[v1];"
             "[v0][0:a][v1][1:a]concat=n=2:v=1:a=1[v][a]",
             "-map", "[v]", "-map", "[a]", *VIDEO_OUT, *AUDIO_OUT, "-movflags", "+faststart", str(out_path)])


WRAP_DIR = VIDEO_DIR / "Episodes" / "singles"


def wrap_video(ffmpeg, src, out_dir=None, arcade_only=False, end_music=None, end_music_start=0.0):
    """end_music/end_music_start: the end card's music track and where in it to start (looped), e.g. to carry
    a gameplay short's world music on under the end card instead of switching to the theme."""
    """An already-finished video (e.g. Video/Narrated/*.mp4, with its own voices and music) given the episodes'
    framing: centred on a blurred copy of itself (if not already 9:16), the end card crossfaded on after it with
    the end-card voices and theme, and the opener in front. Its own sound is kept as it is."""
    out = (out_dir or WRAP_DIR) / src.name
    out.parent.mkdir(parents=True, exist_ok=True)
    body_s = probe_duration(ffmpeg, src)
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        end_voices, card_s = build_end_voices(ffmpeg, work, chorus_at=END_CARD_XFADE + WRAP_SHOUT_DELAY)
        end_png = work / "end_card.png"
        build_end_card(end_png, arcade_only=arcade_only)
        end_start = body_s - END_CARD_XFADE
        total = end_start + card_s
        frames = int(card_s * FPS)
        inputs = ["-i", str(src), "-loop", "1", "-framerate", str(FPS), "-t", f"{card_s:.2f}", "-i", str(end_png)]
        vg = [f"[0:v]fps={FPS},setsar=1,split[a][b];"
              f"[a]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},boxblur=24:2,eq=brightness=-0.2[bg];"
              f"[b]scale={W}:-2:flags=lanczos[fg];"
              f"[bg][fg]overlay=0:'if(gte(h,{H}),({H}-h)/2,{FIT_CENTER_Y}-h/2)',setsar=1[body]",
              f"[1:v]scale={W * 2}:{H * 2},zoompan=z='1+0.04*on/{frames}':x='iw/2-(iw/zoom/2)':"
              f"y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps={FPS},setsar=1[vend]",
              f"[body][vend]xfade=transition=fade:duration={WRAP_CARD_FADE}:offset={body_s - WRAP_CARD_FADE:.2f},"
              f"fade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT},format=yuv420p[v]"]
        labels = []
        for k, (path, at, _d) in enumerate(end_voices):
            idx = inputs.count("-i")
            inputs += ["-i", str(path)]
            ms = int((end_start + at) * 1000)
            vg.append(f"[{idx}:a]adelay={ms}|{ms}[ev{k}]")
            labels.append(f"[ev{k}]")
        mi = inputs.count("-i")
        if end_music:
            inputs += ["-stream_loop", "-1", "-ss", f"{end_music_start:.2f}", "-t", f"{card_s + 2:.2f}",
                       "-i", str(end_music)]
        else:
            inputs += ["-t", f"{card_s + 2:.2f}", "-i", str(THEME)]
        ms = int(end_start * 1000)
        vg.append(f"[{mi}:a]loudnorm=I=-14:TP=-1.5:LRA=11,aformat=sample_rates=48000:channel_layouts=stereo,"
                  f"atrim=0:{card_s + 1:.2f},afade=t=in:d=0.8,volume={MUSIC_GAIN},adelay={ms}|{ms}[mus]")
        vg.append(f"[0:a]aformat=sample_rates=48000:channel_layouts=stereo,"
                  f"afade=t=out:st={end_start:.2f}:d={END_CARD_XFADE}[orig]")
        vg.append("[orig][mus]" + "".join(labels) + f"amix=inputs={2 + len(labels)}:normalize=0:duration=longest,"
                  f"apad=whole_dur={total:.2f},atrim=0:{total:.2f},afade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT}[aout]")
        body = work / "wrapped.mp4"
        run([ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", ";".join(vg), "-map", "[v]", "-map", "[aout]",
             "-t", f"{total:.2f}", *VIDEO_OUT, *AUDIO_OUT, str(body)])
        add_opener(ffmpeg, body, out)
    print(f"{out}  {total + OPENER_SECONDS:.1f}s")


def main():
    args = sys.argv[1:]
    if "--wrap" in args:   # python make_episode.py --wrap <video> [<video> ...]
        ffmpeg = find_ffmpeg()
        for name in args[args.index("--wrap") + 1:]:
            wrap_video(ffmpeg, Path(name))
        return
    play_only = "--play-only" in args
    key = args[args.index("--episode") + 1] if "--episode" in args else "1"
    ep = EPISODES[int(key) if key.isdigit() else key]   # numbered episodes, or a single's name (e.g. "vd")
    shots = ep["shots"]
    final_path = VIDEO_DIR / "Episodes" / (ep["name"] + ("_play_only" if play_only else "") + ".mp4")
    if not key.isdigit():   # singles live in Episodes/singles, named without the "single_" prefix
        final_path = WRAP_DIR / final_path.name.removeprefix("single_")
    out_path = final_path.parent / "no_opener" / final_path.name
    ffmpeg = find_ffmpeg()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    if "--opener-only" in args:
        if not out_path.exists():   # first run on an episode rendered before the opener existed
            shutil.move(str(final_path), str(out_path))
        add_opener(ffmpeg, out_path, final_path)
        print(f"{final_path}  {probe_duration(ffmpeg, final_path):.1f}s")
        return
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        end_voices, card_s = build_end_voices(ffmpeg, work)
        inputs, vgraph, agraph = [], [], []
        starts, t = [], 0.0
        for i, shot in enumerate(shots):
            hold = shot.get("hold", 0.0)
            if "image" in shot:
                # A still with a slow push-in (Ken Burns), for shots where a 1-credit still is enough.
                use = shot["use"]
                dur = use + hold
                frames = int(dur * FPS)
                inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{dur:.2f}", "-i", str(ep["clip_dir"] / shot["image"])]
                vgraph.append(f"[{i}:v]scale={W * 2}:{H * 2}:force_original_aspect_ratio=increase,crop={W * 2}:{H * 2},"
                              f"zoompan=z='1+{shot.get('push', 0.08)}*on/{frames}':x='iw/2-(iw/zoom/2)':"
                              f"y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps={FPS},setsar=1[v{i}]")
                agraph.append(f"anullsrc=r=48000:cl=stereo,atrim=0:{dur}[k{i}]")
            elif "gameplay" in shot:
                g_start, use = shot["gameplay"]
                dur = use + hold
                inputs += ["-ss", f"{g_start:.2f}", "-t", f"{use:.2f}", "-i", str(ep["gameplay"])]
                vgraph.append(f"[{i}:v]trim=0:{use},setpts=PTS-STARTPTS,"
                              f"crop=ih*{CROP_ASPECT}:ih:(iw-ih*{CROP_ASPECT})/2:0,setsar=1,split[gb{i}][gf{i}];"
                              f"[gb{i}]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},"
                              f"boxblur=24:2,eq=brightness=-0.18[gbb{i}];[gf{i}]scale={W}:-2[gff{i}];"
                              f"[gbb{i}][gff{i}]overlay=0:{PICTURE_CENTER_Y}-h/2,fps={FPS},setsar=1,"
                              f"tpad=stop_mode=clone:stop_duration={hold}[v{i}]")
                agraph.append(f"[{i}:a]atrim=0:{use},asetpts=PTS-STARTPTS,volume={GAMEPLAY_GAIN},"
                              "aformat=sample_rates=48000:channel_layouts=stereo,"
                              f"apad=whole_dur={dur}[k{i}]")
            else:
                clip = Path(shot["path"]) if "path" in shot else ep["clip_dir"] / shot["file"]
                if not clip.exists():
                    raise FileNotFoundError(clip)
                inputs += ["-i", str(clip)]
                skip = shot.get("skip", 0.0)
                dur = shot["use"] + hold
                zoom = ""
                if shot.get("hold_zoom") and hold:
                    # Slow push-in that starts when the clip freezes: zoompan with d=1 keeps one output frame per
                    # input frame, so it only changes the framing.
                    n0 = int(shot["use"] * FPS)
                    zoom = (f",zoompan=z='if(lt(on,{n0}),1,1+0.12*(on-{n0})/{max(1, int(hold * FPS))})':"
                            f"x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps={FPS}")
                if shot.get("fit"):
                    # A square or landscape clip (the singles): the whole picture, full width, centred at
                    # FIT_CENTER_Y on a blurred, darkened copy of itself. "crop" (fractions x, y, w, h) trims
                    # first, e.g. to cut off a Kling watermark.
                    cx, cy, cw, ch = shot.get("crop", (0, 0, 1, 1))
                    vgraph.append(f"[{i}:v]trim={skip}:{skip + shot['use']},setpts=PTS-STARTPTS,fps={FPS},"
                                  f"crop=iw*{cw}:ih*{ch}:iw*{cx}:ih*{cy},setsar=1,split[fa{i}][fb{i}];"
                                  f"[fa{i}]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},"
                                  f"boxblur=24:2,eq=brightness=-0.2[fbg{i}];[fb{i}]scale={W}:-2:flags=lanczos[ffg{i}];"
                                  f"[fbg{i}][ffg{i}]overlay=0:{FIT_CENTER_Y}-h/2,setsar=1,"
                                  f"tpad=stop_mode=clone:stop_duration={hold}{zoom},setsar=1[v{i}]")
                else:
                    vgraph.append(f"[{i}:v]trim={skip}:{skip + shot['use']},setpts=PTS-STARTPTS,fps={FPS},"
                                  f"scale={W}:{H}:flags=lanczos,setsar=1,"
                                  f"tpad=stop_mode=clone:stop_duration={hold}{zoom},setsar=1[v{i}]")
                if has_audio(ffmpeg, clip):
                    agraph.append(f"[{i}:a]atrim={skip}:{skip + shot['use']},asetpts=PTS-STARTPTS,"
                                  "aformat=sample_rates=48000:channel_layouts=stereo,"
                                  f"apad=whole_dur={dur}[k{i}]")
                else:
                    agraph.append(f"anullsrc=r=48000:cl=stereo,atrim=0:{dur}[k{i}]")
            shot["_dur"] = dur
            starts.append(t)
            t += dur - XFADE
        clips_end = t + XFADE

        # End card: a still with a slow push-in, crossfaded in after the last clip.
        end_png = work / "end_card.png"
        build_end_card(end_png, play_only, ep.get("end_card") == "arcade")
        ei = inputs.count("-i")
        inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{card_s:.2f}", "-i", str(end_png)]
        frames = int(card_s * FPS)
        vgraph.append(f"[{ei}:v]scale={W * 2}:{H * 2},zoompan=z='1+0.04*on/{frames}':x='iw/2-(iw/zoom/2)':"
                      f"y='ih/2-(ih/zoom/2)':d=1:s={W}x{H}:fps={FPS},setsar=1[vend]")
        end_start = clips_end - END_CARD_XFADE
        total = end_start + card_s

        # Join with crossfades.
        cur, acur = "v0", "k0"
        for i in range(1, len(shots)):
            off = starts[i]
            vgraph.append(f"[{cur}][v{i}]xfade=transition=fade:duration={XFADE}:offset={off:.2f}[x{i}]")
            agraph.append(f"[{acur}][k{i}]acrossfade=d={XFADE}[ka{i}]")
            cur, acur = f"x{i}", f"ka{i}"
        vgraph.append(f"[{cur}][vend]xfade=transition=fade:duration={END_CARD_XFADE}:offset={end_start:.2f}[xend]")
        cur = "xend"

        # Hook text over the cold open ("hook": True uses the episode's hook; a list gives a shot its own, e.g. a
        # "Next episode" tag on the sign-off).
        for si, shot in enumerate(shots):
            if shot.get("hook"):
                hook_png = work / f"hook_{si}.png"
                build_hook(hook_png, ep["hook"] if shot["hook"] is True else shot["hook"])
                hi = inputs.count("-i")
                inputs += ["-i", str(hook_png)]
                a = starts[si] + shot.get("hook_at", 0.0)   # hook_at: seconds into the shot (e.g. a "NEXT:" tag at the end)
                b = a + shot.get("hook_seconds", shot["_dur"] - XFADE - 0.05 - shot.get("hook_at", 0.0))
                vgraph.append(f"[{cur}][{hi}:v]overlay=0:0:enable='between(t,{a:.2f},{b:.2f})'[h{hi}]")
                cur = f"h{hi}"

        # Combo banners ("banner": (file in Sprites/UI, seconds into the shot, seconds shown)), popped on with the
        # game's combo sound.
        for si, shot in enumerate(shots):
            if shot.get("banner"):
                name, at, secs = shot["banner"]
                banner_png = work / f"banner_{si}.png"
                build_banner(banner_png, COMBO_BANNERS / name)
                bi = inputs.count("-i")
                inputs += ["-i", str(banner_png)]
                a = starts[si] + at
                vgraph.append(f"[{cur}][{bi}:v]overlay=0:0:enable='between(t,{a:.2f},{a + secs:.2f})'[bn{bi}]")
                cur = f"bn{bi}"
                shot.setdefault("sfx", []).append(("Combo.mp3", at, 0.8))

        # Voices and captions at absolute times.
        voice_labels = []
        timed_lines = [(si, starts[si], line) for si, shot in enumerate(shots) for line in shot["lines"]
                       if _recorded(line[0])]
        for li, (si, base, (stem, text, at, *part)) in enumerate(timed_lines):
            path, d = prepare_voice(ffmpeg, stem, work)
            if part:
                lo, hi = part[0]
                cut = work / f"{stem}_{li}_part.wav"
                run([ffmpeg, "-v", "error", "-y", "-i", str(path), "-af",
                     f"atrim={lo}:{hi},asetpts=PTS-STARTPTS,afade=t=in:d=0.03,afade=t=out:st={hi - lo - 0.08:.2f}:d=0.08",
                     str(cut)])
                path, d = cut, hi - lo
            a = base + at
            if text:
                cap = work / f"{stem}_{li}_cap.png"
                build_caption(stem, text, cap, shots[si].get("caption_y", 1500))
                vi = inputs.count("-i")
                inputs += ["-i", str(cap)]
                vgraph.append(f"[{cur}][{vi}:v]overlay=0:0:enable='between(t,{a:.2f},{a + d + 0.25:.2f})'[o{vi}]")
                cur = f"o{vi}"
            ai = inputs.count("-i")
            inputs += ["-i", str(path)]
            ms = int(a * 1000)
            agraph.append(f"[{ai}:a]adelay={ms}|{ms}[vo{ai}]")
            voice_labels.append(f"[vo{ai}]")
            print(f"  {a:5.1f}-{a + d:5.1f}s  {stem}")
        for path, at, d in end_voices:
            ai = inputs.count("-i")
            inputs += ["-i", str(path)]
            ms = int((end_start + at) * 1000)
            agraph.append(f"[{ai}:a]adelay={ms}|{ms}[vo{ai}]")
            voice_labels.append(f"[vo{ai}]")
            print(f"  {end_start + at:5.1f}-{end_start + at + d:5.1f}s  end card: {path.stem}")

        # Sound effects at absolute times (not part of the voice key, so they don't duck the music).
        sfx_labels = []
        for si, shot in enumerate(shots):
            for name, at, gain in shot.get("sfx", []):
                fi = inputs.count("-i")
                inputs += ["-i", str(SFX / name)]
                ms = int((starts[si] + at) * 1000)
                agraph.append(f"[{fi}:a]aformat=sample_rates=48000:channel_layouts=stereo,volume={gain},"
                              f"adelay={ms}|{ms}[sfx{fi}]")
                sfx_labels.append(f"[sfx{fi}]")
                print(f"  {starts[si] + at:5.1f}s       sfx {name}")
        if sfx_labels:
            agraph.append("".join(sfx_labels) + f"amix=inputs={len(sfx_labels)}:normalize=0,apad=whole_dur={total:.2f},atrim=0:{total:.2f}[sfx]")
        else:
            agraph.append(f"anullsrc=r=48000:cl=stereo,atrim=0:{total:.2f}[sfx]")

        # Music: the theme for the story, the chase music from the robots' arrival until the gameplay (which has
        # its own game music) or the end card, then the theme again under the end card.
        game_at = next((starts[i] for i, s in enumerate(shots) if "gameplay" in s), end_start)   # no gameplay: up to the end card
        chase_at = next((starts[i] for i, s in enumerate(shots) if s.get("chase_music")), game_at)   # optional
        level = "loudnorm=I=-14:TP=-1.5:LRA=11,aformat=sample_rates=48000:channel_layouts=stereo"
        m1, m2, m3 = (inputs.count("-i") + k for k in range(3))
        chase_len = game_at - chase_at
        # Looped (in case a stretch outlasts the track) but capped with -t: an endless input kept ffmpeg reading
        # long after its atrim had ended, turning a ~2 min render into 10+ min or a hang.
        inputs += ["-stream_loop", "-1", "-t", f"{chase_at + 2:.2f}", "-i", str(ep["music"]),
                   "-stream_loop", "-1", "-t", f"{max(chase_len, 0) + 2:.2f}", "-i", str(CHASE_MUSIC),
                   "-t", f"{card_s + 2:.2f}", "-i", str(THEME)]
        agraph.append(f"[{m1}:a]{level},atrim=0:{chase_at + 0.5:.2f},afade=t=in:d=1,"
                      f"afade=t=out:st={chase_at - 0.3:.2f}:d=0.8[mu1]")
        if chase_len > 0:
            agraph.append(f"[{m2}:a]{level},atrim=0:{chase_len + 0.6:.2f},afade=t=in:d=0.5,"
                          f"afade=t=out:st={chase_len - 0.3:.2f}:d=0.8,adelay={int(chase_at * 1000)}|{int(chase_at * 1000)}[mu2]")
        else:   # no chase stretch: the input is still opened (indices stay fixed) but not used
            agraph.append(f"[{m2}:a]anullsink")
        agraph.append(f"[{m3}:a]{level},atrim=0:{card_s + 1:.2f},afade=t=in:d=0.8,"
                      f"adelay={int(end_start * 1000)}|{int(end_start * 1000)}[mu3]")
        beds = "[mu1][mu2][mu3]" if chase_len > 0 else "[mu1][mu3]"
        agraph.append(f"{beds}amix=inputs={len(beds) // 5}:normalize=0,apad=whole_dur={total:.2f},atrim=0:{total:.2f},volume={MUSIC_GAIN}[mus]")

        agraph.append("".join(voice_labels) + f"amix=inputs={len(voice_labels)}:normalize=0,apad,"
                      f"atrim=0:{total:.2f},asplit=3[vox][key][key2]")
        agraph.append(f"[{acur}]volume={AMBIENCE},apad,afade=t=out:st={end_start:.2f}:d={END_CARD_XFADE},atrim=0:{total:.2f}[amb]")
        agraph.append("[mus][key]sidechaincompress=threshold=0.02:ratio=8:attack=15:release=400[mbed]")
        agraph.append("[amb][key2]sidechaincompress=threshold=0.02:ratio=8:attack=15:release=400[abed]")
        agraph.append("[mbed][abed][vox][sfx]amix=inputs=4:normalize=0:duration=first,"
                      f"loudnorm=I=-14:TP=-1.5:LRA=11,afade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT}[a]")
        vgraph.append(f"[{cur}]fade=t=in:d=0.4,fade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT},format=yuv420p[v]")

        run([ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", ";".join(vgraph + agraph),
             "-map", "[v]", "-map", "[a]", "-t", f"{total:.2f}", *VIDEO_OUT, *AUDIO_OUT,
             "-movflags", "+faststart", str(out_path)])
    add_opener(ffmpeg, out_path, final_path)
    print(f"{final_path}  {total + OPENER_SECONDS:.1f}s")


if __name__ == "__main__":
    main()
