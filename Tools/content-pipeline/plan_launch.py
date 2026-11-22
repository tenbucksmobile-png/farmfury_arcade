"""Launch sheet for every marketing video (Nov 2026): storyline Eps 1-9, the six storyline singles and the
numbered gameplay shorts (Episodes/singles/<World>/<World> NN - <title>.mp4).

The pattern agreed 2026-11-05 (revised):
  every morning 07:30       the next gameplay short, world by world in number order
                            (Corn Field 01-10, then Vegetable Patch 01-10, ...)
  Tuesday + Saturday 16:30  the next episode
  six other afternoons      a story single, next to the episode it belongs with
Runs until Ep 9 and the last single are out. After that the mornings carry on through the worlds and the
Tuesday/Saturday afternoons are free for Chapter 2 (Stampede).

A gameplay short is picked up automatically once its file exists in singles/<World>/; until then its slot
shows as TO RECORD. Re-run after rendering new ones (the dates don't move).

    python plan_launch.py                      # starts Sat 2026-11-07
    python plan_launch.py --start 2026-11-10

Writes plans/launch-<start>.html. Doesn't touch ledger.json.
"""

import argparse
import datetime as dt
import html
from pathlib import Path

from make_shorts import check_wording
from plan_posts import APP_STORE_LINK, PLANS, SHEET_STYLE, TIKTOK_HANDLE, YOUTUBE_SHORTS_LINK, YOUTUBE_STEPS

VIDEO = Path.home() / "Desktop/FarmFury_Technical/FarmFury_Artwork/Video/Episodes"
SINGLES = VIDEO / "singles"
SERIES = "Farm Fury: The Story"
CTA = "Play Farm Fury free on the App Store and Google Play, link in bio."
ARCADE_CTA = "Farm Fury: Arcade is free on the App Store and Google Play, link in bio."
STAMPEDE_CTA = "Farm Fury: Stampede is free on the App Store and Google Play, link in bio."

# Robots get dodged, outrun, busted or powered down - never anything harsher (check_wording enforces it).
EPISODES = {
    1: ("ep01_the_night_the_robots_came.mp4", "The Night the Robots Came",
        "One quiet night, the Harvest Robots landed on the farm and took every crop 🌽🤖 Who's going to stop them?"),
    2: ("ep02_chaos_on_the_farm.mp4", "Chaos on the Farm",
        "The morning after: eight farm animals, zero crops and a LOT of panic 🐔🐄🐷"),
    3: ("ep03_the_debrief.mp4", "The Debrief",
        "Live from the barn: Barnyard Breakdown replays the robot landing frame by frame 📺"),
    4: ("ep04_the_barn_meeting.mp4", "The Barn Meeting",
        "Ring the bell! All eight animals meet in the barn... and Bessie knows a secret 🔔"),
    5: ("ep05_the_golden_glow.mp4", "The Golden Glow",
        "A golden sunflower gives every animal a special power ✨🌻 Which power is your favourite?"),
    6: ("ep06_meet_the_metal.mp4", "Meet the Metal",
        "WANTED: six Harvest Robots, each with its own trick 🤖 Which one would you dodge first?"),
    7: ("ep07_operation_harvest_home.mp4", "Operation Harvest Home",
        "The plan: get into the maze, collect every crop, dodge every robot 🗺️ Ready?"),
    8: ("ep08_into_the_mazes.mp4", "Into the Mazes",
        "Seven worlds of mazes, from the Corn Field to Harvest Moon 🌽🍎🌾 Which one looks hardest?"),
    9: ("ep09_combo_chaos.mp4", "Combo Chaos",
        "Tag-team two animals in the right order and you get a COMBO 🔁💥 Which pair would you pick?"),
    # Chapter 2 (Stampede) starts here: the bridge from Arcade.
    10: ("ep10_the_march.mp4", "The March",
         "The robots ran home... so the farm is going after them 🐄🐔 Chapter 2: STAMPEDE starts now!"),
    11: ("ep11_new_moves.mp4", "New Moves",
         "Out here you don't stand still: every animal's power works on the run 🥚💨🐑☁️ Which one is your favourite?"),
}
EP_TAGS = ["#farmfury", "#animation", "#cartoon", "#farmanimals", "#mobilegame"]
EPISODE_WEEKDAYS = (1, 5)      # Tuesday, Saturday

