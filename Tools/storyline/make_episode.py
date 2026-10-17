"""Storyline Episode 1 ("The Night the Robots Came") from the Kling clips: each clip trimmed to its
slot, joined in order, voice lines dropped in at set times with burned-in captions, the clips' own
Kling sound kept as quiet ambience, and a music bed that dips under the voices. 9:16, 1080x1920.
No title card and no end card - those are made separately.

Clips: <Video>/Ep01_Clips/ep01_<letter>_*.mp4. Voices: <Video>/Eleven_Audio, robot lines from
<Video>/Robot_Voices (run robot_fx.py first).
Output: <Video>/Episodes/ep01_the_night_the_robots_came.mp4

Usage:
    python make_episode.py
"""

import sys
import tempfile
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "content-pipeline"))
from make_shorts import AUDIO_OUT, FPS, H, REPO, VIDEO_OUT, W  # noqa: E402
from make_animatic import VIDEO_DIR, build_caption, prepare_voice, run  # noqa: E402
from normalize_video import find_ffmpeg  # noqa: E402

CLIP_DIR = VIDEO_DIR / "Ep01_Clips"
OUT = VIDEO_DIR / "Episodes" / "ep01_the_night_the_robots_came.mp4"
MUSIC = REPO / "Assets/_Project/Audio/Music/Theme.mp3"

XFADE = 0.4               # crossfade between clips
AMBIENCE = 0.45           # Kling's own clip sound, under everything
MUSIC_GAIN = 0.16
FADE_OUT = 0.6

# Each shot: clip file, seconds cut off its start (the frozen first frame), seconds used after that, extra seconds to hold the last frame,
# and voice lines (file stem, caption, seconds after the shot starts).
SHOTS = [
    {"file": "ep01_A_farm.mp4", "use": 14.0, "lines": [
        ("cluck_1.1", "It was harvest night. The corn was golden, the carrots were crunchy, "
                      "and everybody was fast asleep…", 0.6),
        ("bessie_1.1", "…except one chicken who'd had too much corn before bed.", 9.2)]},
    {"file": "ep01_B_landing.mp4", "use": 10.0, "lines": [
        ("robot_combine_1.4", "Target: farm. Instruction: collect every crop. Every. Single. One.", 7.0)]},
    {"file": "ep01_C_eye.mp4", "skip": 1.4, "use": 4.6, "lines": []},
    {"file": "ep01_D_robots.mp4", "skip": 0.6, "use": 4.4, "lines": [
        ("cluck_1.5_a", "And out came… them.", 0.6),
        ("bessie_1.5", "The Harvest Robots.", 3.3)]},
    {"file": "ep01_E_maze.mp4", "use": 8.0, "hold": 2.2, "lines": [
        ("bessie_1.6", "In one night they turned our fields into mazes. Harvest corridors, they called them.", 1.2),
        ("cluck_1.6_a", "Rude, is what I called them.", 7.6)]},
]


def main():
    ffmpeg = find_ffmpeg()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        work = Path(tmp)
        inputs, vgraph, agraph = [], [], []
        starts, t = [], 0.0
        for i, shot in enumerate(SHOTS):
            clip = CLIP_DIR / shot["file"]
            if not clip.exists():
                raise FileNotFoundError(clip)
            inputs += ["-i", str(clip)]
            skip = shot.get("skip", 0.0)
            dur = shot["use"] + shot.get("hold", 0.0)
            vgraph.append(f"[{i}:v]trim={skip}:{skip + shot['use']},setpts=PTS-STARTPTS,fps={FPS},"
                          f"scale={W}:{H}:flags=lanczos,setsar=1,"
                          f"tpad=stop_mode=clone:stop_duration={shot.get('hold', 0.0)}[v{i}]")
            agraph.append(f"[{i}:a]atrim={skip}:{skip + shot['use']},asetpts=PTS-STARTPTS,"
                          "aformat=sample_rates=48000:channel_layouts=stereo,"
                          f"apad=whole_dur={dur}[k{i}]")
            starts.append(t)
            t += dur - XFADE
        total = t + XFADE

        # Join with crossfades.
        cur, acur = "v0", "k0"
        for i in range(1, len(SHOTS)):
            off = starts[i]
            vgraph.append(f"[{cur}][v{i}]xfade=transition=fade:duration={XFADE}:offset={off:.2f}[x{i}]")
            agraph.append(f"[{acur}][k{i}]acrossfade=d={XFADE}[ka{i}]")
            cur, acur = f"x{i}", f"ka{i}"

        # Voices and captions at absolute times.
        voice_labels = []
        for si, shot in enumerate(SHOTS):
            for stem, text, at in shot["lines"]:
                path, d = prepare_voice(ffmpeg, stem, work)
                a = starts[si] + at
                cap = work / f"{stem}_cap.png"
                build_caption(stem, text, cap)
                vi = len(inputs) // 2
                inputs += ["-i", str(cap)]
                vgraph.append(f"[{cur}][{vi}:v]overlay=0:0:enable='between(t,{a:.2f},{a + d + 0.25:.2f})'[o{vi}]")
                cur = f"o{vi}"
                ai = len(inputs) // 2
                inputs += ["-i", str(path)]
                ms = int(a * 1000)
                agraph.append(f"[{ai}:a]adelay={ms}|{ms}[vo{ai}]")
                voice_labels.append(f"[vo{ai}]")
                print(f"  {a:5.1f}-{a + d:5.1f}s  {stem}")

        mi = len(inputs) // 2
        inputs += ["-stream_loop", "-1", "-i", str(MUSIC)]
        agraph.append("".join(voice_labels) + f"amix=inputs={len(voice_labels)}:normalize=0,apad,"
                      f"atrim=0:{total:.2f},asplit=3[vox][key][key2]")
        agraph.append(f"[{mi}:a]aformat=sample_rates=48000:channel_layouts=stereo,loudnorm=I=-14:TP=-1.5:LRA=11,"
                      f"volume={MUSIC_GAIN},afade=t=in:d=1,atrim=0:{total:.2f}[mus]")
        agraph.append(f"[{acur}]volume={AMBIENCE},atrim=0:{total:.2f}[amb]")
        agraph.append("[mus][key]sidechaincompress=threshold=0.02:ratio=8:attack=15:release=400[mbed]")
        agraph.append("[amb][key2]sidechaincompress=threshold=0.02:ratio=8:attack=15:release=400[abed]")
        agraph.append("[mbed][abed][vox]amix=inputs=3:normalize=0:duration=first,"
                      f"loudnorm=I=-14:TP=-1.5:LRA=11,afade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT}[a]")
        vgraph.append(f"[{cur}]fade=t=in:d=0.4,fade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT},format=yuv420p[v]")

        run([ffmpeg, "-v", "error", "-y", *inputs, "-filter_complex", ";".join(vgraph + agraph),
             "-map", "[v]", "-map", "[a]", "-t", f"{total:.2f}", *VIDEO_OUT, *AUDIO_OUT,
             "-movflags", "+faststart", str(OUT)])
    print(f"{OUT}  {total:.1f}s")


if __name__ == "__main__":
    main()
