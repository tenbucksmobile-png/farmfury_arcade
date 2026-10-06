"""Step 3: write captions for the rendered shorts and plan a posting schedule.

For every short in sessions/*/shorts/ that hasn't been planned yet, this writes a caption and
hashtags (checked with make_shorts.check_wording, so the kid-safe wording rule holds), then lays
the shorts out over the next few days, N per day, mixing the kinds so two of the same kind never
run back to back. Dodge-and-collect shorts (mix_*, short_*) are the main message and go first
in each day.

Output:
  plans/<first-day>.html   a posting sheet: day, time, video file and caption with a Copy button,
                           for scheduling in TikTok Studio (tiktok.com > Upload > Schedule, up to
                           10 days ahead) and YouTube Studio
  ledger.json              every short already planned, so it is never planned twice

Usage:
    python plan_posts.py                          # 7 days from tomorrow, 2 a day
    python plan_posts.py --days 5 --per-day 3 --start 2026-10-01
    python plan_posts.py --times 07:30,12:30,18:30
    python plan_posts.py --dry-run                # show the plan, write nothing
"""

import argparse
import datetime as dt
import html
import json
import sys
import zlib
from pathlib import Path

from make_shorts import check_wording

HERE = Path(__file__).resolve().parent
LEDGER = HERE / "ledger.json"
PLANS = HERE / "plans"

TIKTOK_HANDLE = "@farmfurygames"
DEFAULT_TIMES = ["07:30", "12:30", "18:30"]

# Caption bodies per kind of short. {hook} is the short's own on-screen headline, {character} and
# {ability} come from the file name for ability/unlock shorts. Same messaging rule as the headlines:
# dodge the robots, collect every crop; robots run or scatter, never anything harsher.
CAPTIONS = {
    "main": [
        "Dodge every Harvest Robot and collect every crop to save the farm 🌽🤖 How far can you get?",
        "One maze, a field full of crops and a lot of robots in the way 🐔 Could you clear it?",
        "Rate that escape from 1 to 10 👀 Every crop collected, every robot dodged.",
        "The robots want the harvest. The farm animals said no 🐄🌽",
        "Clear the field before the timer runs out ⏱️ Would you have made it?",
    ],
    "ability": [
        "Every animal has a special move. This is {character}'s {ability} ✨ Which one would you pick?",
        "{character} + {ability} = robots out of the way 😎 8 animals, 8 special moves.",
    ],
    "power": [
        "Grab the power crop and the robots run from YOU 🌻🏃",
        "Power crop time: watch the robots scatter 🌽💨",
    ],
    "combo": [
        "Swap animals in the right order and you get a combo 🔁 {hook}",
    ],
    "swap": [
        "Swap animals any time mid-run 🔁 In comes {character}! Who's your favourite?",
        "Pick your animal: {character} tags in 🐾 Every animal has its own special move.",
        "Feeling stuck? Swap to {character} and try a new way through the maze 🔁",
    ],
    "swap_mix": [
        "8 farm animals and you can swap between them any time 🐔🐄🐷🐑🦆 Who would you pick first?",
    ],
    "unlock": [
        "New friend on the farm! {character} just joined the team 🎉 Who should we unlock next?",
        "Unlocked {character}! Every animal plays differently 🐷🐑🦆",
    ],
}
CTA = "Farm Fury: Arcade is free on Google Play, link in bio."

HASHTAGS_BASE = ["#farmfury", "#mobilegame", "#arcadegame"]
HASHTAGS_EXTRA = {
    "main": ["#mazegame", "#retrogaming", "#androidgames"],
    "ability": ["#indiegame", "#gamedev", "#androidgames"],
    "power": ["#retrogaming", "#mazegame", "#indiegame"],
    "combo": ["#indiegame", "#gaming", "#androidgames"],
    "unlock": ["#cuteanimals", "#indiegame", "#androidgames"],
    "swap": ["#cuteanimals", "#indiegame", "#mazegame"],
    "animated": ["#cuteanimals", "#animation", "#indiegame"],
}