# Storyline singles: key -> (file in singles/, title, caption body)
STORY = {
    "robots": ("robot_celebration.mp4", "The Robots Are Celebrating",
               "The Harvest Robots think they've won... 🤖🎉 Not for long."),
    "bell": ("bessie_bell_boogie.mp4", "Bessie's Bell Boogie",
             "When Bessie rings the bell, the whole farm dances 🔔🐄"),
    "slam": ("bessie_ground_slam.mp4", "Bessie's Ground Slam",
             "GROUND... SLAM! 💥🐄 Bessie shakes the whole farm."),
    "egg": ("cluck_egg_drop.mp4", "Cluck's Egg Drop",
            "Cluck drops an egg, and the robot never saw it coming 🥚🤖"),
    "dance": ("cluck_victory_dance.mp4", "Cluck's Victory Dance",
              "Every crop collected? Time for Cluck's victory dance 🐔🕺"),
    "cluckday": ("cluck_celebration.mp4", "Cluck's Big Day",
                 "We did it! Every crop's home safe 🌽🐔"),
}
STORY_TAGS = ["#farmfury", "#animation", "#cartoon", "#farmanimals", "#cute"]

# Cosmetics singles (Farm Fury: Arcade's Shop items, one short per Shop category): key -> (file, title, body)
COSMETICS = {
    "hats": ("cosmetics_hats.mp4", "Hats & Caps",
             "Cowboy hats, crowns and more 🤠👑 Which animal looks best?"),
    "trails": ("cosmetics_trails.mp4", "Trails",
               "Leave a rainbow ribbon... or a trail of bubbles 🌈🫧 Which trail would you pick?"),
    "machines": ("cosmetics_machines.mp4", "Machines",
                 "Tractor, milk tanker, hay baler 🚜 Ride through the maze in style!"),
}
COS_TAGS = ["#farmfury", "#farmfuryarcade", "#mobilegame", "#farmanimals", "#cute"]
# Afternoon of day N (0 = the first day) -> story single. With a Saturday start the episodes land on days
# 0,3,7,10,14,17,21,24,28; these sit next to the episode each one belongs with.
SINGLE_DAYS = {2: ("story", "robots"),      # Mon, after Ep 1: the robots gloating
               9: ("story", "bell"),        # Mon, before Ep 4 "Ring the bell"
               13: ("story", "slam"),       # Fri, before Ep 5's powers (not Thu: Vegetable Patch 03 is a ground slam too)
               16: ("story", "egg"),        # Mon, after Ep 5
               22: ("cos", "hats"),         # Sun 29 Nov: cosmetics singles start after the days already scheduled
               25: ("cos", "trails"),       # Wed 2 Dec
               26: ("story", "dance"),      # Thu, before the finale
               27: ("cos", "machines"),     # Fri 4 Dec
               29: ("story", "cluckday")}   # Sun, after Ep 9: "every crop's home safe"
# The Scout Reviews (user, 2026-11-15): a third, midday post slotted in between the existing posts, starting
# Mon 16 Nov, every 5 days, the last one the day before Chapter 2 starts (day 0 = Sat 7 Nov).
MIDDAY = {9: ("review", 1),      # Mon 16 Nov
          14: ("review", 2),     # Sat 21 Nov
          19: ("review", 3),     # Thu 26 Nov
          24: ("review", 4),     # Tue 1 Dec
          30: ("review", 5)}     # Mon 7 Dec
# Ep 10 (and with it all Stampede gameplay) starts the day after the last review: Tue 8 Dec = day 31.
STAMPEDE_FROM_DAY = 31

