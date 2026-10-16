"""Robot voices: take each plain robot take and add its robot effect (pitch, metallic ring, echo,
wobble, radio), then layer the five 4.6 takes into one glitching chorus.

Sources come from <Video>/Eleven_Audio/robot_<name>_<clip>.mp3 (ElevenLabs). A line with no
ElevenLabs take falls back to a Windows text-to-speech placeholder (robot_<name>_<clip>.wav, made by
this script with the built-in David/Zira voices); drop an .mp3 with the same name in and re-run to
replace it.

Output: <Video>/Robot_Voices/robot_<name>_<clip>.wav  (+ robot_all_4.6.wav)

Usage:
    python robot_fx.py
    python robot_fx.py --only drone
    python robot_fx.py --remake-tts     # regenerate the Windows placeholders
"""

import argparse
import subprocess
from pathlib import Path

VIDEO_DIR = Path(r"C:\Users\Personel\Desktop\FarmFury_Technical\FarmFury_Artwork\Video")
VOICE_DIR = VIDEO_DIR / "Eleven_Audio"
OUT_DIR = VIDEO_DIR / "Robot_Voices"
REPO = Path(__file__).resolve().parents[2]
FFMPEG_BIN = next((REPO / "Tools/content-pipeline/bin").glob("ffmpeg-*/bin"))
FFMPEG = str(FFMPEG_BIN / "ffmpeg.exe")

# ffmpeg's FFT "robotize": keeps each frequency's strength, flattens its phase -> buzzy monotone.
ROBOTIZE = ("afftfilt=real='hypot(re,im)*sin(0)':imag='hypot(re,im)*cos(0)'"
            ":win_size={w}:overlap=0.75")


def pitch(factor):
    """Shift pitch by `factor` without changing speed."""
    return f"asetrate=48000*{factor},aresample=48000,atempo={1 / factor:.4f}"


def robot(dry, wet, win=512):
    """Blend the plain voice (dry) with its robotized copy (wet)."""
    return (f"asplit=2[d][r];[r]{ROBOTIZE.format(w=win)},volume={wet}[rw];"
            f"[d]volume={dry}[dw];[dw][rw]amix=inputs=2:normalize=0")


# Per-robot effect chain, applied after the take is converted to 48 kHz mono.
FX = {
    "combine":   f"{pitch(0.88)},{robot(0.9, 0.3, 1024)},aecho=0.8:0.6:50:0.3,aecho=0.8:0.4:350:0.12",
    "harvester": f"{pitch(0.86)},{robot(0.5, 0.8)},acrusher=bits=10:mix=0.25,aecho=0.8:0.6:45:0.3",
    "patrol":    f"{robot(0.6, 0.6)},highpass=f=350,lowpass=f=3200,acrusher=bits=8:mix=0.15,aecho=0.8:0.5:30:0.25",
    "scout":     f"{pitch(1.08)},{robot(0.6, 0.6, 256)},highpass=f=250,aecho=0.8:0.5:25:0.3",
    "drifter":   f"{pitch(1.05)},vibrato=f=5.5:d=0.45,{robot(0.6, 0.6)},aecho=0.8:0.5:35:0.3",
    "drone":     f"{pitch(1.10)},flanger=delay=1:depth=1.5:speed=2,vibrato=f=8:d=0.08,{robot(0.9, 0.25, 256)},aecho=0.8:0.4:20:0.2",
}

# Windows TTS placeholders for lines without an ElevenLabs take. Text is SSML-lite: "|" = pause.
TTS = {
    "robot_scout_4.2":       ("Microsoft Zira Desktop", -2, "Calculating. | your. | next. | move."),
    "robot_drifter_4.4":     ("Microsoft David Desktop", 1, "I'm coming for y | | actually. | never mind."),
    "robot_harvester_extra": ("Microsoft David Desktop", -3, "CROP DETECTED. | COLLECTING."),
    "robot_patrol_extra":    ("Microsoft David Desktop", 2, "Flanking procedure engaged. | Partner, go."),
}
ERROR_LINE = "ERROR. | ERROR. | GOLDEN | PROTOCOL | NOT FOUND."
CHORUS = [("combine", "Microsoft David Desktop", -3), ("harvester", "Microsoft David Desktop", -1),
          ("scout", "Microsoft Zira Desktop", 0), ("drifter", "Microsoft David Desktop", 1),
          ("drone", "Microsoft Zira Desktop", 2)]
