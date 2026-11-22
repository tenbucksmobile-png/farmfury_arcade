"""True shorts and the narrated ability shorts (Nov 2026, after the 17 Nov analytics: viewers left at 0:02, inside
the logo opener).

The rules (Video/FarmFury_Saga/True_Shorts_List.md): action and the hook text on screen from the first frame, no
opener, one gag, a small brand bar along the bottom the whole time, and a hard cut at the end so the short loops.
Every true short ends on a short card: FARM FURY over the three games (Arcade, Stampede, Cannonfire), both store
badges, the website. The ability shorts end on the same card with Bessie's Stampede tag over it.

Each short is one entry in SHORTS: segments (gameplay windows from a recorded session, or Kling clips), the hook,
voice lines (Eleven_Audio stems, seconds from the start of the short), sound effects, an optional music bed, and the
end ("card", "tag" or None). The post caption is stored next to the video in a .json for the posting sheets.

    python make_true_short.py --list
    python make_true_short.py ab_cluck ts_boss_fight     # some
    python make_true_short.py --group ability            # a group
    python make_true_short.py --all

Output: Video/Episodes/true_shorts/<name>.mp4 (+ .json); the ability shorts go to
Video/FarmFury_Stampede/Ability_Shorts/<name>.mp4.
"""

import json
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "content-pipeline"))
from PIL import Image, ImageDraw, ImageFont  # noqa: E402

from make_shorts import (ACCENT_FILL, AUDIO_OUT, FONT, FPS, H, STROKE_FILL, TEXT_FILL, VIDEO_OUT, W,  # noqa: E402
                         check_wording, fit_font)
from make_animatic import VIDEO_DIR, build_caption, prepare_voice, probe_duration, run  # noqa: E402
from PIL import ImageFilter  # noqa: E402
from make_shorts import POSTER  # noqa: E402
from make_episode import (APP_STORE_BADGE, ARCADE_LOGO, CANNONFIRE_LOGO, CHASE_MUSIC, FARMFURY_LOGO, PLAY_BADGE, SFX,  # noqa: E402
                          STAMPEDE_LOGO, THEME, _recorded, build_end_card)
from normalize_video import find_ffmpeg  # noqa: E402

SESSIONS = HERE.parent / "content-pipeline" / "sessions"
FROZEN = SESSIONS / "20261122-172342"        # Stampede: Meadow Ruins boss + Frozen Tundra 01-09 (22 Nov 2026)
EP2, EP5, EP6 = VIDEO_DIR / "Ep02_Clips", VIDEO_DIR / "Ep05_Clips", VIDEO_DIR / "Ep06_Clips"
OUT_TRUE = VIDEO_DIR / "Episodes" / "true_shorts"
OUT_ABILITY = VIDEO_DIR / "FarmFury_Stampede" / "Ability_Shorts"

# Layout (1080x1920). The hook sits in the top band, gameplay in the middle, a voice caption under it and the brand
# bar under that; everything stays above ~1600, where TikTok and Shorts put their own caption and buttons.
HOOK_BOX = (60, 110, W - 60, 420)
PIC_Y = 820                 # centre of a gameplay or "fit" picture
CAPTION_Y = 1310
BAR_TOP = 1450
CARD_SECONDS = 2.2          # the short end card ("card"): long enough to take in the three game logos
TAG_PAD = 0.9               # the tag card: Bessie's tag plus this much
GAME_GAIN = 1.0
MUSIC_GAIN = 0.16
# Stampede's controls span the whole bottom edge and its coin counter sits at the top: crop both off (same crop as
# make_gameplay_short.py --game stampede). Arcade: the centre of the maze.
CROPS = {"stampede": "crop=ih*0.757*1.45:ih*0.757:(iw-ih*0.757*1.45)/2:ih*0.035",
         "arcade": "crop=ih*1.3:ih:(iw-ih*1.3)/2:0"}
