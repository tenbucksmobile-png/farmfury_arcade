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
"""

import subprocess
import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "content-pipeline"))
from PIL import Image, ImageDraw, ImageFilter, ImageFont  # noqa: E402

from make_shorts import (ACCENT_FILL, AUDIO_OUT, CROP_ASPECT, FONT, FPS, H, LOGO, PICTURE_CENTER_Y,  # noqa: E402
                         POSTER, REPO, STROKE_FILL, TEXT_FILL, VIDEO_OUT, W, check_wording, fit_font)
from make_animatic import VIDEO_DIR, VOICE_DIR, build_caption, prepare_voice, probe_duration, run  # noqa: E402
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
END_LINE = ("bessie_E.1", [(1.05, 2.12), (0.0, 0.80)])
END_LINE_GAP = 0.35       # after the chorus, and between the two swapped halves
END_TAIL = 1.0            # card stays up this long after the last word
END_CARD_XFADE = 0.8
APP_STORE_BADGE = HERE / "assets" / "app-store-badge.png"
PLAY_BADGE = HERE / "assets" / "google-play-badge.png"
HOOK_TOP = 200

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
            {"file": "ep01_E_maze.mp4", "use": 8.0, "hold": 3.0, "lines": [
                ("bessie_1.6", "In one night they turned our fields into mazes. Harvest corridors, they called them.", 1.2),
                ("cluck_1.6_a", "Rude, is what I called them.", 7.6)]},
            # Ending on the game itself: Woolly dodging robots, a power crop sending them running, Level Complete.
            {"gameplay": (210.6, 2.6), "lines": [("cluck_6.11", "Robots, meet the Farm Fury.", 0.15)]},
            {"gameplay": (455.0, 2.4), "lines": []},
            {"gameplay": (471.2, 3.0), "lines": []},
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
            # Gameplay: dodging, more dodging, then unlocking a new animal (Ducky).
            {"gameplay": (278.3, 2.4), "lines": [("cluck_5.5b", "So who's with me?!", 0.35)]},
            {"gameplay": (311.8, 2.4), "lines": []},
            {"gameplay": (338.8, 3.0), "lines": []},
        ],
    },
}

GAMEPLAY_GAIN = 1.8       # game sound sits in the ambience track (x AMBIENCE); louder than Kling's


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


def build_end_card(path, play_only=False):
    """Blurred, darkened game poster; logo, 'play free', both store badges, the website."""
    card = Image.open(POSTER).convert("RGBA")
    scale = max(W / card.width, H / card.height)
    card = card.resize((round(card.width * scale), round(card.height * scale)), Image.LANCZOS)
    left, top = (card.width - W) // 2, (card.height - H) // 2
    card = card.crop((left, top, left + W, top + H)).filter(ImageFilter.GaussianBlur(40))
    card = Image.alpha_composite(card, Image.new("RGBA", (W, H), (0, 0, 0, 155)))
    draw = ImageDraw.Draw(card)

    logo_size = 640
    logo = Image.open(LOGO).convert("RGBA").resize((logo_size, logo_size), Image.LANCZOS)
    card.alpha_composite(logo, ((W - logo_size) // 2, 230))
    _centered(draw, "PLAY FREE NOW!", ImageFont.truetype(str(FONT), 124), 930, TEXT_FILL, 8)

    badge_w = 600
    y = 1130
    for badge_path in ((PLAY_BADGE,) if play_only else (APP_STORE_BADGE, PLAY_BADGE)):
        badge = _badge(badge_path, badge_w)
        card.alpha_composite(badge, ((W - badge_w) // 2, y))
        y += badge.height + 50
    _centered(draw, "FARMFURYGAMES.COM", ImageFont.truetype(str(FONT), 80), y + 40, ACCENT_FILL, 6)
    card.convert("RGB").save(path)


def build_end_voices(ffmpeg, work):
    """The end card's audio: the 'Farm Fury!' chorus and Bessie's reordered line, as (wav, seconds into the card,
    duration). Voice only, no captions: a caption would cover the store badges."""
    items, at = [], END_CHORUS_AT
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


def main():
    args = sys.argv[1:]
    play_only = "--play-only" in args
    ep = EPISODES[int(args[args.index("--episode") + 1]) if "--episode" in args else 1]
    shots = ep["shots"]
    out_path = VIDEO_DIR / "Episodes" / (ep["name"] + ("_play_only" if play_only else "") + ".mp4")
    ffmpeg = find_ffmpeg()
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        end_voices, card_s = build_end_voices(ffmpeg, work)
        inputs, vgraph, agraph = [], [], []
        starts, t = [], 0.0
        for i, shot in enumerate(shots):
            hold = shot.get("hold", 0.0)
            if "gameplay" in shot:
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
        build_end_card(end_png, play_only)
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

        # Hook text over the cold open.
        for si, shot in enumerate(shots):
            if shot.get("hook"):
                hook_png = work / "hook.png"
                build_hook(hook_png, ep["hook"])
                hi = inputs.count("-i")
                inputs += ["-i", str(hook_png)]
                a = starts[si]
                b = a + shot.get("hook_seconds", shot["_dur"] - XFADE / 2)
                vgraph.append(f"[{cur}][{hi}:v]overlay=0:0:enable='between(t,{a:.2f},{b:.2f})'[h{hi}]")
                cur = f"h{hi}"

        # Voices and captions at absolute times.
        voice_labels = []
        timed_lines = [(si, starts[si], line) for si, shot in enumerate(shots) for line in shot["lines"]]
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
        agraph.append("".join(sfx_labels) + f"amix=inputs={len(sfx_labels)}:normalize=0,apad,atrim=0:{total:.2f}[sfx]")

        # Music: the theme for the story, the chase music from the robots' arrival until the gameplay (which has
        # its own game music), then the theme again under the end card.
        chase_at = next(starts[i] for i, s in enumerate(shots) if s.get("chase_music"))
        game_at = next(starts[i] for i, s in enumerate(shots) if "gameplay" in s)
        level = "loudnorm=I=-14:TP=-1.5:LRA=11,aformat=sample_rates=48000:channel_layouts=stereo"
        m1, m2, m3 = (inputs.count("-i") + k for k in range(3))
        chase_len = game_at - chase_at
        # Looped (in case a stretch outlasts the track) but capped with -t: an endless input kept ffmpeg reading
        # long after its atrim had ended, turning a ~2 min render into 10+ min or a hang.
        inputs += ["-stream_loop", "-1", "-t", f"{chase_at + 2:.2f}", "-i", str(ep["music"]),
                   "-stream_loop", "-1", "-t", f"{chase_len + 2:.2f}", "-i", str(CHASE_MUSIC),
                   "-t", f"{card_s + 2:.2f}", "-i", str(THEME)]
        agraph.append(f"[{m1}:a]{level},atrim=0:{chase_at + 0.5:.2f},afade=t=in:d=1,"
                      f"afade=t=out:st={chase_at - 0.3:.2f}:d=0.8[mu1]")
        agraph.append(f"[{m2}:a]{level},atrim=0:{chase_len + 0.6:.2f},afade=t=in:d=0.5,"
                      f"afade=t=out:st={chase_len - 0.3:.2f}:d=0.8,adelay={int(chase_at * 1000)}|{int(chase_at * 1000)}[mu2]")
        agraph.append(f"[{m3}:a]{level},atrim=0:{card_s + 1:.2f},afade=t=in:d=0.8,"
                      f"adelay={int(end_start * 1000)}|{int(end_start * 1000)}[mu3]")
        agraph.append(f"[mu1][mu2][mu3]amix=inputs=3:normalize=0,apad,atrim=0:{total:.2f},volume={MUSIC_GAIN}[mus]")

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
    print(f"{out_path}  {total:.1f}s")


if __name__ == "__main__":
    main()
