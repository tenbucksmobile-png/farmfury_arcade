"""Turn a recorded session's [Highlight] log lines into ranked clip candidates.

Reads session.json + logcat.txt from a session folder (written by
record_session.py) and writes:
  markers.json  every marker, with its time in seconds from the start of the video
  clips.json    candidate clips (start/end seconds), best first

Usage:
    python parse_markers.py sessions/20260924-153000
    python parse_markers.py                # newest session
"""

import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent

LINE = re.compile(r"\[Highlight\] type=(\S+) ms=(\d+)(.*)")
DETAIL = re.compile(r"(\w+)=(\S+)")

# How interesting each moment is, and how much video to keep before/after it (seconds).
# Tune these after watching a few sessions.
WEIGHTS = {
    "full_chain":       (10, 5.0, 3.0),
    "combo":            (8, 4.0, 4.0),
    "world_unlock":     (7, 1.0, 5.0),
    "character_unlock": (6, 1.0, 5.0),
    "near_miss":        (5, 3.0, 2.0),
    "level_complete":   (4, 5.0, 3.0),
    "robot_defeated":   (3, 3.0, 2.0),
    "revive":           (2, 2.0, 3.0),
    "power_pellet":     (2, 1.0, 3.0),
    "ability":          (1, 1.0, 3.0),
}
# Context only: never the reason for a clip.
CONTEXT_TYPES = {"level_start", "level_failed", "player_death"}

NEAR_MISS_SURVIVE_SECONDS = 2.0   # a near miss followed by a death this soon wasn't a near miss
MAX_CLIP_SECONDS = 25.0
MIN_CLIP_SECONDS = 6.0
DEATH_PENALTY = 4                 # clips showing a death rank lower (still kept, sometimes they're funny)


def load_markers(session_dir):
    session = json.loads((session_dir / "session.json").read_text(encoding="utf-8"))
    start_pc_ms = session["recording_start_pc_ms"]
    offset_ms = session["clock_offset_ms"]

    markers = []
    for raw in (session_dir / session["logcat"]).read_text(encoding="utf-8", errors="replace").splitlines():
        match = LINE.search(raw)
        if not match:
            continue
        kind, phone_ms, rest = match.group(1), int(match.group(2)), match.group(3)
        t = (phone_ms - offset_ms - start_pc_ms) / 1000.0
        if t < 0:
            continue  # happened before the recording started
        markers.append({"t": round(t, 2), "type": kind, **dict(DETAIL.findall(rest))})
    markers.sort(key=lambda m: m["t"])
    return markers


def drop_failed_near_misses(markers):
    deaths = [m["t"] for m in markers if m["type"] == "player_death"]
    kept = []
    for m in markers:
        if m["type"] == "near_miss" and any(0 <= d - m["t"] <= NEAR_MISS_SURVIVE_SECONDS for d in deaths):
            continue
        kept.append(m)
    return kept


def score(marker):
    weight = WEIGHTS[marker["type"]][0]
    if marker["type"] == "robot_defeated" and int(marker.get("chain", 0)) >= 2:
        weight += 2
    if marker["type"] == "full_chain":
        # Early levels have only 2 robots, so a 2-robot full chain is routine; 4+ is the real prize.
        robots = int(marker.get("robots", 2))
        weight = {2: 5, 3: 8}.get(robots, 10 if robots >= 4 else 4)
    if marker["type"] == "level_complete":
        weight += int(marker.get("stars", 0))
    return weight


def build_clips(markers):
    windows = []
    for m in markers:
        if m["type"] not in WEIGHTS:
            continue
        _, pre, post = WEIGHTS[m["type"]]
        windows.append({"start": max(0.0, m["t"] - pre), "end": m["t"] + post, "score": score(m), "moments": [m]})

    # Merge overlapping windows so one exciting stretch becomes one clip, not five.
    windows.sort(key=lambda w: w["start"])
    merged = []
    for w in windows:
        if merged and w["start"] <= merged[-1]["end"] and w["end"] - merged[-1]["start"] <= MAX_CLIP_SECONDS:
            last = merged[-1]
            last["end"] = max(last["end"], w["end"])
            last["score"] += w["score"]
            last["moments"].extend(w["moments"])
        else:
            merged.append(dict(w, moments=list(w["moments"])))

    deaths = [m["t"] for m in markers if m["type"] == "player_death"]
    clips = []
    for c in merged:
        length = c["end"] - c["start"]
        if length < MIN_CLIP_SECONDS:
            pad = (MIN_CLIP_SECONDS - length) / 2
            c["start"], c["end"] = max(0.0, c["start"] - pad), c["end"] + pad
        c["has_death"] = any(c["start"] <= d <= c["end"] for d in deaths)
        if c["has_death"]:
            c["score"] -= DEATH_PENALTY
        c["start"], c["end"] = round(c["start"], 2), round(c["end"], 2)
        c["summary"] = ", ".join(sorted({m["type"] for m in c["moments"]}))
        clips.append(c)
    clips.sort(key=lambda c: -c["score"])
    return clips


def main():
    if len(sys.argv) > 1:
        session_dir = Path(sys.argv[1]).resolve()
    else:
        sessions = sorted((HERE / "sessions").glob("*/session.json"))
        if not sessions:
            sys.exit("No sessions found. Run record_session.py first.")
        session_dir = sessions[-1].parent

    markers = load_markers(session_dir)
    if not markers:
        print("No [Highlight] markers found. Is the phone running a Development Build?")
        print(f"Check {session_dir / 'logcat.txt'} for Unity log lines.")
        return

    markers = drop_failed_near_misses(markers)
    clips = build_clips(markers)
    (session_dir / "markers.json").write_text(json.dumps(markers, indent=2), encoding="utf-8")
    (session_dir / "clips.json").write_text(json.dumps(clips, indent=2), encoding="utf-8")

    counts = {}
    for m in markers:
        counts[m["type"]] = counts.get(m["type"], 0) + 1
    print(f"{len(markers)} markers: " + ", ".join(f"{k} {v}" for k, v in sorted(counts.items())))
    print(f"{len(clips)} candidate clips (best first):")
    for i, c in enumerate(clips[:15], 1):
        flag = "  [death]" if c["has_death"] else ""
        print(f"  {i:2}. {c['start']:7.1f}s - {c['end']:7.1f}s  score {c['score']:3}  {c['summary']}{flag}")
    print(f"Written: {session_dir / 'markers.json'}, {session_dir / 'clips.json'}")


if __name__ == "__main__":
    main()