BAR_LOGO = {"stampede": STAMPEDE_LOGO, "arcade": ARCADE_LOGO, "franchise": FARMFURY_LOGO}
TAG = ("bessie_tag-stampede", "")   # "Farm Fury: Stampede. Free on the App Store and Google Play."

ST_TAGS = "#farmfury #farmfurygames #mobilegames #platformer #familygames"
AR_TAGS = "#farmfury #farmfurygames #mobilegames #familygames #cartoon"
ST_CTA = "Farm Fury: Stampede is free on the App Store and Google Play, link in bio."
FF_CTA = "Farm Fury games are free on the App Store and Google Play, link in bio."


def gp(start, use, hook=None, src=FROZEN, game="stampede"):
    """A window of a recorded session's video.mp4 (seconds into it)."""
    return {"gameplay": (start, use), "src": src, "game": game, "hook": hook}


def clip(path, use, skip=0.0, hold=0.0, fit=False, hook=None, crop=None):
    """A Kling clip: full frame (9:16), or "fit" for a square/landscape clip (whole picture on a blurred copy)."""
    return {"clip": Path(path), "use": use, "skip": skip, "hold": hold, "fit": fit, "hook": hook, "crop": crop}


def ability(name, animal, stem, line, power, start, use, caption, part=None):
    voice = (stem, line, 0.3, part) if part else (stem, line, 0.3)
    return {"name": name, "group": "ability", "out": OUT_ABILITY, "game": "stampede",
            "hook": f"{animal}'S|{power}!", "segs": [gp(start, use)], "lines": [voice],
            "end": "tag", "caption": f"{caption} {ST_CTA} {ST_TAGS} #{animal.lower()}"}