# Stampede batch made 2026-11-22 (user: add every new Stampede video to the sheets). key -> (file, kind, title, body)
# Ability shorts (narrated, real Frozen Tundra/Meadow Ruins gameplay) and true shorts (5-8 s gags, hook from frame 1)
# fill the afternoons without an episode from Wed 9 Dec, alternating; the Stampede Scout Reviews take the midday slot
# every 4 days from Thu 10 Dec.
ABILITY_DIR = VIDEO.parent / "FarmFury_Stampede" / "Ability_Shorts"
TRUE_DIR = VIDEO / "true_shorts"
ST_SINGLES = {
    "ab_cluck": (ABILITY_DIR / "ab_cluck.mp4", "Stampede ability short", "Cluck's Egg Launch",
                 "Eggs away! Right on the noggin! 🥚💨"),
    "ab_bessie": (ABILITY_DIR / "ab_bessie.mp4", "Stampede ability short", "Bessie's Ground Pound",
                  "Bessie breaks the floor... and finds a secret 🐄💥"),
    "ab_percy": (ABILITY_DIR / "ab_percy.mp4", "Stampede ability short", "Percy's Roll Dash",
                 "Out of my way, I'm on a roll! 🐷💨"),
    "ab_woolly": (ABILITY_DIR / "ab_woolly.mp4", "Stampede ability short", "Woolly's Cloud Step",
                  "Woolly makes her own cloud to stand on ☁️🐑"),
    "ab_ducky": (ABILITY_DIR / "ab_ducky.mp4", "Stampede ability short", "Ducky's Water Spout",
                 "Ducky's Water Spout knocks robots right over 🦆🌊"),
    "ab_horace": (ABILITY_DIR / "ab_horace.mp4", "Stampede ability short", "Horace's Horseshoe Throw",
                  "Horace's horseshoe reaches the robots up high 🐴"),
    "ab_gerald": (ABILITY_DIR / "ab_gerald.mp4", "Stampede ability short", "Gerald's Feather Blow",
                  "Feel the breeze, tin can! 🦃🌬️"),
    "ab_billy": (ABILITY_DIR / "ab_billy.mp4", "Stampede ability short", "Billy's Mud Sling",
                 "Mind the mud. Seriously. Mind it. 🐐"),
    "ts_pick_your_animal": (TRUE_DIR / "ts_pick_your_animal.mp4", "Stampede true short", "Who Would You Pick?",
                            "Eight animals, one run. Who would you pick? 👇"),
    "ts_secret_room": (TRUE_DIR / "ts_secret_room.mp4", "Stampede true short", "Secret Room!",
                       "Bessie found a secret room under the ice 🐄❄️"),
    "ts_three_stars": (TRUE_DIR / "ts_three_stars.mp4", "Stampede true short", "Three Stars!",
                       "Every crop, every coin, three stars ⭐⭐⭐"),
    "ts_boss_fight": (TRUE_DIR / "ts_boss_fight.mp4", "Stampede true short", "Boss Fight!",
                      "Three bonks and the Commander is done 🦆"),
    "ts_world_unlocked": (TRUE_DIR / "ts_world_unlocked.mp4", "Stampede true short", "New World Unlocked!",
                          "Next stop: the Frozen Tundra ❄️"),
}
ST_SINGLE_ORDER = ["ab_cluck", "ts_pick_your_animal", "ab_bessie", "ts_secret_room", "ab_percy", "ts_three_stars",
                   "ab_woolly", "ts_boss_fight", "ab_ducky", "ts_world_unlocked", "ab_horace", "ab_gerald", "ab_billy"]
ST_SINGLE_FROM_DAY = 32                  # Wed 9 Dec, the day after Ep 10
ST_REVIEW_DAYS = [33, 37, 41, 45, 49]    # Thu 10, Mon 14, Fri 18, Tue 22, Sat 26 Dec (midday)
ST_REVIEWS = {
    1: ("scout_review_stampede_01.mp4", "A Robot Reviews Farm Fury: Stampede",
        "The Scout is back, and this time Cluck and Bessie talk back 🤖🐔🐄"),
    2: ("scout_review_stampede_02.mp4", "A Robot Rates the Stampede Powers",
        "Eggs, wool clouds, mud... the Scout has complaints 🥚☁️🐐"),
    3: ("scout_review_stampede_03.mp4", "A Robot Tours the Six Lands",
        "Six lands, and the Scout wants every one of them back 🗺️🤖"),
    4: ("scout_review_stampede_04.mp4", "A Robot Defends the Bosses",
        "Three hits, and every boss wobbles. Don't tell anyone 🤫🤖"),
    5: ("scout_review_stampede_05.mp4", "The Verdict: Is Farm Fury: Stampede Worth It?",
        "The honest facts for grown-ups... and Cluck has the final word ⭐⭐⭐⭐⭐"),
}
ST_REVIEW_TAGS = ["#farmfury", "#farmfurystampede", "#gamereview", "#mobilegame", "#funny"]

