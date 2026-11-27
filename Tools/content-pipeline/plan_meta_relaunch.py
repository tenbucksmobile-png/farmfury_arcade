"""Relaunch schedule for the new Farm Fury Games Facebook Page (and Instagram once it is connected).

The old Page was lost with a disabled Facebook account (2026-11-26), so this starts the Meta channels over
gently. Audience: the US and Europe (not South Africa), so the slot is 20:00 South Africa time = 18:00 UK,
19:00 central Europe, 13:00 New York, 10:00 Los Angeles. ONE post a day for the first two weeks,
photo carousels on Monday/Wednesday/Friday and the new fast-start Arcade gameplay shorts on the other days.
Episodes and the rest come after these two weeks (not planned here yet).

    python plan_meta_relaunch.py                    # starts Sat 2026-11-28, 14 days
    python plan_meta_relaunch.py --days 21

Writes plans/meta-relaunch-<start>.html and plans/meta-relaunch-<start>.json (the JSON is what Claude
reads when loading posts through Business Suite in Chrome).
"""

import argparse
import datetime as dt
import html
import json
from pathlib import Path

from make_shorts import check_wording
from plan_launch import CAR_TAGS, CAROUSEL_DIR, CAROUSEL_ORDER, CAROUSELS, VIDEO
from plan_meta import FB_CTA, FB_TAGS, IG_CTA, IG_TAGS
from plan_posts import PLANS, SHEET_STYLE

FAST_START = VIDEO / "gameplay_fast_start"
WORLDS = ["Corn Field", "Vegetable Patch", "Orchard"]
CAROUSEL_WEEKDAYS = {0, 2, 4}      # Mon, Wed, Fri

# Captions for the fast-start shorts (kid-friendly: dodge and collect, robots are never killed or eaten).
CAPTIONS = {
    ("Corn Field", 1): "Cluck drops an egg right in the robot's path 🥚🐔",
    ("Corn Field", 2): "Bessie's Ground Slam shakes the whole corn field 💥🐄",
    ("Corn Field", 3): "One Woolly? Make that three! 🐑🐑🐑",
    ("Corn Field", 4): "Ducky skips across the water to dodge the robots 🦆💦",
    ("Corn Field", 5): "Collect every crop to clear the maze 🌽",
    ("Vegetable Patch", 1): "Horace lobs a horseshoe across the veg patch 🐴",
    ("Vegetable Patch", 2): "Meet Gerald, the turkey with the Feather Burst 🦃🪶",
    ("Vegetable Patch", 3): "Percy rolls through the vegetable patch 🐷💨",
    ("Vegetable Patch", 4): "Can Horace dodge every robot? 🐴🤖",
    ("Vegetable Patch", 5): "Collect every carrot before time runs out 🥕",
    ("Orchard", 1): "Meet Billy, the goat with the Mud Sling 🐐",
    ("Orchard", 2): "Ducky's Water Spout in the orchard 🦆💦",
    ("Orchard", 3): "Percy bounces through the apple trees 🐷🍎",
    ("Orchard", 4): "Gerald's Feather Burst! 🦃🪶",
    ("Orchard", 5): "Outrun the robots and save the orchard 🍎🤖",
}


def gameplay_posts():
    out = []
    for world in WORLDS:
        for n in range(1, 6):
            found = sorted((FAST_START / world).glob(f"{world} {n:02d} - *.mp4"))
            if not found:
                continue
            body = CAPTIONS.get((world, n), found[0].stem.split(" - ", 1)[1] + "! 🌽🤖")
            out.append({"kind": f"Gameplay - {world} {n:02d}", "group": "Gameplay", "files": [str(found[0])],
                        "body": body})
    return out


def carousel_posts():
    out = []
    for key in CAROUSEL_ORDER:
        folder = CAROUSEL_DIR / key
        files = sorted(str(p) for p in folder.glob("*.png"))
        if not files:
            continue
        title, body = CAROUSELS[key]
        out.append({"kind": f"Carousel - {title}", "group": "Carousel", "files": files,
                    "body": f"{title}\n{body}"})
    return out


def captions(p):
    g = p["group"]
    ig = f"{p['body']}\n\n{IG_CTA[g]}\n\n{' '.join(IG_TAGS[g])}"
    fb = f"{p['body']}\n\n{FB_CTA[g]}\n\n{' '.join(FB_TAGS[g])}"
    for text in (ig, fb):
        check_wording(text.replace("#", " "))
    return ig, fb


def build(start, days, time):
    gp, car = gameplay_posts(), carousel_posts()
    plan = []
    for d in range(days):
        day = start + dt.timedelta(days=d)
        queue = car if day.weekday() in CAROUSEL_WEEKDAYS and car else gp
        if not queue:
            continue
        p = dict(queue.pop(0))
        p["ig_caption"], p["fb_caption"] = captions(p)
        p["date"], p["time"] = day.isoformat(), time
        plan.append(p)
    return plan


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--start", default="2026-11-28")
    ap.add_argument("--days", type=int, default=14)
    ap.add_argument("--time", default="20:00")
    args = ap.parse_args()
    start = dt.date.fromisoformat(args.start)
    plan = build(start, args.days, args.time)

    PLANS.mkdir(exist_ok=True)
    stem = PLANS / f"meta-relaunch-{start.isoformat()}"
    Path(f"{stem}.json").write_text(json.dumps(plan, ensure_ascii=False, indent=1), encoding="utf-8")
    rows = []
    for i, p in enumerate(plan):
        when = dt.datetime.fromisoformat(f"{p['date']}T{p['time']}").strftime("%a %d %b %H:%M")
        files = "<br>".join(html.escape(Path(f).name) for f in p["files"][:3]) + (
            f"<br>... {len(p['files'])} images" if len(p["files"]) > 3 else "")
        rows.append(f"""
<div class="post">
  <div class="meta"><b>{when}</b> (South Africa time) &middot; {html.escape(p['kind'])}</div>
  <div class="file">{files}</div>
  <div class="label">Facebook caption</div>
  <textarea id="f{i}" rows="6" readonly>{html.escape(p['fb_caption'])}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('f{i}').value)">Copy Facebook caption</button>
  <div class="label">Instagram caption (once Instagram is connected)</div>
  <textarea id="i{i}" rows="7" readonly>{html.escape(p['ig_caption'])}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('i{i}').value)">Copy Instagram caption</button>
</div>""")
    page = (f"<!doctype html><meta charset='utf-8'><title>Farm Fury Meta relaunch</title><style>{SHEET_STYLE}</style>"
            f"<h1>Farm Fury Games: Facebook/Instagram relaunch</h1>"
            f"<p>{len(plan)} posts, one a day at {args.time} South Africa time from {start.strftime('%a %d %b')}. "
            f"Photo carousels Mon/Wed/Fri, fast-start gameplay shorts on the other days.</p>" + "".join(rows))
    Path(f"{stem}.html").write_text(page, encoding="utf-8")
    print(f"{stem}.html  ({len(plan)} posts)")
    for p in plan:
        print(p["date"], p["time"], p["kind"], f"({len(p['files'])} file{'s' if len(p['files']) > 1 else ''})")


if __name__ == "__main__":
    main()
