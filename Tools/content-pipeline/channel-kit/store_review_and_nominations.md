# Store pages review + Apple featuring nominations (2026-11-25)

Checked as a US visitor (Google Play `gl=US`, App Store `/us/`). All three games are live in 177 countries.
Downloads shown on Google Play: Arcade 5+, Stampede 1+, Cannonfire 0+. No ratings yet on any of them.

## Status (2026-11-25)

- DONE, in Google review: Arcade short + full description (section 3 text, paragraphs unwrapped); Stampede renamed
  "FarmFury Stampede" -> "Farm Fury Stampede" (matches "Farm Fury Arcade" / "Farm Fury Cannonfire" rather than adding a colon).
- DONE, live: Arcade App Store promotional text = "Eight farm animals, eight powers, one big robot problem. Dodge the
  Harvest Robots and bring every crop home!" (the trading-cards line was dropped: the cards are social posts, not in the game).
- TO DO with the next iOS version (fields locked until a version is being prepared): Arcade subtitle
  "Farm animals vs robots maze", subcategory (currently none; Arcade / Action fit), description line about
  "puff up huge, or charge headfirst" -> "fire a burst of feathers, or fling sticky mud".
- DONE: Apple featuring nominations SUBMITTED 2026-11-25 for all three (iPhone only, publish date 25 Nov,
  links farmfurygames.com + youtube.com/@FarmFuryGames, same 'helpful details' text): Cannonfire and Stampede as
  App Launch, Arcade as App Enhancements (1.0.1's new powers). Apple only replies if a game is picked.
- Checked, no change needed: Stampede/Cannonfire Google Play descriptions; Stampede/Cannonfire App Store promo text.
- TO DO with each game's next iOS version: Cannonfire has NO subtitle either; Stampede's App Store description has two
  cut-off sentences ("...and unlock the" and "leave colorful  farm machines") - rewrite those.
- Data safety (Arcade + Stampede): Cannonfire's answers imported via Play Console CSV export/import (user OK'd),
  SAVED but NOT SENT for review: the user must press Publishing overview > "Submit 1 change for review" on each app
  (Claude's attempt was blocked by the permission system). One Cannonfire answer could not be copied: "complies with
  the Families policy" is not allowed on Arcade or Stampede, which usually means their Target audience does not
  include children. Check App content > Target audience and content on both.

## 1. Fix first: Google Play Data safety (Arcade and Stampede)

Both say **"No data collected"** and **"No data shared with third parties"**. Both games show ads (LevelPlay/AdMob)
and Arcade sends Unity Analytics events, so that is almost certainly wrong. Cannonfire's form is filled in properly
("may share Location, Financial info and 2 others", "Committed to follow the Play Families Policy"), so use it as the
template. An inaccurate Data safety form in a Families-listed game can get an app pulled, so this comes before any
marketing. Play Console > App content > Data safety. Check each ad/analytics SDK's own disclosure page.

## 2. Store page issues

| Game | Store | Issue | Fix |
|---|---|---|---|
| Arcade | Play | Description is out of date and partly garbled: Gerald "puffs up", Billy "charges ahead with a headbutt" (both changed), "knocks robots" (sentence cut), "where others can'", "unlock as in, Golden Sunset" (Frozen Garden missing), "purchases,including" | Replace with the text in section 3 |
| Arcade | App Store | **No subtitle** (Stampede has "Stomp robots, save the farm"). Subtitle text is searchable and is the second line people read | Subtitle: `Farm animals vs robots maze` (27/30) |
| Arcade | App Store | Description still lists "puff up huge, or charge headfirst" | Swap that line for the one in section 3 |
| Arcade | Both | Screenshot 1 is the title screen, 2 is the world menu. Most people only see the first two or three | Lead with gameplay: a busy maze with robots close, then a power in action, then the 8 animals |
| Arcade | Both | Screenshots have no captions | Add a short caption bar to each: "Dodge the robots", "8 animals, 8 powers", "Swap for combos", "100+ mazes" |
| Stampede | Play | Title is "FarmFury Stampede" (no space) | `Farm Fury: Stampede` to match the other games and the search term "farm fury" |
| All | Both | Zero ratings | Add the in-game review prompt (Play In-App Review API / Apple SKStoreReviewController) after a 3-star level. Never offer rewards for reviews |
| All | App Store | Promotional text unused (170 chars, can change any time without review) | `New: Farm Fury trading cards are out! Collect all 8 animals and their signature powers. Free on iPhone.` |

## 3. Replacement description: Farm Fury: Arcade (Google Play, also the App Store FEATURES list)

Short description (Play, 80 max):
`Maze action! Collect crops, dodge Harvest Robots, swap 8 farm animal heroes.`

Full description:

```
The Harvest Robots have taken the farm's crops, and the animals want them back!

Farm Fury: Arcade is a fast maze game. Guide brave farm animals through moonlit mazes, collect every crop before
time runs out, and stay one step ahead of the Harvest Robots. Grab a power crop and, for a few seconds, the robots
run scared.

EIGHT ANIMAL HEROES, EIGHT POWERS
• Cluck drops an egg the robots walk right into
• Bessie slams the ground and powers down robots nearby
• Percy curls up and rolls straight through them
• Woolly splits into helpful clones
• Ducky skips across water where nobody else can go
• Horace throws a horseshoe down the maze
• Gerald fires a burst of feathers
• Billy flings mud that leaves a sticky trap
Swap animals mid-maze and set off combos when the right pair team up.

LOTS TO PLAY
• 100 mazes across four free worlds: Corn Field, Veg Patch, Orchard and Wheat Field
• Three bonus worlds to unlock: Frozen Garden, Golden Sunset and Harvest Moon
• A new Daily Challenge every day
• Earn up to 3 stars on every level and unlock new animals as you go
• Dress up your animals with hats, trails and farm machines

SIMPLE CONTROLS
On-screen D-pad or swipe. Tap the power button when it lights up.

MADE FOR FAMILIES
No account, no chat. Purchases sit behind a parental gate.

Farm Fury: Arcade is free to play. It contains ads and optional in-app purchases (coin packs, cosmetics, bonus
worlds and Remove Ads). Watching a rewarded ad is always optional.

Part of the Farm Fury family of games. More at farmfurygames.com
```

## 4. Apple featuring nominations

Where: App Store Connect > Apps > (the app) > Featuring > Nominations > Create. One per game. Field names below
follow Apple's form as of 2026; adjust to the labels you actually see. Submit at least 2 weeks ahead of any date
you name (Apple asks for up to 3 months' notice for launches; for apps already live, use "App Enhancements" or
"New Content").

### Farm Fury: Arcade
- **Type:** New Content (or App Enhancements)
- **Name:** Farm Fury: Arcade, eight farm animal heroes vs the Harvest Robots
- **Description:**
  A colourful maze game from a small independent studio in South Africa. Players guide eight farm animals through moonlit
  mazes to win back the crops the Harvest Robots took. Every animal has its own power (egg drops, ground slams,
  rolling, wool clones, water skips, horseshoes, feathers and mud), and swapping animals in the right order sets
  off combos. 100 free mazes across four worlds, a Daily Challenge, three bonus worlds, and cosmetics such as hats,
  trails and farm machines. Built for families: no account, no chat, a parental gate on purchases and
  child-directed ads.
- **Why it's a good fit:** A friendly, non-violent take on the classic maze-chase genre with a story told through
  an animated series ("Farm Fury: The Story", on YouTube and TikTok), and part of a three-game Farm Fury family
  (Arcade, Stampede, Cannonfire) that shares the same animals and story.
- **Countries:** All (worldwide). **Supporting link:** https://www.farmfurygames.com

### Farm Fury: Stampede
- **Type:** New Content (or App Enhancements)
- **Name:** Farm Fury: Stampede, a farm-animal platformer across six lands
- **Description:**
  A side-scrolling platformer where eight farm animals march across six lands to the Robot Mothership. Swap animals
  at any time, each with its own power (egg launches, ground pounds, roll dashes, wool clouds, water spouts,
  horseshoes, feather blasts and mud traps), find secret rooms only one animal can reach, and beat a giant robot
  boss at the end of every world. Every level can be finished with any animal. Six story worlds are free.
- **Why it's a good fit:** Chapter 2 of the Farm Fury story, accessible controls with checkpoints and three lives,
  and a cast that carries over from Farm Fury: Arcade.
- **Countries:** All. **Supporting link:** https://www.farmfurygames.com

### Farm Fury: Cannonfire
- **Type:** App Launch (if within Apple's window) or New Content
- **Name:** Farm Fury: Cannonfire, launch farm animals at wobbly robot towers
- **Description:**
  A physics game where players pick three farm animals, drag back to aim the cannon and topple wobbly robot
  fortresses. Tap mid-flight for each animal's power (cluster eggs, wool balls, water swirls, feather bombs and
  more). 160 levels across nine worlds, a boss for every world, and cannon skins and trails to collect. No account
  needed; purchases and links out are behind a parental gate.
- **Why it's a good fit:** Chapter 3 of the Farm Fury story: the same eight animals, now with their biggest powers
  yet, in a family-friendly physics game.
- **Countries:** All. **Supporting link:** https://www.farmfurygames.com

Before submitting: confirm each game's facts above against the current build (level counts, prices, which worlds
are free).