# The Scout Reviews: the Scout "reviews" Farm Fury: Arcade, Bessie corrects him (scripts in
# Video/FarmFury_Arcade/Robot_Reviews/). n -> (file in Episodes/scout_reviews/, title, caption body)
REVIEWS_DIR = VIDEO / "scout_reviews"
REVIEWS = {
    1: ("scout_review_01.mp4", "A Robot Reviews Farm Fury: Arcade",
        "A Harvest Robot reviews our game. He hates it. Bessie has notes 🤖⭐"),
    2: ("scout_review_02.mp4", "A Robot Rates the Farm Fury Animals",
        "The Scout rates all eight animals... badly 🐔🐄🐷🐑🦆🐴🦃🐐"),
    3: ("scout_review_03.mp4", "A Robot Explores Every Farm Fury World",
        "Corn, carrots, apples... the Scout is NOT impressed by any of it 🌽🥕🍎"),
    4: ("scout_review_04.mp4", "A Robot Discovers Combos",
        "Two animals, one combo, one very confused robot 🔁💥"),
    5: ("scout_review_05.mp4", "The Verdict: Is Farm Fury: Arcade Worth It?",
        "The Scout's final verdict, and the honest facts for grown-ups 🤖✅"),
}
REVIEW_TAGS = ["#farmfury", "#farmfuryarcade", "#gamereview", "#mobilegame", "#funny"]

# Gameplay: world by world, numbered 01-10 each. Add hand-written captions as shorts get made; one without
# gets a plain caption from its file title.
# 2026-11-15: Arcade stops after Orchard (enough Arcade gameplay); Stampede follows, 5 shorts per world.
ARCADE_WORLDS = ["Corn Field", "Vegetable Patch", "Orchard"]
STAMPEDE_WORLDS = ["Meadow Ruins", "Frozen Tundra", "Watermill Village", "Sky Islands", "Sunken City",
                   "Robot Mothership"]