SHORTS = [
    # --- Ability shorts: Farm Fury: Stampede (Ability_Shorts_Stampede.md). Frozen Tundra 01-09 and the Meadow Ruins
    # boss level, 22 Nov 2026. Start times are the game's log times minus 1.8 s (the log runs ahead of the video;
    # measured on two Level Complete screens and a character pick), checked on 1 fps contact sheets.
    ability("ab_cluck", "CLUCK", "cluck_ab-stampede", "Eggs away! Right on the noggin!", "EGG LAUNCH",
            236.9, 9.0, "Cluck's Egg Launch 🥚💨"),
    ability("ab_bessie", "BESSIE", "bessie_ab-stampede", "Cracked floor? Not for long.", "GROUND POUND",
            316.6, 8.0, "Bessie breaks the floor... and finds a secret 🐄💥"),
    ability("ab_percy", "PERCY", "percy_ab-stampede", "Out of my way, I'm on a roll!", "ROLL DASH",
            424.6, 8.0, "Percy's Roll Dash clears the way 🐷💨"),
    ability("ab_woolly", "WOOLLY", "woolly_ab-stampede", "A little wool cloud... and up I go!", "CLOUD STEP",
            444.8, 9.0, "Woolly makes her own cloud to stand on ☁️🐑"),
    # The only Ducky footage so far is the Meadow Ruins boss level on dry land, so only the "Wooo! Surf's up!" end of
    # his take is used ("Only duck who goes down here" needs water on screen). Swap in a water clip when recorded.
    ability("ab_ducky", "DUCKY", "ducky_ab-stampede", "Wooo! Surf's up!", "WATER SPOUT",
            138.0, 8.5, "Ducky's Water Spout knocks robots right over 🦆🌊", part=(2.15, 3.91)),
    ability("ab_horace", "HORACE", "horace_ab-stampede", "Up high? Still a ringer!", "HORSESHOE THROW",
            489.2, 8.0, "Horace's horseshoe reaches the robots up high 🐴"),
    ability("ab_gerald", "GERALD", "gerald_ab-stampede", "Feel the breeze, tin can!", "FEATHER BLOW",
            572.6, 8.0, "Gerald's Feather Blow 🦃🌬️"),
    ability("ab_billy", "BILLY", "billy_ab-stampede", "Mind the mud. Seriously. Mind it.", "MUD SLING",
            676.4, 8.0, "Billy leaves a mud trap behind him 🐐"),

    # --- True shorts from the Frozen Tundra recording (REC).
    {"name": "ts_secret_room", "group": "rec", "game": "stampede", "hook": "SECRET ROOM!",
     "segs": [gp(317.6, 5.0)], "end": "card",
     "caption": f"Bessie found a secret room under the ice 🐄❄️ {ST_CTA} {ST_TAGS}"},
    {"name": "ts_boss_fight", "group": "rec", "game": "stampede", "hook": "BOSS FIGHT!",
     "segs": [gp(207.6, 5.8)], "end": "card",
     "caption": f"Three bonks and the Commander is done 🦆 {ST_CTA} {ST_TAGS}"},
    {"name": "ts_three_stars", "group": "rec", "game": "stampede", "hook": "THREE STARS!",
     "segs": [gp(281.4, 5.5)], "end": "card",
     "caption": f"Every crop, every coin, three stars ⭐⭐⭐ {ST_CTA} {ST_TAGS}"},
    {"name": "ts_world_unlocked", "group": "rec", "game": "stampede", "hook": "NEW WORLD|UNLOCKED!",
     "segs": [gp(214.6, 5.5)], "end": "card",
     "caption": f"Next stop: the Frozen Tundra ❄️ {ST_CTA} {ST_TAGS}"},
    {"name": "ts_pick_your_animal", "group": "rec", "game": "stampede", "hook": "WHO WOULD|YOU PICK?",
     "segs": [gp(228.4, 7.6)], "end": "card",
     "caption": f"Eight animals, one run. Who would you pick? 👇 {ST_CTA} {ST_TAGS}"},

    # --- Robot comedy (Kling clips we already have).
    {"name": "ts_robot_celebration", "group": "kling", "game": "franchise", "hook": "THE ROBOTS THINK|THEY WON...",
     "music": THEME, "segs": [clip(VIDEO_DIR / "Singles/robot_celebration_keyed.mp4", 3.7, skip=0.3, hold=2.6, fit=True)],
     "lines": [("robot_harvester_rc1", "Crops collected. Celebration mode... ON.", 0.3), ("cluck_rc2", "Not for long.", 4.4)],
     "sfx": [("RobotSpawn.mp3", 0.3, 0.7)], "end": "card",
     "caption": f"The robots are celebrating a bit early 🤖🎉 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_meet_harvester", "group": "kling", "game": "franchise", "hook": "MEET THE|HARVESTER",
     "music": CHASE_MUSIC, "segs": [clip(EP6 / "ep06_4.1_harvester.mp4", 5.0, hold=2.4)],
     "lines": [("bessie_4.1", "The Harvester. The boss. It sees you, it comes for you. No tricks.", 0.3)],
     "sfx": [("RobotSpawn.mp3", 0.3, 0.6)], "end": "card",
     "caption": f"Meet the Harvester. It sees you, it comes for you 🤖 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_meet_scout", "group": "kling", "game": "franchise", "hook": "MEET THE|SCOUT",
     "music": CHASE_MUSIC, "segs": [clip(EP6 / "ep06_4.2_scout.mp4", 5.0, hold=2.4)],
     "lines": [("robot_scout_4.2", "Calculating your next move…", 0.4), ("bessie_4.2", "Usually it does.", 3.0)],
     "end": "card", "caption": f"The Scout thinks it knows where you're going 🤖 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_meet_patrol", "group": "kling", "game": "franchise", "hook": "MEET THE|PATROL",
     "music": CHASE_MUSIC, "segs": [clip(EP6 / "ep06_4.3_patrol.mp4", 4.0, hold=3.4)],
     "lines": [("bessie_4.3", "The Patrol. Never works alone. Its partner comes from the front; it comes from behind.", 0.3)],
     "end": "card", "caption": f"The Patrol never works alone 🤖 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_meet_drifter", "group": "kling", "game": "franchise", "hook": "MEET THE|DRIFTER",
     "music": CHASE_MUSIC, "segs": [clip(EP6 / "ep06_4.4_drifter.mp4", 5.0, hold=2.6)],
     "lines": [("robot_drifter_4.4", "I'm coming for y— …actually, never mind.", 0.2),
               ("cluck_4.4", "The Drifter. Big talk from far away.", 3.9)],
     "end": "card", "caption": f"The Drifter: big talk from far away 🤖 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_meet_drone", "group": "kling", "game": "franchise", "hook": "MEET THE|DRONE",
     "music": CHASE_MUSIC, "segs": [clip(EP6 / "ep06_4.5_drone.mp4", 5.0, hold=2.6)],
     "lines": [("robot_drone_4.5", "Walls? How quaint. Hee hee.", 0.3),
               ("bessie_4.5", "The Drone. Floats over walls. Watch the sky.", 3.4)],
     "end": "card", "caption": f"The Drone floats right over the walls 🤖 {FF_CTA} {AR_TAGS}"},

    # --- Animal personality: the Ep 5 power reveals, one animal each, and two favourites.
    {"name": "ts_power_cluck", "group": "kling", "game": "franchise", "hook": "CLUCK GOT|A POWER!", "music": CHASE_MUSIC,
     "segs": [clip(EP5 / "ep05_6.3_cluck.mp4", 5.0, hold=0.6)],
     "lines": [("bessie_6.3", "Clucky got Egg Drop.", 0.2), ("cluck_6.3", "And it's CLUCK!", 3.3)],
     "sfx": [("Clucky_abiltiy.mp3", 0.4, 0.8), ("Robot_damage.mp3", 2.4, 0.7)], "end": "card",
     "caption": f"Cluck got Egg Drop 🥚 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_power_bessie", "group": "kling", "game": "franchise", "hook": "BESSIE GOT|A POWER!", "music": CHASE_MUSIC,
     "segs": [clip(EP5 / "ep05_6.4_bessie.mp4", 5.0, hold=0.8)],
     "lines": [("cluck_6.4", "Bessie got Ground Slam.", 0.4), ("bessie_6.4", "Now that's what I call a ground rule.", 2.6)],
     "sfx": [("Bessie-ability.mp3", 1.0, 0.9)], "end": "card",
     "caption": f"Bessie got Ground Slam 🐄💥 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_power_percy", "group": "kling", "game": "franchise", "hook": "PERCY GOT|A POWER!", "music": CHASE_MUSIC,
     "segs": [clip(EP5 / "ep05_6.5_percy.mp4", 5.0, hold=0.4)], "lines": [("percy_6.5", "Roll with it!", 3.4)],
     "sfx": [("Percy_ability.mp3", 0.3, 0.8)], "end": "card", "caption": f"Percy rolls with it 🐷 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_power_woolly", "group": "kling", "game": "franchise", "hook": "WOOLLY GOT|A POWER!", "music": CHASE_MUSIC,
     "segs": [clip(EP5 / "ep05_6.6_woolly.mp4", 5.0, hold=0.6)],
     "lines": [("woolly_6.6", "Ewe can never have too many of me.", 2.2), ("woolly_6.6_b", "", 2.27),
               ("woolly_6.6_c", "", 2.15)], "end": "card",
     "caption": f"Woolly can never have too many of herself 🐑🐑🐑 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_power_ducky", "group": "kling", "game": "franchise", "hook": "DUCKY GOT|A POWER!", "music": CHASE_MUSIC,
     "segs": [clip(EP5 / "ep05_6.7_ducky.mp4", 5.0, hold=0.6)], "lines": [("ducky_6.7", "Just go with the flow, dude.", 3.2)],
     "sfx": [("DuckyTeleport.mp3", 0.3, 0.8)], "end": "card", "caption": f"Ducky goes with the flow 🦆🌊 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_power_horace", "group": "kling", "game": "franchise", "hook": "HORACE GOT|A POWER!", "music": CHASE_MUSIC,
     "segs": [clip(EP5 / "ep05_6.8_horace.mp4", 5.0, hold=0.4)], "lines": [("horace_6.8", "Ringer!", 3.8)],
     "sfx": [("Horace_ability.mp3", 0.8, 0.8)], "end": "card", "caption": f"Horace never misses 🐴 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_power_gerald", "group": "kling", "game": "franchise", "hook": "GERALD GOT|A POWER!", "music": CHASE_MUSIC,
     "segs": [clip(EP5 / "ep05_6.9_gerald.mp4", 5.0, hold=0.6)],
     "lines": [("gerald_6.9", "You want feathers? I'll GIVE you feathers!", 0.4)],
     "sfx": [("Gerald_ability.mp3", 1.6, 0.6)], "end": "card",
     "caption": f"Gerald has had enough of these robots 🦃 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_power_billy", "group": "kling", "game": "franchise", "hook": "BILLY GOT|A POWER!", "music": CHASE_MUSIC,
     "segs": [clip(EP5 / "ep05_6.10_billy.mp4", 5.0, hold=0.6)], "lines": [("billy_6.10", "Okay… maybe MUD first.", 0.6)],
     "sfx": [("billy_ability.mp3", 0.9, 0.7)], "end": "card", "caption": f"Billy: mud first, questions later 🐐 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_cluck_panic", "group": "kling", "game": "franchise", "hook": "CLUCK DOES NOT|HANDLE STRESS", "music": CHASE_MUSIC,
     "segs": [clip(EP2 / "ep02_2.7_chaos.mp4", 5.0), clip(EP2 / "ep02_2.7_chaos.mp4", 0.1, skip=1.55, hold=3.6)],
     "lines": [("cluck_2.7a", "It was CHAOS. Feathers! Wool! Panic! Somebody screamed!", 0.1),
               ("cluck_2.7b", "…It was a brave scream.", 7.4)], "end": "card",
     "caption": f"Cluck stayed very calm. Very calm 🐔 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_bell_boogie", "group": "kling", "game": "franchise", "hook": "DING DING DING!", "music": THEME,
     "segs": [clip(VIDEO_DIR / "BessieBellBoogie.mp4", 7.0, skip=1.4, fit=True)], "end": "card",
     "caption": f"Bessie rings the barn bell 🔔🐄 {FF_CTA} {AR_TAGS}"},
    {"name": "ts_victory_dance", "group": "kling", "game": "franchise", "hook": "VICTORY DANCE!", "music": THEME,
     "segs": [clip(VIDEO_DIR / "ClucksVictoryDance.mp4", 7.0, skip=1.4, fit=True)], "end": "card",
     "caption": f"Cluck's victory dance 🐔🕺 {FF_CTA} {AR_TAGS}"},

    # --- Comment starter: every animal, one after another.
    {"name": "ts_which_one_are_you", "group": "kling", "game": "franchise", "hook": "WHICH ONE|ARE YOU?",
     "music": CHASE_MUSIC,
     "segs": [clip(EP5 / f, 0.75, skip=2.2) for f in ("ep05_6.3_cluck.mp4", "ep05_6.4_bessie.mp4", "ep05_6.5_percy.mp4",
                                                      "ep05_6.6_woolly.mp4", "ep05_6.7_ducky.mp4", "ep05_6.8_horace.mp4",
                                                      "ep05_6.9_gerald.mp4", "ep05_6.10_billy.mp4")],
     "end": "card", "caption": f"8 farm animals, 8 powers. Which one are you? Tell us 👇 {FF_CTA} {AR_TAGS}"},
]


