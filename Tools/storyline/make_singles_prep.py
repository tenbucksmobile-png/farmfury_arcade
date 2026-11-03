"""Prepares Robot_Celebration.mp4 (a Kling clip on green screen) for the "rc" single in make_episode.py: crops
off the Kling watermark, keys out the green and puts the robot over the game's Corn Field backdrop, darkened
to night. Output: <Video>/Singles/robot_celebration_keyed.mp4 (silent, same length).

Usage: python make_singles_prep.py
"""

from pathlib import Path

from make_animatic import VIDEO_DIR, run
from normalize_video import find_ffmpeg

HERE = Path(__file__).resolve().parent
BACKDROP = HERE.parents[1] / "Assets/_Project/Sprites/UI/World1_Cornfield.png"
SRC = VIDEO_DIR / "Robot_Celebration.mp4"
OUT = VIDEO_DIR / "Singles" / "robot_celebration_keyed.mp4"
GREEN = "0x35E916"   # sampled from the clip


def main():
    OUT.parent.mkdir(exist_ok=True)
    ffmpeg = find_ffmpeg()
    run([ffmpeg, "-v", "error", "-y", "-i", str(SRC), "-loop", "1", "-i", str(BACKDROP), "-filter_complex",
         "[1:v]scale=1280:720:force_original_aspect_ratio=increase,crop=1280:720,eq=brightness=-0.12,setsar=1[bg];"
         "[0:v]crop=iw*0.92:ih*0.92:iw*0.04:0,scale=1280:720,"
         f"colorkey={GREEN}:0.32:0.12,despill=type=green[fg];"
         "[bg][fg]overlay=0:0:shortest=1,format=yuv420p[v]",
         "-map", "[v]", "-an", "-c:v", "libx264", "-crf", "16", "-preset", "medium", str(OUT)])
    print(OUT)


if __name__ == "__main__":
    main()