WORLD_ORDER = ARCADE_WORLDS + STAMPEDE_WORLDS
PER_WORLD = {**{w: 10 for w in ARCADE_WORLDS}, **{w: 5 for w in STAMPEDE_WORLDS}}
CAPTIONS = {
    ("Corn Field", 1): "Cluck leaves an egg behind, and the robot walks right into it 🥚",
    ("Corn Field", 2): "Bessie's Ground Slam powers down every robot nearby 💥🐄",
    ("Corn Field", 3): "Swap animals mid-maze! Percy rolls in 🐷",
    ("Corn Field", 4): "Three Woollys are better than one 🐑🐑🐑",
    ("Corn Field", 5): "Only Ducky can skip across the water 🦆💦",
    ("Corn Field", 6): "That was close! Percy dodges the robots 🐷🤖",
    ("Corn Field", 7): "Stay one step ahead of the robots 🐄🌽",
    ("Corn Field", 8): "Collect every crop to clear the maze 🌽",
    ("Corn Field", 9): "Can you outrun the Harvest Robots? 🤖💨",
    ("Corn Field", 10): "Ducky takes the water shortcut 🦆💦",
    ("Vegetable Patch", 1): "Horace throws a horseshoe, and the robot is out of the game 🐴✨",
    ("Vegetable Patch", 2): "Three Woollys in the veggie patch 🐑🐑🐑🥕",
    ("Vegetable Patch", 3): "Bessie's Ground Slam shakes the whole vegetable patch 💥🐄",
    ("Vegetable Patch", 4): "Meet Gerald! The newest animal on the team 🦃",
    ("Vegetable Patch", 5): "Bessie, then Percy: that's the Earthquake Roll combo 🐄➡️🐷💥",
    ("Vegetable Patch", 6): "Ducky waddles through the vegetable patch 🦆🥕",
    ("Vegetable Patch", 7): "Horace dodges the robots between the cabbages 🐴🤖",
    ("Vegetable Patch", 8): "Collect every carrot before the robots catch up 🥕",
    ("Vegetable Patch", 9): "Gerald stays one step ahead 🦃🤖",
    ("Vegetable Patch", 10): "Percy on the move in the vegetable patch 🐷🥬",
    ("Orchard", 1): "Meet Billy! He flings mud at the robots 🐐🟤",
    ("Orchard", 2): "Three Woollys in the orchard 🐑🐑🐑🍎",
    ("Orchard", 3): "Ducky's Water Spout sends the robots packing 🦆💦",
    ("Orchard", 4): "Horace's horseshoe flies through the apple trees 🐴✨",
    ("Orchard", 5): "Cluck drops an egg, and the robot walks right into it 🥚🍎",
    ("Orchard", 6): "Percy rolls through the orchard 🐷💨",
    ("Orchard", 7): "Gerald's Feather Burst! 🦃🪶",
    ("Orchard", 8): "Bessie's Ground Slam shakes the apple trees 💥🐄",
    ("Orchard", 9): "Can Horace outrun the robots? 🐴🤖",
    ("Orchard", 10): "Collect every apple to clear the maze 🍎",
    ("Meadow Ruins", 1): "Cluck finds a secret room... and a crystal apple! Percy joins the stampede 🐔🍎🐷",
    ("Meadow Ruins", 2): "Bessie pounds through the floor into a secret room. Hello, Woolly! 🐄💥🐑",
    ("Meadow Ruins", 3): "Cluck races to the flag for three stars ⭐⭐⭐🐔",
    ("Meadow Ruins", 4): "Pick your animal before every run! Woolly's turn 🐑",
    ("Meadow Ruins", 5): "Woolly jumps the robots all the way to the finish 🐑🤖🏁",
    ("Frozen Tundra", 1): "Cluck slides across the ice, eggs at the ready 🐔❄️🥚",
    ("Frozen Tundra", 2): "Bessie's Ground Pound shakes the whole tundra 🐄❄️💥",
    ("Frozen Tundra", 3): "A race across the ice to the flag 🏁❄️",
    ("Frozen Tundra", 4): "Horace lobs horseshoes all the way to the finish 🐴❄️",
    ("Frozen Tundra", 5): "Gerald blows the robots back through the snow 🦃🌬️❄️",
}
GP_TAGS = ["#farmfury", "#mobilegame", "#arcadegame", "#mazegame", "#retrogaming"]
ST_TAGS = ["#farmfury", "#farmfurystampede", "#mobilegame", "#platformer", "#farmanimals"]


def store_links(tags):
    return (f"App Store: {APP_STORE_LINK}\nGoogle Play: {YOUTUBE_SHORTS_LINK}\n\n"
            f"{' '.join(['#shorts'] + tags)}")


def episode(n):
    file, title, body = EPISODES[n]
    note = ("TikTok: AI-generated label on, add to the playlist" + (", PIN this one" if n == 1 else "")
            + f". YouTube: add to the \"{SERIES}\" playlist"
            + (f", set Related video to Ep {n - 1}" if n > 1 else "") + ".")
    return {"kind": f"Episode {n}", "video": VIDEO / file,
            "caption": f"EP {n} | {title}\n{body}\n\n{CTA}\n\n{' '.join(EP_TAGS)}",
            "yt_title": f"Ep {n}: {title} | {SERIES}",
            "yt_desc": f"{body}\n\nEpisode {n} of {SERIES}.\n\nPlay Farm Fury free:\n{store_links(EP_TAGS)}",
            "note": note, "short": f"Ep {n}: {title}"}


def story(key):
    file, title, body = STORY[key]
    return {"kind": "Story single", "video": SINGLES / file,
            "caption": f"{body}\n\n{CTA}\n\n{' '.join(STORY_TAGS)}",
            "yt_title": f"{title} | {SERIES}",
            "yt_desc": f"{body}\n\nFrom {SERIES}.\n\nPlay Farm Fury free:\n{store_links(STORY_TAGS)}",
            "note": "TikTok: AI-generated label on.", "short": f"{title} (story single)"}