ABILITIES = {
    "eggdrop": ("Cluck", "Egg Drop"), "groundslam": ("Bessie", "Ground Slam"),
    "bounceroll": ("Percy", "Bounce Roll"), "tripleclone": ("Woolly", "Triple Clone"),
    "skipshot": ("Ducky", "Water Skip"), "horseshoethrow": ("Horace", "Horseshoe Throw"),
    "puffup": ("Gerald", "Puff Up"), "headbuttthrough": ("Billy", "Headbutt Charge"),
}


def kind_of(name):
    if name.startswith("swap_") or name.endswith("_swaps"):
        return "swap"
    if name.startswith(("mix_", "short_")):
        return "main"
    for prefix in ("animated", "ability", "power", "combo", "unlock"):
        if name.startswith(prefix):
            return prefix
    return "main"


def pick(options, key):
    """Stable choice per short, so re-running gives the same caption but shorts differ."""
    return options[zlib.crc32(key.encode()) % len(options)]


def write_caption(short_id, name, info):
    kind = kind_of(name)
    tail = name.split("_", 1)[1] if "_" in name else ""
    character, ability = ABILITIES.get(tail, ("", ""))
    if kind in ("unlock", "swap"):
        character = tail.capitalize()
    if info.get("caption_body"):   # animated (Kling) shorts carry their own caption
        body = info["caption_body"]
    else:
        templates = CAPTIONS["swap_mix"] if name.endswith("_swaps") else CAPTIONS[kind]
        body = pick(templates, short_id).format(
            hook=info.get("hook", "").capitalize(), character=character or "this animal",
            ability=ability or "special move")
    tags = HASHTAGS_BASE + HASHTAGS_EXTRA[kind]
    caption = f"{body}\n\n{CTA}\n\n{' '.join(tags)}"
    check_wording(caption.replace("#", " "))
    return kind, caption


def load_ledger():
    return json.loads(LEDGER.read_text(encoding="utf-8")) if LEDGER.exists() else {"planned": {}}


def find_unplanned(ledger):
    shorts = []
    for meta in sorted(HERE.glob("sessions/*/shorts/*.json")):
        video = meta.with_suffix(".mp4")
        short_id = f"{meta.parent.parent.name}/{meta.stem}"
        if not video.exists() or short_id in ledger["planned"]:
            continue
        info = json.loads(meta.read_text(encoding="utf-8"))
        kind, caption = write_caption(short_id, meta.stem, info)
        shorts.append({"id": short_id, "kind": kind, "video": str(video), "caption": caption,
                       "seconds": info.get("seconds")})
    return shorts


def order(shorts):
    """Main-message shorts first, then round-robin through the other kinds so none repeat
    back to back. Within a kind, oldest session first."""
    by_kind = {}
    for s in shorts:
        by_kind.setdefault(s["kind"], []).append(s)
    main = by_kind.pop("main", [])
    others = [by_kind[k] for k in ("animated", "swap", "ability", "power", "unlock", "combo") if k in by_kind]
    rest = []
    while any(others):
        for queue in others:
            if queue:
                rest.append(queue.pop(0))
    # Alternate main / other so every day leads with dodge-and-collect when there is enough of it.
    out = []
    while main or rest:
        if main:
            out.append(main.pop(0))
        if rest:
            out.append(rest.pop(0))
    return out