for name, voice, rate in CHORUS:
    TTS[f"robot_{name}_4.6"] = (voice, rate, ERROR_LINE)
CHORUS_OFFSETS_MS = [0, 35, 70, 105, 140]


def make_tts(stem, voice, rate, text, out):
    ssml_body = text.replace("|", '<break time="250ms"/>').replace("&", "and")
    ssml = ('<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" xml:lang="en-US">'
            f"{ssml_body}</speak>")
    ssml_file = out.with_suffix(".ssml")   # passed by file so apostrophes can't break the quoting
    ssml_file.write_text(ssml, encoding="utf-8")
    ps = ("Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; "
          f"$s.SelectVoice('{voice}'); $s.Rate = {rate}; $s.SetOutputToWaveFile('{out}'); "
          f"$s.SpeakSsml([IO.File]::ReadAllText('{ssml_file}')); $s.Dispose()")
    try:
        subprocess.run(["powershell", "-NoProfile", "-Command", ps], check=True)
    finally:
        ssml_file.unlink(missing_ok=True)
    print(f"  placeholder {out.name}")


def source(stem, remake):
    mp3 = VOICE_DIR / f"{stem}.mp3"
    if mp3.exists():
        return mp3
    wav = VOICE_DIR / f"{stem}.wav"
    if stem in TTS and (remake or not wav.exists()):
        make_tts(stem, *TTS[stem], wav)
    return wav if wav.exists() else None


def apply_fx(src, robot_name, out):
    graph = (f"[0:a]aformat=sample_rates=48000:channel_layouts=mono,{FX[robot_name]},"
             "loudnorm=I=-16:TP=-1.5:LRA=11,apad=pad_dur=0.6[a]")
    cmd = [FFMPEG, "-v", "error", "-y", "-i", str(src), "-filter_complex", graph, "-map", "[a]",
           "-ar", "48000", str(out)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError(f"ffmpeg failed on {src.name}")
    print(f"  {out.name}")


def layer_chorus(parts, out):
    """Five robots shouting the 4.6 line a few ms apart, with a stutter on the first 'ERROR'."""
    inputs, labels, graph = [], [], []
    for i, (path, ms) in enumerate(zip(parts, CHORUS_OFFSETS_MS)):
        inputs += ["-i", str(path)]
        graph.append(f"[{i}:a]adelay={ms}|{ms}[p{i}]")
        labels.append(f"[p{i}]")
    graph.append("".join(labels) + f"amix=inputs={len(labels)}:normalize=0,asplit=3[m0][m1][m2]")
    graph.append("[m0]atrim=0:0.18,asetpts=PTS-STARTPTS[s0]")
    graph.append("[m1]atrim=0:0.18,asetpts=PTS-STARTPTS[s1]")
    graph.append("[m2]asetpts=PTS-STARTPTS[rest]")
    graph.append("[s0][s1][rest]concat=n=3:v=0:a=1,loudnorm=I=-16:TP=-1.5:LRA=11[a]")
    cmd = [FFMPEG, "-v", "error", "-y", *inputs, "-filter_complex", ";".join(graph), "-map", "[a]",
           "-ar", "48000", str(out)]
    if subprocess.run(cmd).returncode != 0:
        raise RuntimeError("ffmpeg failed layering the 4.6 chorus")
    print(f"  {out.name}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--only", help="process only robots whose name contains this text")
    parser.add_argument("--remake-tts", action="store_true", help="regenerate the Windows TTS placeholders")
    args = parser.parse_args()
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    stems = {p.stem for p in VOICE_DIR.glob("robot_*.mp3")} | set(TTS)
    for stem in sorted(stems):
        robot_name = stem.split("_")[1]
        if robot_name not in FX or (args.only and args.only not in robot_name):
            continue
        src = source(stem, args.remake_tts)
        if src is None:
            print(f"  missing {stem}")
            continue
        apply_fx(src, robot_name, OUT_DIR / f"{stem}.wav")

    chorus = [OUT_DIR / f"robot_{name}_4.6.wav" for name, _, _ in CHORUS]
    if all(p.exists() for p in chorus):
        layer_chorus(chorus, OUT_DIR / "robot_all_4.6.wav")
    print(f"Written to {OUT_DIR}")


if __name__ == "__main__":
    main()