def review(n):
    file, title, body = REVIEWS[n]
    video = REVIEWS_DIR / file
    if not video.exists():
        return None
    return {"kind": f"The Scout Reviews {n}", "video": video,
            "caption": f"THE SCOUT REVIEWS {n}/5 | {title}\n{body}\n\n{ARCADE_CTA}\n\n{' '.join(REVIEW_TAGS)}",
            "yt_title": f"{title} | The Scout Reviews",
            "yt_desc": (f"{body}\n\nThe Scout Reviews, part {n} of 5. A Farm Fury team production.\n\nPlay free:\n"
                        f"{store_links(REVIEW_TAGS)}"),
            "note": "TikTok: AI-generated label on. YouTube: add to a \"The Scout Reviews\" playlist.",
            "short": f"Scout Reviews {n}: {title}"}


def stampede_single(key):
    file, kind, title, body = ST_SINGLES[key]
    if not file.exists():
        return None
    return {"kind": kind, "video": file,
            "caption": f"{body}\n\n{STAMPEDE_CTA}\n\n{' '.join(ST_TAGS)}",
            "yt_title": f"{title} | Farm Fury: Stampede",
            "yt_desc": f"{body}\n\nReal gameplay from Farm Fury: Stampede.\n\nPlay free:\n{store_links(ST_TAGS)}",
            "note": "Real gameplay: no AI label.", "short": f"{title} (Stampede)"}


def stampede_review(n):
    file, title, body = ST_REVIEWS[n]
    video = REVIEWS_DIR / file
    if not video.exists():
        return None
    return {"kind": f"The Scout Reviews: Stampede {n}", "video": video,
            "caption": (f"THE SCOUT REVIEWS: STAMPEDE {n}/5 | {title}\n{body}\n\n{STAMPEDE_CTA}\n\n"
                        f"{' '.join(ST_REVIEW_TAGS)}"),
            "yt_title": f"{title} | The Scout Reviews",
            "yt_desc": (f"{body}\n\nThe Scout Reviews: Stampede, part {n} of 5. A Farm Fury team production.\n\n"
                        f"Play free:\n{store_links(ST_REVIEW_TAGS)}"),
            "note": "TikTok: AI-generated label on. YouTube: add to the \"The Scout Reviews\" playlist.",
            "short": f"Scout Reviews Stampede {n}: {title}"}


def cosmetic(key):
    file, title, body = COSMETICS[key]
    return {"kind": "Cosmetics single", "video": SINGLES / file,
            "caption": f"{body}\n\n{ARCADE_CTA}\n\n{' '.join(COS_TAGS)}",
            "yt_title": f"{title} | Farm Fury: Arcade Shop",
            "yt_desc": f"{body}\n\nFrom the Shop in Farm Fury: Arcade.\n\nPlay free:\n{store_links(COS_TAGS)}",
            "note": "TikTok: AI-generated label on.", "short": f"{title} (cosmetics)"}


def gameplay(world, n):
    folder = SINGLES / world
    found = sorted(folder.glob(f"{world} {n:02d} - *.mp4")) if folder.exists() else []
    if not found:
        return None
    video = found[0]
    title = video.stem.split(" - ", 1)[1]
    body = CAPTIONS.get((world, n), f"{title}! 🌽🤖")
    game, cta, tags = (("Farm Fury: Stampede", STAMPEDE_CTA, ST_TAGS) if world in STAMPEDE_WORLDS
                       else ("Farm Fury: Arcade", ARCADE_CTA, GP_TAGS))
    return {"kind": f"Gameplay - {world} {n:02d}", "video": video,
            "caption": f"{body}\n\n{cta}\n\n{' '.join(tags)}",
            "yt_title": f"{title} | {game}",
            "yt_desc": (f"{body}\n\nReal gameplay from {game}, {world}.\n\nPlay free:\n"
                        f"{store_links(tags)}"),
            "note": "", "short": f"{world} {n:02d} {title}"}


