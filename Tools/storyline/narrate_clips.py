"""Narrated story clips: a Kling clip played full frame, as it is, with the ElevenLabs voice-over laid
on top and a quiet music bed that dips under the voices. No logo intro, headline, store bar or end
card - these are standalone story videos, not the gameplay-short template.

Voices come from <Video>/Eleven_Audio (see FarmFury_Voice_Script.md).
Output: <Video>/Narrated/<name>.mp4

Usage:
    python narrate_clips.py
    python narrate_clips.py --only egg
"""

import argparse
import subprocess
from pathlib import Path

VIDEO_DIR = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video")
VOICE_DIR = VIDEO_DIR / "Eleven_Audio"
OUT_DIR = VIDEO_DIR / "Narrated"
REPO = Path(__file__).resolve().parents[2]
MUSIC_DIR = REPO / "Assets/_Project/Audio/Music"
FFMPEG_BIN = next((REPO / "Tools/content-pipeline/bin").glob("ffmpeg-*/bin"))

MUSIC_LEVEL = 0.25        # quiet bed; it dips further while someone speaks
FADE_OUT = 0.5            # picture and sound fade at the very end

# skip: seconds of the black reference-sprite opening Kling adds, cut off.
# voice: (take file stem, from, to, at) - the phrase from..to seconds into the take plays at `at`
# seconds into the source clip. One take per line, each in its own character's voice; `to` is where
# the speech ends (trailing silence cut), and the lines are spaced so they never overlap.
CLIPS = [
    {"name": "cluck_victory_dance", "file": "ClucksVictoryDance.mp4", "skip": 1.0, "music": "Theme.mp3",
     "voice": [("cluck_vd1", 0.0, 2.32, 1.1),        # Cluck: "Every. Last. KERNEL!"
               ("cluck_vd2", 0.0, 2.93, 3.6),        # Cluck: "And for my next trick... EGGS!"
               ("bessie_vd3", 0.0, 1.90, 7.3)]},     # Bessie: "Ten out of ten, Clucky."
    {"name": "cluck_egg_drop", "file": "EggDropDodge.mp4", "skip": 0.85, "music": "Theme.mp3",
     "voice": [("cluck_ed1", 0.0, 3.62, 0.85),       # Cluck: "Robot on my tail? Time for plan EGG."
               ("cluck_ed2", 0.0, 2.53, 4.5),        # Cluck: "Whoops! Mind the yolk!"
               ("bessie_ed3", 0.0, 2.61, 7.05)]},    # Bessie: "Egg Drop. Works every time."
    {"name": "bessie_bell_boogie", "file": "BessieBellBoogie.mp4", "skip": 0.8, "music": "Theme.mp3",
     "voice": [("bessie_bb1", 0.0, 3.10, 0.85),      # Bessie: "Field cleared. Every last carrot."
               ("cluck_bb2", 0.0, 2.53, 4.05),       # Cluck: "Go Bessie! Go Bessie!"
               ("bessie_bb3", 0.0, 2.78, 6.7)]},     # Bessie: "Steady, Clucky. I've still got it."
]


def duration(path):
    out = subprocess.run([str(FFMPEG_BIN / "ffprobe.exe"), "-v", "error", "-show_entries", "format=duration",
                          "-of", "csv=p=0", str(path)], capture_output=True, text=True, check=True).stdout
    return float(out.strip())


def render(clip):
    video = VIDEO_DIR / clip["file"]
    total = duration(video) - clip["skip"]
    inputs = ["-ss", f"{clip['skip']:.2f}", "-i", str(video), "-stream_loop", "-1", "-i", str(MUSIC_DIR / clip["music"])]
    graph, labels = [], []
    for i, (stem, start, end, at) in enumerate(clip["voice"]):
        take = VOICE_DIR / f"{stem}.mp3"
        if not take.exists():
            raise FileNotFoundError(take)
        inputs += ["-i", str(take)]
        ms = max(0, int((at - clip["skip"]) * 1000))
        graph.append(f"[{i + 2}:a]atrim={start}:{end},asetpts=PTS-STARTPTS,"
                     "aformat=sample_rates=48000:channel_layouts=stereo,afade=t=in:d=0.03,"
                     f"afade=t=out:st={end - start - 0.06:.2f}:d=0.06,adelay={ms}|{ms}[p{i}]")
        labels.append(f"[p{i}]")
    graph.append("".join(labels) + f"amix=inputs={len(labels)}:normalize=0,apad,atrim=0:{total:.2f},"
                 "loudnorm=I=-15:TP=-2:LRA=11,asplit=2[vox][key]")
    graph.append("[1:a]aformat=sample_rates=48000:channel_layouts=stereo,loudnorm=I=-14:TP=-1.5:LRA=11,"
                 f"volume={MUSIC_LEVEL},afade=t=in:d=0.5,atrim=0:{total:.2f}[mus]")
    graph.append("[mus][key]sidechaincompress=threshold=0.02:ratio=10:attack=15:release=400[bed]")
    graph.append("[bed][vox]amix=inputs=2:normalize=0:duration=first,loudnorm=I=-14:TP=-1.5:LRA=11,"
                 f"afade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT}[a]")
    graph.append(f"[0:v]fade=t=in:d=0.25,fade=t=out:st={total - FADE_OUT:.2f}:d={FADE_OUT},format=yuv420p[v]")
    out = OUT_DIR / f"{clip['name']}.mp4"
    cmd = [str(FFMPEG_BIN / "ffmpeg.exe"), "-v", "error", "-y", *inputs, "-filter_complex", ";".join(graph),
           "-map", "[v]", "-map", "[a]", "-t", f"{total:.2f}",
           "-c:v", "libx264", "-preset", "medium", "-crf", "18", "-r", "30",
           "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-movflags", "+faststart", str(out)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError(f"ffmpeg failed on {clip['name']}")
    print(f"  {out.name}  {total:.1f}s")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", help="render only clips whose name contains this text")
    args = parser.parse_args()
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for clip in CLIPS:
        if not args.only or args.only in clip["name"]:
            render(clip)
    print(f"Written to {OUT_DIR}")


if __name__ == "__main__":
    main()