def write_sheet(plan, path):
    rows = []
    day = None
    for i, p in enumerate(plan):
        if p["day"] != day:
            day = p["day"]
            label = dt.date.fromisoformat(day).strftime("%A %d %B")
            rows.append(f"<h2>{label}</h2>")
        rows.append(f"""
<div class="post">
  <div class="meta"><b>{p['time']}</b> &middot; {html.escape(p['kind'])} &middot; {p['seconds']}s</div>
  <div class="file"><a href="file:///{html.escape(p['video'].replace(chr(92), '/'))}">{html.escape(p['video'])}</a></div>
  <textarea id="c{i}" rows="6" readonly>{html.escape(p['caption'])}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('c{i}').value)">Copy caption</button>
</div>""")
    path.write_text(f"""<!doctype html><meta charset="utf-8"><title>Farm Fury posting plan</title>
<style>
body{{font-family:system-ui,sans-serif;max-width:760px;margin:24px auto;padding:0 16px;color:#222}}
h2{{margin-top:32px;border-bottom:2px solid #e6b800}}
.post{{margin:14px 0;padding:12px;border:1px solid #ddd;border-radius:8px}}
.file{{font-size:13px;margin:4px 0 8px;word-break:break-all}}
textarea{{width:100%;box-sizing:border-box;font:14px system-ui}}
button{{margin-top:6px}}
</style>
<h1>Farm Fury posting plan ({TIKTOK_HANDLE})</h1>
<p>Schedule each video in TikTok Studio (Upload &gt; Schedule) at the time shown, paste the caption,
and switch on <i>Disclose post content &gt; Your brand</i>. Times are this PC's local time.</p>
{''.join(rows)}
""", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    tomorrow = dt.date.today() + dt.timedelta(days=1)
    parser.add_argument("--start", default=tomorrow.isoformat(), help="first day, YYYY-MM-DD (default tomorrow)")
    parser.add_argument("--days", type=int, default=7)
    parser.add_argument("--per-day", type=int, default=2)
    parser.add_argument("--times", default=",".join(DEFAULT_TIMES), help="comma-separated HH:MM slots")
    parser.add_argument("--session", help="only plan shorts from this session id (e.g. 20260924-170129)")
    parser.add_argument("--replan", action="store_true",
                        help="forget earlier plans for --session's shorts (e.g. they were never scheduled)")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    times = [t.strip() for t in args.times.split(",") if t.strip()]
    if args.per_day > len(times):
        sys.exit(f"--per-day {args.per_day} needs at least that many --times (got {times})")
    if args.per_day == 2 and times == DEFAULT_TIMES:
        times = [DEFAULT_TIMES[0], DEFAULT_TIMES[2]]   # morning and evening
    times = times[:args.per_day]

    ledger = load_ledger()
    if args.replan:
        if not args.session:
            sys.exit("--replan needs --session")
        ledger["planned"] = {k: v for k, v in ledger["planned"].items() if not k.startswith(args.session + "/")}
    shorts = find_unplanned(ledger)
    if args.session:
        shorts = [s for s in shorts if s["id"].startswith(args.session + "/")]
    shorts = order(shorts)
    # Slots already past (or within 20 minutes, too soon to schedule) are skipped.
    soon = dt.datetime.now() + dt.timedelta(minutes=20)
    slots = [(dt.date.fromisoformat(args.start) + dt.timedelta(days=d), t)
             for d in range(args.days) for t in times]
    slots = [(day, t) for day, t in slots if dt.datetime.combine(day, dt.time.fromisoformat(t)) > soon]
    plan = [dict(s, day=day.isoformat(), time=t) for s, (day, t) in zip(shorts, slots)]

    for p in plan:
        print(f"{p['day']} {p['time']}  {p['kind']:<8} {p['id']}")
    missing = len(slots) - len(plan)
    if missing > 0:
        print(f"\nOnly {len(plan)} unplanned shorts for {len(slots)} slots: {missing} short. "
              f"Record another session (about 5 minutes of play gives 8-10 shorts).")
    if args.dry_run or not plan:
        return

    PLANS.mkdir(exist_ok=True)
    sheet = PLANS / f"{args.start}.html"
    write_sheet(plan, sheet)
    for p in plan:
        ledger["planned"][p["id"]] = {"day": p["day"], "time": p["time"], "caption": p["caption"]}
    LEDGER.write_text(json.dumps(ledger, indent=2), encoding="utf-8")
    print(f"\nPosting sheet: {sheet}")


if __name__ == "__main__":
    main()