def resolve(slot):
    """slot: ("ep", n) | ("story", key) | ("gp", world, n). None = gameplay not rendered yet."""
    if slot[0] == "ep":
        return episode(slot[1])
    if slot[0] == "story":
        return story(slot[1])
    if slot[0] == "cos":
        return cosmetic(slot[1])
    if slot[0] == "review":
        return review(slot[1])
    if slot[0] == "st":
        return stampede_single(slot[1])
    if slot[0] == "streview":
        return stampede_review(slot[1])
    return gameplay(slot[1], slot[2])


def label(slot):
    if slot[0] == "review":
        return f"The Scout Reviews {slot[1]}: {REVIEWS[slot[1]][1]}"
    if slot[0] == "streview":
        return f"The Scout Reviews: Stampede {slot[1]}: {ST_REVIEWS[slot[1]][1]}"
    if slot[0] == "st":
        return f"{ST_SINGLES[slot[1]][2]} (Stampede)"
    return f"{slot[1]} {slot[2]:02d} gameplay short"


def build_days(start):
    queue = [("gp", w, n) for w in WORLD_ORDER for n in range(1, PER_WORLD[w] + 1)]
    days, ep, d = [], 1, 0
    st_singles = [("st", k) for k in ST_SINGLE_ORDER]
    midday = dict(MIDDAY)
    midday.update({day_n: ("streview", n) for n, day_n in enumerate(ST_REVIEW_DAYS, 1)})
    # Runs until every episode, single and review is out, then on while rendered gameplay shorts are left.
    while (ep <= len(EPISODES) or any(k >= d for k in SINGLE_DAYS) or any(k >= d for k in midday) or st_singles
           or (queue and resolve(queue[0]))):
        day = start + dt.timedelta(days=d)
        afternoon = None
        ep_ready = ep <= len(EPISODES) and (ep < 10 or d >= STAMPEDE_FROM_DAY)
        if day.weekday() in EPISODE_WEEKDAYS and ep_ready:
            afternoon, ep = ("ep", ep), ep + 1
        elif d in SINGLE_DAYS:
            afternoon = SINGLE_DAYS[d]
        elif d >= ST_SINGLE_FROM_DAY and st_singles and day.weekday() not in EPISODE_WEEKDAYS:
            afternoon = st_singles.pop(0)
        # Stampede gameplay only from Ep 10's day on; mornings before that stay empty once Arcade runs out.
        morning = None
        if queue and (queue[0][1] not in STAMPEDE_WORLDS or d >= STAMPEDE_FROM_DAY):
            morning = queue.pop(0)
        days.append((day, morning, midday.get(d), afternoon))
        d += 1
    return days, queue