def build_card3(path):
    """The true shorts' end card: the episodes' Stampede-era card (FARM FURY over Arcade, Stampede and Cannonfire,
    both store badges, the website), shared so the two can't drift apart."""
    build_end_card(path, cannonfire=True)


def build_hook(path, text):
    check_wording(text.replace("|", " "))
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    font, lines, line_h = fit_font(d, text, HOOK_BOX, max_size=150, min_size=70)
    y = HOOK_BOX[1] + (HOOK_BOX[3] - HOOK_BOX[1] - len(lines) * line_h) // 2
    for line in lines:
        d.text(((W - d.textlength(line, font=font)) / 2, y), line, font=font, fill=ACCENT_FILL,
               stroke_width=max(6, font.size // 13), stroke_fill=STROKE_FILL)
        y += line_h
    img.save(path)


def build_bar(path, game):
    """The small brand bar: the game's logo and 'free on App Store & Google Play'."""
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    logo = Image.open(BAR_LOGO[game]).convert("RGBA")
    logo = logo.crop(logo.getbbox())
    k = min(300 / logo.width, 120 / logo.height)
    logo = logo.resize((round(logo.width * k), round(logo.height * k)), Image.LANCZOS)
    text = "FREE ON APP STORE & GOOGLE PLAY"
    font = ImageFont.truetype(str(FONT), 54)
    gap = 26
    while logo.width + gap + d.textlength(text, font=font) > W - 80:
        font = ImageFont.truetype(str(FONT), font.size - 2)
    left = int((W - logo.width - gap - d.textlength(text, font=font)) / 2)
    img.alpha_composite(logo, (left, BAR_TOP + (120 - logo.height) // 2))
    d.text((left + logo.width + gap, BAR_TOP + 60), text, font=font, fill=TEXT_FILL, anchor="lm",
           stroke_width=5, stroke_fill=STROKE_FILL)
    img.save(path)


def render(short, ffmpeg):
    name, game = short["name"], short["game"]
    out_dir = short.get("out", OUT_TRUE)
    out_dir.mkdir(parents=True, exist_ok=True)
    out = out_dir / f"{name}.mp4"
    segs = short["segs"]
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        tag = None
        card_s = 0.0
        if short.get("end") == "tag" and _recorded(TAG[0]):
            tag = prepare_voice(ffmpeg, TAG[0], work)
            card_s = tag[1] + TAG_PAD
        elif short.get("end"):
            card_s = CARD_SECONDS

        inputs, vg, ag = [], [], []
        starts, t = [], 0.0
        for i, s in enumerate(segs):
            last = i == len(segs) - 1
            if "gameplay" in s:
                g0, use = s["gameplay"]
                dur = use
                a_len = use + (card_s if last else 0)   # the game sound carries on under the end card
                inputs += ["-ss", f"{g0:.2f}", "-t", f"{a_len:.2f}", "-i", str(s["src"] / "video.mp4")]
                vg.append(f"[{i}:v]trim=0:{use},setpts=PTS-STARTPTS,{CROPS[s['game']]},setsar=1,split[gb{i}][gf{i}];"
                          f"[gb{i}]scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},boxblur=24:2,"
                          f"eq=brightness=-0.22[bb{i}];[gf{i}]scale={W}:-2[ff{i}];"
                          f"[bb{i}][ff{i}]overlay=0:{PIC_Y}-h/2,fps={FPS},setsar=1[v{i}]")
                ag.append(f"[{i}:a]atrim=0:{a_len},asetpts=PTS-STARTPTS,volume={GAME_GAIN},"
                          f"aformat=sample_rates=48000:channel_layouts=stereo,apad=whole_dur={a_len}[a{i}]")
            else:
                skip, use, hold = s["skip"], s["use"], s["hold"]
                dur = use + hold
                a_len = dur + (card_s if last else 0)
                inputs += ["-i", str(s["clip"])]
                crop = ""
                if s.get("crop"):
                    cx, cy, cw, ch = s["crop"]
                    crop = f"crop=iw*{cw}:ih*{ch}:iw*{cx}:ih*{cy},"
                base = f"[{i}:v]trim={skip}:{skip + use},setpts=PTS-STARTPTS,fps={FPS},{crop}setsar=1"
                if s["fit"]:
                    vg.append(f"{base},split[fa{i}][fb{i}];[fa{i}]scale={W}:{H}:force_original_aspect_ratio=increase,"
                              f"crop={W}:{H},boxblur=24:2,eq=brightness=-0.22[fbg{i}];[fb{i}]scale={W}:-2:flags=lanczos[ffg{i}];"
                              f"[fbg{i}][ffg{i}]overlay=0:{PIC_Y}-h/2,tpad=stop_mode=clone:stop_duration={hold},setsar=1[v{i}]")
                else:
                    vg.append(f"{base},scale={W}:{H}:force_original_aspect_ratio=increase,crop={W}:{H},"
                              f"tpad=stop_mode=clone:stop_duration={hold},setsar=1[v{i}]")
                # Kling's own clip sound (ambience) is left out: the music bed and effects carry these.
                ag.append(f"anullsrc=r=48000:cl=stereo,atrim=0:{a_len}[a{i}]")
            starts.append(t)
            t += dur
        body = t
        total = body + card_s

        vlabels = "".join(f"[v{i}]" for i in range(len(segs)))
        n = len(segs)
        if card_s:
            card = work / "card.png"
            build_card3(card)   # every short, ability shorts too (user, 2026-11-22), even though Bessie's tag names Stampede
            ci = inputs.count("-i")
            inputs += ["-loop", "1", "-framerate", str(FPS), "-t", f"{card_s:.2f}", "-i", str(card)]
            vg.append(f"[{ci}:v]scale={W}:{H},setsar=1,fps={FPS}[vcard]")
            vlabels += "[vcard]"
            n += 1
        vg.append(f"{vlabels}concat=n={n}:v=1:a=0[vc]")
        ag.append("".join(f"[a{i}]" for i in range(len(segs))) + f"concat=n={len(segs)}:v=0:a=1[game]")

        cur = "vc"

        def overlay(png, a, b):
            nonlocal cur
            k = inputs.count("-i")
            inputs.extend(["-i", str(png)])
            vg.append(f"[{cur}][{k}:v]overlay=0:0:enable='between(t,{a:.2f},{b:.2f})'[o{k}]")
            cur = f"o{k}"

        bar = work / "bar.png"
        build_bar(bar, game)
        overlay(bar, 0, body)
        for i, s in enumerate(segs):   # one hook png per distinct text, shown over its segments
            text = s.get("hook") or short["hook"]
            hp = work / f"hook_{i}.png"
            build_hook(hp, text)
            end = starts[i + 1] if i + 1 < len(segs) else body
            overlay(hp, starts[i], end - 0.01)

        voice_labels = []
        lines = [ln for ln in short.get("lines", []) if _recorded(ln[0])]
        for li, (stem, text, at, *part) in enumerate(lines):
            path, d = prepare_voice(ffmpeg, stem, work)
            if part:   # (from, to) seconds of the prepared (silence-trimmed) take: use only that part
                lo, hi = part[0]
                hi = min(hi, d)
                cut = work / f"{stem}_{li}_part.wav"
                run([ffmpeg, "-v", "error", "-y", "-i", str(path), "-af",
                     f"atrim={lo}:{hi},asetpts=PTS-STARTPTS,afade=t=in:d=0.03,afade=t=out:st={hi - lo - 0.08:.2f}:d=0.08",
                     str(cut)])
                path, d = cut, hi - lo
            if text:
                cap = work / f"cap_{li}.png"
                build_caption(stem, text, cap, CAPTION_Y)
                overlay(cap, at, min(at + d + 0.25, body))
            k = inputs.count("-i")
            inputs += ["-i", str(path)]
            ms = int(at * 1000)
            ag.append(f"[{k}:a]adelay={ms}|{ms}[vo{k}]")
            voice_labels.append(f"[vo{k}]")
            print(f"  {at:5.1f}-{at + d:5.1f}s  {stem}")
        if tag:
            k = inputs.count("-i")
            inputs += ["-i", str(tag[0])]
            ms = int((body + 0.3) * 1000)
            ag.append(f"[{k}:a]adelay={ms}|{ms}[vo{k}]")
            voice_labels.append(f"[vo{k}]")
            print(f"  {body + 0.3:5.1f}s        tag (end card)")

        sfx_labels = []
        for name_, at, gain in short.get("sfx", []):
            k = inputs.count("-i")
            inputs += ["-i", str(SFX / name_)]
            ms = int(at * 1000)
            ag.append(f"[{k}:a]aformat=sample_rates=48000:channel_layouts=stereo,volume={gain},adelay={ms}|{ms}[sx{k}]")
            sfx_labels.append(f"[sx{k}]")

        beds = ["[game]"]
        if short.get("music"):
            k = inputs.count("-i")
            inputs += ["-stream_loop", "-1", "-t", f"{total + 1:.2f}", "-i", str(short["music"])]
            ag.append(f"[{k}:a]loudnorm=I=-14:TP=-1.5:LRA=11,aformat=sample_rates=48000:channel_layouts=stereo,"
                      f"atrim=0:{total:.2f},volume={MUSIC_GAIN}[mus]")
            beds.append("[mus]")
        ag.append("".join(beds) + f"amix=inputs={len(beds)}:normalize=0,apad=whole_dur={total:.2f},atrim=0:{total:.2f}[bed]")
        if voice_labels:
            ag.append("".join(voice_labels) + f"amix=inputs={len(voice_labels)}:normalize=0,apad=whole_dur={total:.2f},"
                      f"atrim=0:{total:.2f},asplit=2[vox][key]")
            ag.append("[bed][key]sidechaincompress=threshold=0.02:ratio=8:attack=15:release=400[duck]")
            mix = ["[duck]", "[vox]"]
        else:
            mix = ["[bed]"]
        if sfx_labels:
            ag.append("".join(sfx_labels) + f"amix=inputs={len(sfx_labels)}:normalize=0,apad=whole_dur={total:.2f},"
                      f"atrim=0:{total:.2f}[sfx]")
            mix.append("[sfx]")
        fade = 0.4 if card_s else 0.06   # no card: just enough to avoid a click, so the loop point stays sharp
        ag.append("".join(mix) + f"amix=inputs={len(mix)}:normalize=0:duration=first,loudnorm=I=-14:TP=-1.5:LRA=11,"
                  f"afade=t=out:st={total - fade:.2f}:d={fade}[a]")
        vg.append(f"[{cur}]format=yuv420p[v]")

        run([ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", ";".join(vg + ag),
             "-map", "[v]", "-map", "[a]", "-t", f"{total:.2f}", *VIDEO_OUT, *AUDIO_OUT,
             "-movflags", "+faststart", str(out)])

    check_wording(short["caption"])
    info = {"file": out.name, "group": short["group"], "hook": short["hook"].replace("|", " "),
            "caption": short["caption"], "end": short.get("end"), "seconds": round(probe_duration(ffmpeg, out), 1)}
    out.with_suffix(".json").write_text(json.dumps(info, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"{out}  {info['seconds']}s")


def main():
    args = sys.argv[1:]
    if "--list" in args or not args:
        for s in SHORTS:
            print(f"{s['group']:8} {s['name']:24} {s['hook'].replace('|', ' ')}")
        return
    if "--all" in args:
        chosen = SHORTS
    elif "--group" in args:
        chosen = [s for s in SHORTS if s["group"] == args[args.index("--group") + 1]]
    else:
        by_name = {s["name"]: s for s in SHORTS}
        chosen = [by_name[a] for a in args]
    ffmpeg = find_ffmpeg()
    failed = []
    for s in chosen:
        print(f"\n== {s['name']}")
        try:
            render(s, ffmpeg)
        except Exception as e:   # keep going: one bad clip shouldn't stop a batch
            print(f"  FAILED: {e}")
            failed.append(s["name"])
    if failed:
        print("\nFailed: " + ", ".join(failed))


if __name__ == "__main__":
    main()