def post_html(i, time, slot):
    p = resolve(slot)
    if p is None:
        return (f'<div class="post todo"><div class="meta"><b>{time}</b> &middot; TO MAKE: '
                f'{html.escape(label(slot))}</div></div>')
    for text in (p["caption"], p["yt_title"], p["yt_desc"]):
        check_wording(text.replace("#", " "))
    missing = "" if p["video"].exists() else ' <b style="color:#c00">FILE MISSING</b>'
    note = f'<div class="note">{html.escape(p["note"])}</div>' if p["note"] else ""
    cls = "post ep" if p["kind"].startswith("Episode") else "post"
    return f"""
<div class="{cls}">
  <div class="meta"><b>{time}</b> &middot; {html.escape(p['kind'])}{missing}</div>
  <div class="file"><a href="file:///{html.escape(str(p['video']).replace(chr(92), '/'))}">{html.escape(str(p['video']))}</a></div>
  {note}
  <div class="label">TikTok caption</div>
  <textarea id="c{i}" rows="6" readonly>{html.escape(p['caption'])}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('c{i}').value)">Copy caption</button>
  <div class="label">YouTube title ({len(p['yt_title'])}/100)</div>
  <input id="t{i}" readonly value="{html.escape(p['yt_title'], quote=True)}">
  <button onclick="navigator.clipboard.writeText(document.getElementById('t{i}').value)">Copy title</button>
  <div class="label">YouTube description</div>
  <textarea id="d{i}" rows="6" readonly>{html.escape(p['yt_desc'])}</textarea>
  <button onclick="navigator.clipboard.writeText(document.getElementById('d{i}').value)">Copy description</button>
</div>"""


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--start", default="2026-11-07")
    p.add_argument("--morning", default="07:30")
    p.add_argument("--midday", default="12:00")
    p.add_argument("--afternoon", default="16:30")
    args = p.parse_args()
    start = dt.date.fromisoformat(args.start)
    days, left = build_days(start)

    rows, overview, first_todo, i = [], [], {}, 0
    for n, (day, morning, midday, afternoon) in enumerate(days, 1):
        kind = "Episode day" if afternoon and afternoon[0] == "ep" else "Gameplay day"
        rows.append(f"<h2>Day {n} &middot; {day.strftime('%a %d %b')} &middot; {kind}</h2>")
        cells = []
        for time, slot in ((args.morning, morning), (args.midday, midday), (args.afternoon, afternoon)):
            if slot is None:
                cells.append("")
                continue
            rows.append(post_html(i, time, slot))
            i += 1
            r = resolve(slot)
            if r is None:
                if slot[0] == "gp":
                    first_todo.setdefault(slot[1], day)
                cells.append(f"<i>to make: {html.escape(label(slot))}</i>")
            else:
                text = html.escape(r["short"])
                cells.append(f"<b>{text}</b>" if r["kind"].startswith("Episode") else text)
        overview.append(f"<tr><td>{n}</td><td>{day.strftime('%a %d %b')}</td>"
                        + "".join(f"<td>{c}</td>" for c in cells) + "</tr>")
    todo_count = sum(1 for _, m, md, a in days for x in (m, md, a) if x and resolve(x) is None)
    need = "".join(f"<li><b>{html.escape(w)}</b> ({PER_WORLD[w]} shorts): first one posts {d.strftime('%a %d %b')}, so have "
                   f"them rendered by {(d - dt.timedelta(days=3)).strftime('%a %d %b')}</li>"
                   for w, d in first_todo.items())
    after = sorted({s[1] for s in left}, key=WORLD_ORDER.index)
    window = start + dt.timedelta(days=9)

    PLANS.mkdir(exist_ok=True)
    out = PLANS / f"launch-{start.isoformat()}.html"
    out.write_text(f"""<!doctype html><meta charset="utf-8"><title>Farm Fury launch schedule</title>
<style>{SHEET_STYLE}
table{{border-collapse:collapse;width:100%;font-size:14px}} td,th{{border:1px solid #ddd;padding:4px 6px;text-align:left}}
.ep{{border-color:#e6b800;background:#fffbea}} .todo{{background:#f4f4f4;color:#777}} .note{{font-size:13px;color:#555}}
</style>
<h1>Farm Fury launch schedule ({TIKTOK_HANDLE} + YouTube Shorts)</h1>
<p>{len(days)} days, {start.strftime('%a %d %b')} to {days[-1][0].strftime('%a %d %b')}. Every morning
{args.morning}: the next gameplay short, world by world in number order. Tuesday and Saturday {args.afternoon}:
the next episode. Other afternoons: a story or cosmetics single. Same file, same time on TikTok and
YouTube.</p>
<p><b>Recordings needed:</b></p><ul>{need or "<li>none</li>"}</ul>
<p><b>After this sheet:</b> the mornings carry on through {html.escape(', '.join(after))} ({len(left)} more
shorts, one a day); Tuesday and Saturday afternoons are free for Chapter 2 (Stampede) once it's made.</p>
<p><b>TikTok Studio</b> schedules at most 10 days ahead: schedule Days 1-10 now, then top up every few days
(next on or after {window.strftime('%a %d %b')}). Every post: <i>Disclose post content &gt; Your brand</i> on.
Episodes and story singles: also the <i>AI-generated content</i> label. Make a "{SERIES}" playlist.</p>
{YOUTUBE_STEPS}
<table><tr><th>Day</th><th>Date</th><th>{args.morning}</th><th>{args.midday}</th><th>{args.afternoon}</th></tr>{''.join(overview)}</table>
{''.join(rows)}
""", encoding="utf-8")
    print(f"{out}  ({len(days)} days to {days[-1][0]}, {todo_count} posts still to make)")


if __name__ == "__main__":
    main()
