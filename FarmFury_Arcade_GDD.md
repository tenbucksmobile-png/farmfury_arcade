# FARM FURY: ARCADE — Game Design Document

**v2.4** · "Cluck. Chase. Collect. Chaos." · converted to Markdown from `FarmFury_Arcade_GDD_v2.docx` for in-repo use with Claude Code

Status: Pre-Launch — content, monetisation, and core systems complete; ad/IAP platform registration and analytics instrumentation in progress.

This document supersedes v1.0 (2026-07-26) and describes Farm Fury: Arcade only. The original Farm Fury project (physics-destruction, launcher-based) is a separate, currently shelved Unity project and is out of scope for this document — see Section 1.

> **Security note:** the source .docx this file was converted from had a live-looking GitHub personal access token pasted into its final page. It has been deliberately left out of this Markdown version. If that token hasn't been revoked yet, do that before anything else — see github.com/settings/tokens.

---

## 1. Executive Summary

**Game Title:** Farm Fury: Arcade
**Tagline:** "Cluck. Chase. Collect. Chaos."

**Concept:** A maze-based arcade action game, set in the Farm Fury universe. Players control farm animals navigating farm-themed mazes, collecting crops while being chased by (or chasing) Harvest Robots. The classic dot-collection / enemy-avoidance / power-pellet loop is reimagined through Farm Fury's cartoon aesthetic, an 8-character roster with unique active abilities, and a character-swap combo system unique to this game.

**Project Lineage:** Farm Fury: Arcade and the original Farm Fury (physics-destruction game — six worlds, six launcher mechanics) are separate Unity projects in separate repositories, sharing only the Farm Fury brand, character cast, and developer. The original project's repository was last updated 2026-08-09 and has no monetisation or backend implementation. Farm Fury: Arcade has been under continuous active development since and is the studio's near-launch title.

**Platform:** iOS and Android (primary). App identifier: `com.farmfury.arcade` on both platforms, registered under the Tenbucks_Mobile developer account. A WebGL build is a stated possibility but has not been started.

**Target Audience:**
- Primary: Casual mobile gamers aged 8–45 who enjoy retro-inspired arcade games
- Secondary: Existing Farm Fury players (cross-promotion, contingent on the wider franchise's active titles — see Section 14)
- Tertiary: Nostalgic players seeking modern takes on classic arcade formulas

This range is carried over from the original design pass and is not yet validated against real player data — see Section 15 for why this matters before committing UA spend.

Apple's Kids Category (Guideline 1.3) was considered and deliberately not pursued for this launch: it prohibits third-party advertising and third-party analytics outright, which is incompatible with the ad-mediation revenue this game's entire business model depends on (Section 11). General-audience listing is the only category compatible with that model as designed.

**Genre:** Arcade / Maze / Action / Casual

**Business Model:** Free-to-play. Revenue comes from rewarded and interstitial ads, a Remove Ads purchase, coin packs, cosmetic purchases (hats, trails, and character-vehicle Machine skins), and outright purchase of three additional worlds. No cosmetic or purchase affects core gameplay difficulty or ability power.

**Development Stack:** Unity 6 (6000.5.0f1), C#, with Claude Code as the primary engineering collaborator and Kling AI as the primary art-generation tool.

**Unique Selling Points:**
- Farm Fury brand recognition and character roster
- 8 playable characters, each with a unique active ability (unlike a single interchangeable Pac-Man avatar)
- A character-swap combo system — 8 discoverable combos reward using specific characters in sequence within one maze
- Forgiving-but-real stakes: 3 free respawns per maze attempt, then a coin- or ad-gated revive keeps a run alive rather than ending it outright (see Section 5)
- Content-driven progression across 7 worlds and 175 hand-verified mazes, with 3 of those worlds sold as direct, single-purchase content unlocks rather than a subscription or grind gate

---

## 2. Game Overview

**Core Gameplay Loop:** Main Menu → Level Select (choose a world, choose an unlocked maze) → Gameplay (navigate the maze, collect crops, avoid or confront Harvest Robots, use power pellets to turn the tables, survive the level timer) → Level Complete or Level Failed → back to Level Select → repeat with the next maze.

**Session Length:** Figures from the original design pass are retained as planning assumptions, but are unverified — no analytics pipeline exists yet to measure real sessions (see Sections 12–13).

| Metric | Planning assumption |
|---|---|
| Average session | 5–10 minutes |
| Individual maze | 60–90 seconds (bounded by a hard 120-second level timer) |
| Session structure | 5–8 mazes per typical play session |

**Player Fantasy:** The player IS the farm animal fighting back against the robot invasion — sneaking through the farm at night, gathering the crops the robots are trying to steal, and occasionally turning the tables to chase down the metal invaders.

---

## 3. The Farm Fury Arcade Universe

**Narrative Context:** The Harvest Robots have invaded the farm not just to destroy structures but to steal the harvest. Each maze represents a different section of the farm at night. Four fields are always available from the start — Corn Field, Vegetable Patch, Orchard, and Wheat Field. Three further farms — Frostbite Garden, Golden Sunset, and Harvest Moon — can each be unlocked with a one-time purchase (see Sections 6 and 11).

**Setting Atmosphere:**

| Aspect | Current state |
|---|---|
| Time of day | Night, warm moonlight (per-world backdrop art) |
| Mood | Playful stealth mission |
| Colour palette | Varies per world — see Section 8 for the maintained hex palette |
| Music | Each of the 4 free worlds now has its own dedicated background track (plus a separate Main Menu/Level Select theme), crossfading to a second track while a power pellet is active; the 3 purchase-gated worlds don't have dedicated tracks yet and fall back to Corn Field's. |

---

## 4. Characters

**Playable Roster (8 characters).** All eight abilities were reimagined for maze gameplay and have been through several balance passes since the original design. Movement speed is unified at the same value for all 8 characters. Every ability cooldown is unified to 10 seconds. Unlock thresholds (mazes completed) were retained from the original design and are confirmed accurate against the shipped build.

Two abilities were substantially reworked after the original design: Percy's was originally a one-time wall-phase and is now a forward roll; Billy's was originally permanent wall destruction and is now a forward charge. Neither character destroys or phases through walls anymore outside of the Iron Stampede combo (see Section 5).

| Character | Ability | Effect | Cooldown | Unlock |
|---|---|---|---|---|
| Cluck the Chicken | Egg Drop | Drops a single egg at her current position; any robot that walks over it is instantly defeated. | 10s | Starter character |
| Bessie the Cow | Ground Slam | Instant shockwave defeats every robot within 2 tiles at cast; the shockwave zone then lingers 3 seconds, defeating any robot that wanders in afterward. | 10s | Starter character |
| Percy the Pig | Bounce Roll | Rolls 3 tiles forward (9 if buffed by Earthquake Roll / Kick and Roll) in his current facing direction, instantly defeating any robot touched; stops early at a wall. | 10s | After 5 mazes completed |
| Woolly the Sheep | Triple Clone | Spawns 2 AI-controlled clones that wander and collect crops for 10 seconds. | 10s | After 10 mazes completed |
| Ducky the Duck | Skip Shot | Teleports across an adjacent, not-yet-used water tile pair — usable once per pair per maze regardless of cooldown (the deeper limiter); the button re-arms on the shared 10s cooldown. | 10s | After 15 mazes completed |
| Horace the Horse | Horseshoe Throw | Throws a spinning horseshoe (or hay bale, if his Machine Cosmetics skin is equipped) 3 tiles forward in his current facing direction (6 if buffed by Crossfire), instantly defeating the first robot it touches; stops early at a wall. | 10s | After 20 mazes completed |
| Gerald the Turkey | Puff Up | Pulsates between normal size and 2x scale over 3 seconds (3 full pulse cycles); any robot touched during the pulse is instantly defeated. Moves at half speed and cannot use warp tunnels while pulsing. | 10s | After 30 mazes completed |
| Billy the Goat | Headbutt Through | Speeds up and charges 3 tiles forward in his current facing direction, instantly defeating any robot touched; stops early at a wall. | 10s | After 40 mazes completed |

**Enemy Roster — Harvest Robots.** Six robot types are defined in data; five are in active use. Heavy's dedicated art was removed from the project and it is excluded from the automatic level-spawn curve — its code and data still exist, but no shipped level currently spawns one.

All robots alternate between Chase and Scatter states on a fixed 20-second/5-second cycle. A power pellet flips every robot to Vulnerable, fleeing toward the maze's farthest real walkable point from the player. A robot defeated while Vulnerable disappears permanently for the remainder of that maze attempt; every robot resets fresh on the next level load.

| Robot | Behaviour | Speed | Threat |
|---|---|---|---|
| Harvester Robot (Red) | Direct pursuit — always heads toward the player's current position by shortest path. | Medium | Low (predictable path) |
| Scout Robot (Pink) | Predictive interception — targets 4 tiles ahead of the player's facing direction. | Medium-Fast | Medium |
| Patrol Robot (Cyan) | Flanking — positions itself opposite the Harvester relative to the player (falls back to direct pursuit if no Harvester is present). | Medium | Medium |
| Drifter Robot (Orange) | Distance-based — chases when the player is far away, retreats toward its own scatter corner when close. | Slow | Low-Medium |
| Drone Robot (Purple) | Ignores interior walls (still blocked by the maze's outer border) and moves in a straight line toward the player at reduced speed. | Slow (0.5x) | Very High |
| Heavy Robot (Grey) | Defined in data (direct pursuit, 2 hits to defeat, 0.7x speed) but not currently spawned in any shipped level; its art was removed from the project. | Very Slow | Not currently in play |

---

## 5. Gameplay Mechanics

**Movement System.** Hold-to-move, not auto-run. The character advances only while a direction is actively held (keyboard, on-screen D-pad, or a completed swipe); releasing the input stops the character immediately, wherever it is — including mid-tile. Switching direction, including a full 180-degree reversal, takes effect instantly, with no cooldown and no requirement to be at an intersection. This replaced an earlier queue-and-auto-run model (closer to classic Pac-Man) that read as unresponsive in practice. Movement is strictly 4-direction; diagonal movement is not possible.

**Crop Collection.** Two collectible tiers exist per world (not four — the original design's "special" and "golden" 200/500-point tiers were never built; see Section 14):

| Tier | Points |
|---|---|
| Corn kernel (or per-world equivalent) | 10 |
| Vegetable (or per-world equivalent) | 50 |

A maze also carries a per-world themed bonus pickup (Orchard: Cherry, Wheat: Grain Sack, 10 of each, awarding coins directly rather than maze score) and one universal coin pickup on every maze regardless of world. Corn Field and Vegetable Patch have no themed bonus of their own. Level completes when every required crop is collected.

**Power Pellets.** Exactly one power pellet spawns per maze (a hard cap, regardless of how many pellet spawn points a level's data defines). Its tier is randomly rolled per maze:

| Tier | Roll odds | Duration |
|---|---|---|
| Sunflower (standard) | 70% | 5 seconds |
| Golden Wheat (rare) | 20% | 9.5 seconds |
| Rainbow (very rare) | 10% | 17 seconds |

These are the raw roll odds, but since every maze has exactly one pellet and that sole pellet is always treated as "the special one," a Sunflower roll is silently promoted to Golden Wheat — Sunflower can never actually appear in a real maze. The effective in-game distribution a player experiences is Golden Wheat ~90%, Rainbow ~10%, Sunflower 0%.

While active: robots turn Vulnerable and flee; the player defeats robots for escalating points. Eating a second pellet while one is already active extends the remaining time to the new pellet's duration if longer — it never shortens an already-running window.

**Chain Scoring:**

| Event | Points |
|---|---|
| First robot eaten | 200 |
| Second robot eaten | 400 |
| Third robot eaten | 800 |
| Fourth robot eaten | 1,600 |
| All robots on one pellet activation | +5,000 bonus |

**Character Abilities & Swapping.** During any maze, the player can pause and swap to a different unlocked character. Swapping costs 1 coin (free if the player currently has zero coins). Strategic swapping enables using multiple abilities in one maze and triggers the combo system below.

**Combo System.** Certain character-swap sequences within one maze trigger a combo effect, consumed on the named ability's next activation (Full Fury fires immediately instead).

| Combo Name | Trigger | Effect |
|---|---|---|
| FEATHER STORM | Cluck → Woolly | Woolly's clones drop eggs as they walk |
| EARTHQUAKE ROLL | Bessie → Percy | Percy's next Bounce Roll travels 9 tiles instead of 3 |
| SKIP SHATTER | Ducky → Woolly | Ducky's next Skip Shot spawns 2 wool clones at the destination |
| DOUBLE SLAM | Bessie → Bessie (2nd+ activation) | Ground Slam radius doubles to 4 tiles |
| CROSSFIRE | Billy → Horace | Horace's next horseshoe (or hay bale) throw travels twice as far — 6 tiles instead of 3 |
| IRON STAMPEDE | Bessie → Gerald | Puff Up also destroys walls Gerald is adjacent to |
| KICK AND ROLL | Horace → Percy | Same buff as Earthquake Roll — 9-tile roll |
| FULL FURY | 5+ distinct characters used this maze | Immediate: every robot on the maze is stunned for 5 seconds |

**Respawn & Revive System.** A maze is not endless. The player has 3 free respawns per maze attempt. On the 4th death, the run does not end immediately — a Revive Prompt offers:
- Pay 5 coins to revive on the spot (disabled up front if the player can't afford it)
- Watch a rewarded ad (the `continue_after_death` placement) — the revive is only granted if the ad SDK confirms the reward actually fired, not merely that the ad was shown
- Decline — the run ends, identical to how a 4th death behaved before this feature existed

Separately, a 120-second level timer runs independently of the respawn count and ends the run directly on timeout — no revive is offered for a timeout, only for the respawn cap. The on-screen timer pulses a red warning colour in the final 15 seconds.

**Scoring System.** Crop and chain-kill points are tracked live. A maze's final result adds a capped time bonus and a capped perfect-run bonus on top of the running score, then assigns 1–3 stars against an estimated maximum-possible score for that level (1 star for completing at all, 2 stars at 75% of the estimate, 3 stars at 95%). This estimate is a deliberate approximation, since the real achievable chain-kill bonus depends on how many robots happen to be near the player when each pellet triggers. Coins earned per level: 10 + (stars × 5).

**Difficulty Progression.** Difficulty resets at the start of every 25-level world rather than climbing once across all 175 levels.

| Levels within a world | Robots |
|---|---|
| First 5 | 2 robots |
| Next 7 | 3 robots |
| Next 7 | 4 robots |
| Final 6 | 5 robots |

Robot type is drawn from a rotating roster of Harvester, Scout, Patrol, Drifter, and Drone (Heavy is excluded — see Section 4). This is a first-pass difficulty default, easily retuned per level. No procedural, unbounded-difficulty "Endless" mode exists — see Section 14.

---

## 6. Level Design

**Maze Structure.** Every maze in every world uses the same fixed 12×9 tile grid — not the original design's 28×31 "standard Pac-Man dimensions." Each maze has exactly one power pellet and one single-cell Robot Factory spawn point (not an open "ghost house" area). Player start position is read from the maze's own authored layout, not fixed to a universal bottom-centre coordinate. Warp tunnel tiles are paired by matching same-row tiles first, then falling back to same-column pairing for whatever remains.

**Farm-Themed Maze Worlds.** 175 levels total. Worlds 1–4 unlock sequentially by star progress on the previous world's final level; Worlds 5–7 unlock purely on purchase — there is no star requirement to enter a purchased world, though levels within it still gate on the previous level's stars.

| World | Level range | Access | Notes |
|---|---|---|---|
| 1. Corn Field | Levels 1–25 | Free | Golden corn stalks, rich brown soil, warm night sky. Full dedicated art. |
| 2. Vegetable Patch | Levels 26–50 | Free | Row markers, lattice fences, tilled earth. Full dedicated art. |
| 3. Orchard | Levels 51–75 | Free | Fruit tree trunks, fallen leaves, deep purple sky. Full dedicated art. |
| 4. Wheat Field | Levels 76–100 | Free (last free world) | Wheat sheaves, cracked dry earth. Full dedicated art. |
| 5. Frostbite Garden | Levels 101–125 | $3.99 purchase | Real dedicated wall/ground/backdrop/shield art; crop, vegetable, pellet, and warp-tunnel art currently reuse Corn Field's. Level Select badge art reads "Frozen Garden." |
| 6. Golden Sunset | Levels 126–150 | $3.99 purchase | Real dedicated wall/ground/backdrop/shield art; crop/vegetable/pellet/warp-tunnel art currently reuse Corn Field's. |
| 7. Harvest Moon | Levels 151–175 | $3.99 purchase | Real dedicated wall/ground/backdrop/shield art; crop/vegetable/pellet/warp-tunnel art currently reuse Corn Field's. |

**World Purchase.** Each of the three additional worlds is sold as a single $3.99 non-consumable purchase that unlocks the whole 25-level world outright — not a subscription, not a per-level purchase, and not a cosmetic reskin of an already-owned world (an earlier "Maze Theme" reskin concept was built and then fully removed in favour of this model). Reachable from the Shop hub's Worlds icon (moved off Settings in a later navigation reorg), and by tapping a locked-but-purchase-gated world's badge directly in Level Select.

**Special Level Types.** Bonus Rounds and Boss Levels were part of the original design and have not been built — see Section 14. No level in the shipped build differs structurally from a normal maze.

---

## 7. User Interface

**Gameplay HUD (Landscape).** Score and the countdown Timer are stacked in the top-left corner. A coin-balance chip sits top-right. Bottom-right, stacked bottom-to-top: a watch-ad skip-cooldown button, the active character's ability icon above it (pulses when off cooldown, with a coin-cost skip badge overlaid — enlarged and continuously spinning as of a later pass), the Swap Character button, and a Locker button (opens an in-maze "try what you've bought" cosmetics panel) topmost. Bottom-left: a 4-direction on-screen D-pad, with Pause positioned directly above it. No separate on-screen Level indicator. Character swapping opens directly from this HUD (the Swap Character button, or the Tab keyboard shortcut), not from the Pause menu.

**Main Menu.** Three buttons on the title art: Play (→ Level Select), Settings, and Exit (calls `Application.Quit()` — functional on Android, a documented no-op on iOS since Apple doesn't allow an app to self-terminate). Settings opens a Menu Hub overlay with two signs — SETTINGS and Shop.

**Settings.** Reached via Main Menu → Settings → Menu Hub's SETTINGS sign (or directly from Pause's own Settings button). A single row of 4 icon cells: Music mute toggle, Leaderboards, Character Story, and Policies (Legal screen with working Privacy Policy and Terms of Use links, both opening pages hosted at www.farmfurygames.com; both are still drafts pending legal review). Shop, Restore Purchases, and the Worlds entry all moved off this screen onto the Shop hub. Volume sliders, a language selector, and vibration/handedness toggles from the original design are still not present.

**Level Select.** Two states on one screen. World-select shows a horizontally flickable carousel: a Daily Challenge shield first, followed by all 7 world badges (locked free worlds dimmed but shown; locked purchase-gated worlds fully tappable, opening the World Purchase screen). Tapping the centred badge reveals that world's 4-column tile grid showing lock state and star rating. This replaced the original design's isometric world map with a connecting path (built at one point, later removed).

**Pause Menu.** Four icons — Play/Skip/Settings/Quit — the same layout Level Failed uses. Play resumes gameplay (the only way to un-pause). Skip returns to Level Select. Settings opens the Settings panel. Quit returns to Level Select's world-select state. Swap Character moved to the Gameplay HUD.

**Level Complete Screen.** Simplified from the original design: shows star rating and total score only (the crop/robot/time/perfect-bonus breakdown is still computed, just not displayed). A single Play button (bottom-left) advances to the next unlocked level's tile. Home and Settings buttons were removed. A Double Coins button (bottom-right, rewarded ad, doubles that completion's coin payout) took the vacated corner.

**Shop, Cosmetics, and World Purchase Screens.**
- Shop hub (from Settings' Menu Hub): 4 icons — Cash (→ Coin Purchase), Worlds (→ World Purchase), Ads (direct Remove Ads purchase, no sub-screen), Cosmetics (→ Cosmetics Hub)
- Coin Purchase screen: 4 coin-pack plaques + Restore Purchases (moved here from Settings)
- Cosmetics chooser: three entry icons — Hats, Trails, and Machines (Machine Cosmetics — full character-vehicle skins)
- Hat / Trail purchase screens: framed item icons, each with a flat $1.99 price plaque
- Machines purchase screen: framed item icons, each with a flat $3.99 price plaque, same shape as World Purchase
- World Purchase screen: the 3 world shields + a $3.99 price plaque

---

## 8. Visual Design

**Art Style.** Consistent with the Farm Fury brand — warm, 3D-rendered cartoon aesthetic, produced via Kling AI and wired into a 2D tile-based renderer.

**Character Sprites — Current Completeness.** Coverage varies by character. All 8 characters have a unique active ability and are fully playable; several still fall back to their front-facing sprite for one or more directions where dedicated art hasn't landed yet (most notably Up-facing art, which no character has dedicated frames for). No character currently has a distinct defeat or victory animation frame — level-end states are communicated through screen transitions and the character fading out.

**Colour Palette:**

| Colour | Hex Code | Usage |
|---|---|---|
| Primary Orange | #E85D04 | Titles, primary UI |
| Secondary Brown | #8B4513 | Wood elements |
| Accent Gold | #FFD700 | Rewards, celebrations |
| Wall Brown | #4A2C1A | Maze walls (placeholder-art baseline; real per-world wall art now covers this in most worlds) |
| Crop Yellow | #F5C842 | Standard crops |
| Power Sunflower | #FFC300 | Power pellets |
| Robot Red | #CC2200 | Enemy robots |
| Robot Blue | #4A9BD1 | Vulnerable robots (still colour-tint only — no dedicated Vulnerable-state sprite exists yet) |

**Known Placeholder Art:**
- Vulnerable robot state (colour tint only, no dedicated sprite)
- Loading screen background (uploaded but not wired into any screen)
- Several minor UI chrome elements without dedicated icon art

---

## 9. Audio Design

**Music.** Each of the 4 free worlds now has its own dedicated background track, plus a separate "Theme" track for Main Menu/Level Select. Music crossfades to a second track for the duration a power pellet is active, then crossfades back. The 3 purchase-gated worlds don't have their own dedicated tracks yet and fall back to Corn Field's.

**Sound Effects.**

| Event | Trigger |
|---|---|
| Animal death | Start of the player death sequence |
| Crop pickup | Every corn/vegetable crop collected (shared cue for both tiers) |
| Coin pickup | Every coin collected |
| Ability ready | The instant an ability's cooldown reaches zero |
| Rare pellet pickup | Collecting a Golden Wheat or Rainbow pellet (not the standard Sunflower tier) |
| Robot spawn | Every robot spawn, including level start |
| Robot defeated | The moment a robot is actually defeated, whichever kill method triggered it |
| Combo triggered | The instant a real combo's celebration callout appears |
| Per-character ability cast | A dedicated mechanical cue (not a voice bark) per activation for 7 of the 8 characters — Cluck, Bessie, Percy, Billy, Horace, Gerald, Ducky. Woolly has none yet. |

These are short mechanical/impact cues (egg crack, hoofbeat, etc.), not voice performances — per-character voice barks on ability use (Squawk, Moo, Oink, etc.) were part of the original design and are not built — see Section 14.

---

## 10. Progression Systems

**Character Unlocks** (verified directly against the current build's character-data source — unchanged from the original design):

| Character | Unlock requirement |
|---|---|
| Cluck | Available from start |
| Bessie | Available from start |
| Percy | Complete 5 mazes |
| Woolly | Complete 10 mazes |
| Ducky | Complete 15 mazes |
| Horace | Complete 20 mazes |
| Gerald | Complete 30 mazes |
| Billy | Complete 40 mazes |

**Coin Economy.** Coins earned: 10 + (stars × 5) per level completed. The original design's separate "1 per level / 5 per new best / 10 per boss / 25 per daily challenge" bonuses were never implemented as separate lines — superseded by this single formula.

| Coins spent on | Cost |
|---|---|
| Character swap mid-maze | 1 coin (free if balance is 0) |
| Revive on the 4th death | 5 coins |
| Skip current ability cooldown | 3 coins |

**Cosmetics System.** Three cosmetic categories exist: Hats, Trails, and Machines (a full character-vehicle skin, the `CosmeticType.Skin` data type's first real use). Maze Theme was built and then fully removed in favour of the World Purchase model. Character accessories (glasses, bows, bandanas) were never built as their own category. Hats and Trails are each a flat $1.99 real-money purchase; Machines are $3.99 — not the original design's coin-priced system for any of them.
- Hats (5): Baseball Cap and Cowboy Hat (each real per-character art, one purchase grants and auto-equips every character's own version, all 8 at once), Sombrero, Chef Hat, Crown (all three universal one-size sprites, no per-character art)
- Trails (6): Corn Husk, Ember, Sparkle Dust, Rainbow Ribbon, Confetti, Bubbles — the first 4 render via a procedural tinted trail effect or fading afterimages of the real icon art; Confetti/Bubbles added later
- Machines (3): Clucky's Tractor, Bessie's Milk Tanker, Horace's Hay Baler — each exclusive to the one character it was drawn for, purely cosmetic reskins of that character's existing ability hazard (same power, same cooldown — never pay-to-win)

**Daily Challenge.** A shield inserted as the first item in the Level Select world carousel, not a separate menu screen. It picks a real, already-existing level from whichever worlds the player currently has unlocked, using a date-based seed, and applies a 1.25x robot-speed multiplier. A character-restriction rule exists but is checked only after the run completes, not enforced live — a player can freely swap characters mid-challenge, and the run simply won't register as a valid Daily Challenge completion if more than one character was used.

---

## 11. Monetisation

**Free-to-Play Philosophy.** No energy walls, no pay-to-win, no loot boxes. Every purchase buys convenience, cosmetics, or additional content — never gameplay advantage or access to content that isn't also reachable for free (deliberate exception: the 3 purchase-gated worlds, sold outright as additional content).

**Ads.** All ad requests are treated as child-directed (COPPA) — set at the platform dashboard level and in the SDK before initialisation. Mediated via Unity's Ads Mediation (LevelPlay), mediating **AdMob and Unity Ads** (not AdMob alone). As of 2026-09-20, on Android LevelPlay init succeeds and all three ad units are recognised (two configuration errors were found and fixed: a wrong app key, and ad unit names supplied where the SDK needs the real Ad Unit IDs), but every ad request still returns `509 No fill` and no ad has been shown yet; the cause has not been determined. On iOS, LevelPlay init was failing (`Error 2080`); the app key and ad unit IDs in the build were found to be wrong and have been corrected in code, but iOS has not been rebuilt or retested, so it is unknown whether that resolves it. A parental arithmetic gate (`ParentalGateController`) is built and wired in front of every real-money purchase surface (Remove Ads, Coin Purchase, Cosmetic/World Purchase) — this closes the gap the previous version of this document flagged as open.
- Rewarded video — 3 placements: continue after death (revive), double coins on Level Complete, skip current ability cooldown. Reward only granted if the SDK confirms it actually fired.
- Interstitial — every 6 levels (configurable), skipped entirely if Remove Ads is owned. Gameplay is frozen (not merely covered) for its duration.

**In-App Purchases.** **22 real-money products** defined in code (up from an earlier count of 15 — 7 more were added since: Chef Hat, Crown, Confetti Trail, Bubbles Trail, and the 3-item Machine Cosmetics category). All 22 are registered in App Store Connect for iOS. Google Play Console product/store setup has not yet been done. Purchases have been tested and confirmed working end-to-end on a real device (iOS TestFlight); Android has not yet had a purchase test.

| Product | Price | Notes |
|---|---|---|
| Remove Ads | $4.99 | One-time, non-consumable. Includes 100 bonus coins. Rewarded ads remain available afterward — only the interstitial is removed. |
| Coin Pack — 100 coins | $0.99 | Consumable |
| Coin Pack — 500 coins | $3.99 | Consumable |
| Coin Pack — 5,000 coins | $9.99 | Consumable |
| Coin Pack — 15,000 coins | $19.99 | Consumable |
| Hat — Baseball Cap (all 8 characters) | $1.99 | Non-consumable, real per-character art |
| Hat — Cowboy Hat (all 8 characters) | $1.99 | Non-consumable, real per-character art |
| Hat — Sombrero | $1.99 | Non-consumable, universal sprite |
| Hat — Chef Hat | $1.99 | Non-consumable, universal sprite |
| Hat — Crown | $1.99 | Non-consumable, universal sprite |
| Trail — Corn Husk | $1.99 | Non-consumable |
| Trail — Ember | $1.99 | Non-consumable |
| Trail — Sparkle Dust | $1.99 | Non-consumable |
| Trail — Rainbow Ribbon | $1.99 | Non-consumable |
| Trail — Confetti | $1.99 | Non-consumable |
| Trail — Bubbles | $1.99 | Non-consumable |
| Machine — Clucky's Tractor | $3.99 | Non-consumable, Cluck-exclusive skin |
| Machine — Bessie's Milk Tanker | $3.99 | Non-consumable, Bessie-exclusive skin |
| Machine — Horace's Hay Baler | $3.99 | Non-consumable, Horace-exclusive skin |
| World — Frostbite Garden | $3.99 | Non-consumable, 25 levels |
| World — Golden Sunset | $3.99 | Non-consumable, 25 levels |
| World — Harvest Moon | $3.99 | Non-consumable, 25 levels |

The original design's coin-pack pricing included a 1,500-coin/$9.99 tier and priced the top two tiers at $19.99/$49.99; the shipped pricing dropped the 1,500 tier and repriced the top two tiers down.

**Not Built.** Season Pass and the original design's cross-promotion mechanic — see Section 14.

---

## 12. Technical Specifications

**Architecture.** Single-scene architecture — every level is data (ScriptableObjects), swapped in place rather than loaded via separate Unity scenes. All game data (levels, characters, robots, cosmetics) loads via `Resources.LoadAll` at startup.

**Data Storage — a genuine gap versus the original plan.** There is currently no backend of any kind. All player progress, coin balance, unlock state, settings, and purchase records live in local PlayerPrefs only. The original design specified a Supabase-backed cloud sync; that still doesn't exist, so there's no cross-device progress restore beyond platform purchase-restore. Analytics (the original design specified Firebase Analytics; Unity Gaming Services Analytics was used instead — see below) is now wired in code as of 2026-09-18, but no real data has been verified flowing yet — see the Analytics status note in Section 13.

**Platform Support.** iOS (iPhone only for v1) and Android. Orientation is landscape only. Minimum iOS version confirmed at 15.0 — a deliberate choice: iOS 26 accounts for roughly 70% of active iPhones and iOS 18 another ~18%, with every version below iOS 18 collectively in the low single digits. iPad support is switched off for v1 (target device set to iPhone only): the fixed-corner HUD was never tested against iPad's Split View or Stage Manager, and no iPad hardware exists in the current dev environment to test against.

**Localization.** English only for launch. No localization infrastructure exists (no Unity Localization package; every UI string is a hardcoded English literal) and no business case for day-one localization has been identified. Deliberate scope decision — revisit only if a specific market opportunity justifies the infrastructure work.

**Actual Tech Stack:**

| Component | Actual |
|---|---|
| Game Engine | Unity 6 (6000.5.0f1), Universal Render Pipeline, 2D Renderer — not the original design's Unity 2022 LTS |
| Input | Unity Input System package exclusively |
| Programming | C#, with Claude Code as the primary engineering collaborator |
| Art Assets | Kling AI (sprites), hand cleanup as needed |
| UI Assembly | Built programmatically via a suite of custom Unity Editor tools, not hand-composed in an external design tool |
| Ad Mediation | Unity Ads Mediation (LevelPlay), mediating AdMob + Unity Ads |
| In-App Purchases | Unity IAP (com.unity.purchasing 5.4.2), the newer async UnityIAPServices/StoreController API |
| Analytics | Unity Gaming Services Analytics (com.unity.services.analytics 6.3.0) — wired 2026-09-18, all 5 custom events registered in the dashboard 2026-09-19, not yet verified end-to-end on a real device |
| Version Control | Git / GitHub |

---

## 13. Current Development & Launch-Readiness Status

**Complete:**
- Core gameplay: movement, maze rendering, all 6 robot AI types (5 in active use), abilities, combos, chain scoring, respawn/revive system, level timer
- 175 levels across all 7 worlds (4 free + 3 purchase-gated), generated and verified for connectivity and solvability
- All UI screens listed in Section 7, wired end to end
- Ad mediation (LevelPlay, mediating AdMob + Unity Ads) integrated in code
- A real iOS device build (TestFlight) has been produced and is actively playtested
- All 22 IAP products registered in App Store Connect; purchases tested and confirmed working end-to-end on the iOS TestFlight build

**In Progress:**
- Ad network live/approval status on iOS — LevelPlay init consistently fails (`Error 2080`) even after every individually-checkable network/instance-level dashboard setting (ironSource, AdMob, Unity Ads) was confirmed correctly configured. Both the iOS and Android apps show "Store Availability: Not live yet" (requires a real public App Store listing, which doesn't exist yet — only a TestFlight build), currently the strongest remaining lead but not confirmed as the actual mechanism. Escalated to LevelPlay/ironSource support as the next step. Update 2026-09-20: the iOS app key and ad unit IDs in the build did not match the dashboard (the same mistake that broke Android ad init) and were corrected in code, so the "Not live yet" lead above is unconfirmed until iOS is rebuilt and retested.
- Android: a local build installs, launches and plays on a real device (a missing launcher activity in the custom manifest was fixed 2026-09-20); LevelPlay init succeeds but ad loads return `509 No fill`; Android has had no purchase test yet
- Google Play Console: app entry created, but the 22 IAP products, signing keystore, an internal-testing build, and license testers still need to be set up
- Android-side ad network setup — init works; ad fill (`509 No fill`) not yet resolved
- Analytics: Unity Gaming Services Analytics is now wired in code (level/purchase/ad events), and all 5 custom events were registered in the Unity Analytics Event Manager dashboard on 2026-09-19, but nothing has been verified flowing end-to-end on a device yet.

**Not Started:**
- The Post-Launch Roadmap items in Section 14

**Known Content Gaps:**
- Vulnerable robot sprite, Loading screen art, and some minor UI chrome remain placeholders
- Character voice barks are not built

---

## 14. Post-Launch Roadmap

Deliberately out of launch scope — not cut from the game's future, and not a promise of a near-term update.

- Boss Levels — a multi-stage Harvest Robot Commander encounter, originally planned every 25 levels
- Season Pass — a monthly subscription with exclusive cosmetics and a coin-earning bonus
- Achievement System — 50+ achievements across progression, skill, discovery, and mastery categories
- Character voice barks — a distinct sound per character on ability use
- "Special" and "Golden" rare crop tiers — additional 200/500-point collectibles beyond the current kernel/vegetable pair
- Bonus Rounds — short, no-robot, high-value timed levels, originally planned every 10 levels
- A true procedural/endless difficulty-scaling mode — the purchasable-world model now serves the "more content" goal the original Endless mode was aimed at, but a genuinely unbounded, procedurally generated mode was never built
- Cross-promotion with the wider Farm Fury franchise — the original design scoped this to the original Farm Fury (physics-destruction) game, which is currently shelved. Farm Fury: Rush is a separate, actively developed project and a more realistic near-term cross-promotion partner.

---

## 15. Marketing Plan, Strategy & Execution

Grounded in the corrected mechanics and monetisation model above. Anywhere real market research, competitive data, or measured cost figures would normally inform a number, that gap is flagged explicitly rather than filled with an invented figure.

**Positioning.** A Pac-Man-structured maze game differentiated by three things: the Farm Fury cast and IP (8 characters with distinct active abilities), the character-swap combo system, and a "keep the run alive" monetisation moment (the revive prompt) rather than a hard game-over screen. The original design flagged "feeling too similar to Pac-Man" as an IP risk and recommended a legal review before global launch — the developer has since reassessed this directly and determined the game does not infringe (distinct character cast/abilities/farm setting/combo system, not a reskin of Pac-Man's specific characters or maze designs) and no formal legal review is being pursued. This is a business decision, not a legal opinion this document can substantiate on its own.

**Target Audience.** The 8–45 casual-gamer range is an unvalidated assumption — no analytics pipeline exists yet to confirm who actually plays. Not hypothetical, though: because every ad request is treated as child-directed (COPPA) at the SDK level, under-13 players are already a real, structural part of the ad-monetisation setup — this constrains available ad demand and should inform audience planning now.

**Soft-Launch Approach:**
1. iOS has a real TestFlight build with confirmed-working purchases; ad network live status on iOS and the whole Android IAP/ad-network registration still need to close out before either platform is soft-launch ready.
2. Before any UA spend, run a small, unlisted or limited-market build specifically to test IAP and ad flows end to end on both platforms (iOS purchase flow is already confirmed; ad flow and all of Android remain open).
3. Only after that: a limited-market soft launch. The Canada/Australia/Nordics shortlist is a reasonable starting point, but should be checked against real cost-per-install data for this specific COPPA-constrained ad setup.

**ASO.** Title/subtitle copy should lead with the actual differentiators (farm-animal cast, ability-driven maze-chase) rather than generic "arcade" or "maze" terms. Keyword selection and competitor analysis need real ASO tooling — none estimated here.

**Launch Channels & UA.** The built monetisation backbone is rewarded-ad-mediation revenue, pairing naturally with a rewarded-video-first UA strategy. Cross-promotion becomes a $0 channel once a sibling title such as Farm Fury: Rush has a real install base. An analytics SDK (Unity Gaming Services Analytics) is now wired in code, but nothing has been verified flowing yet and there's still no dedicated attribution/MMP SDK (Adjust, AppsFlyer, etc.). An MMP was considered on 2026-09-24 and deferred: in a child-directed app it must run with advertising IDs disabled, which leaves mostly the Play install referrer and SKAdNetwork, and it would need a new Families review, Data safety and privacy-policy changes. Install source per content channel is instead measured with store-native campaign links (below), which need no SDK. This gives install counts per channel, not per-user revenue/ROAS. Funnel conversion still depends on Analytics being verified.

**Channel tracking links (2026-09-24, not yet used or verified).** One link per content channel, so installs can be compared by channel. Source names the platform, campaign names the content line.

| Channel | utm_source | utm_medium | utm_campaign / App Store `ct` |
|---|---|---|---|
| TikTok, auto-posted via Eklipse | `tiktok` | `social` | `eklipse` |
| YouTube Shorts, distributed via Blotato | `youtube` | `social` | `blotato_shorts` |
| Higgsfield character-reveal clips | the platform posted to (e.g. `tiktok`) | `social` | `higgsfield_reveal` |
| farmfurygames.com store buttons | `website` | `referral` | `site` |
| TikTok profile bio link | `tiktok` | `social` | `bio` |
| YouTube channel links | `youtube` | `social` | `channel` |
| Organic / no link | — | — | baseline: installs with no campaign |

Google Play: `https://play.google.com/store/apps/details?id=com.farmfury.arcade&referrer=utm_source%3Dtiktok%26utm_medium%3Dsocial%26utm_campaign%3Deklipse` (the `referrer` value is URL-encoded). Results appear in Play Console's acquisition reports under tracked channels (UTM). Only installs from people who tapped the link are counted.
App Store: generate each link with App Store Connect > App Analytics > Campaign Generator (it adds the provider token `pt` and campaign `ct`, max 40 characters); results appear under App Analytics > Sources > Campaigns. Apple only counts users who share app analytics with developers, so these are partial counts. The app's numeric App Store ID isn't recorded in this document yet.
Limits: a view of a video that doesn't lead to a tap on the link isn't attributed to anything; neither store connects an install to later ad or purchase revenue.

**Launch Timeline (dependency-ordered, not date-anchored):**
1. Get the ad network live and approved on iOS (currently blocked on `Error 2080`); finish Android IAP/ad platform registration and produce/purchase-test a real Android device build
2. Verify analytics on a device; start using the per-channel store links (no MMP SDK — see Launch Channels & UA)
3. Internal or closed test pass on both platforms
4. Small-market soft launch, instrumented
5. Global launch decision, informed by real data

---

## 16. Key Performance Indicators

Aspirational planning targets carried over from the original design pass, not validated against any real player data — no analytics pipeline exists yet to measure any of them.

| Metric | Target | Stretch |
|---|---|---|
| Day 1 Retention | 40% | 50% |
| Day 7 Retention | 20% | 30% |
| Day 30 Retention | 10% | 15% |
| Average Session Length | 6 min | 10 min |
| Sessions per Day | 3 | 5 |
| Average Level Reached | 15 | 30 |
| ARPDAU | $0.20 | $0.40 |
| Rating (App Store) | 4.3+ | 4.5+ |

---

## 17. Risks and Mitigation

| Risk | Mitigation |
|---|---|
| Feeling too similar to Pac-Man (IP concern) | Farm Fury visual identity, unique character abilities, and the combo system distinguish the game meaningfully. The developer has reassessed this directly and determined the game does not infringe — no formal legal/trademark review is being pursued. Not a legal opinion this document can substantiate; a real review remains available if circumstances change (e.g. a cease-and-desist, a platform IP complaint). |
| No analytics or attribution verified working yet | Unity Gaming Services Analytics is now wired in code (2026-09-18), and its 5 events are registered in the dashboard (2026-09-19), but nothing has been confirmed flowing on a real device. No attribution/MMP SDK is integrated (deliberately deferred 2026-09-24 for a child-directed app); install source per channel is to be measured with store campaign links instead (Section 15), which give install counts only, not per-user revenue. Treat analytics as launch-blocking until verified — every KPI in Section 16 and every UA decision in Section 15 depends on this. |
| Android platform readiness | IAP and ad-network registration for Android are both still in progress; iOS purchases are confirmed working on a real device, but Android has had no purchase test yet. Ad network live status is also not yet confirmed on either platform. |
| Character balance issues | The original design's mitigation referenced live tuning via a Supabase config — no backend exists, so tuning currently requires a client update. Extensive playtesting before each release is the realistic mitigation. |
| Cosmetic art/rendering uncertainty | Under active reconsideration — hat/skin rendering and per-character positioning have proven fiddly to tune without live visual verification during development. |
| Ad revenue below projection | Multiple revenue streams reduce reliance on any single one; COPPA/child-directed treatment is a known constraint on ad fill and eCPM that should be factored into any revenue projection once real data exists. |
| Market saturation in the arcade genre | Farm Fury brand and character-driven mechanics differentiate from generic clones; cross-promotion depends on a sibling title having real installs. |
| Client-side-only economy is forgeable | Coin balance and non-consumable ownership flags live entirely in local PlayerPrefs with no server-side validation — not a submission blocker, but a real, knowingly accepted business-integrity risk. Not planned to be closed pre-launch; revisit with server-validated receipts if scale ever justifies the backend work. |

---

## 18. Appendix

**Glossary:**

| Term | Meaning |
|---|---|
| Crop pellet | The equivalent of Pac-Man's dots — farm-themed collectibles (corn kernel / vegetable tier) |
| Power crop / power pellet | The equivalent of Pac-Man's power pellet — grants temporary robot-defeating power |
| Robot Factory | The equivalent of Pac-Man's ghost house — a single-cell robot spawn point |
| Warp tunnel | The equivalent of Pac-Man's warp tunnel — teleports the player across the maze |
| Combo chain | A sequence of character swaps within one maze that triggers a combo effect |

**Document Version History:**
- v1.0 (2026-07-26) — Initial GDD, maze-arcade concept.
- v2.0 — Full rewrite against the actual shipped build. Corrected respawn/revive, movement model, ability reworks, maze dimensions, pellet cap, coin formula, cosmetics model, coin pack tiers, Level Select UI, HUD layout, Level Complete screen, actual tech stack, Daily Challenge behaviour, per-world difficulty curve. Added the 3 purchase-gated worlds, full monetisation stack, Post-Launch Roadmap, Marketing Plan. Noted original Farm Fury as shelved.
- v2.1 (2026-08-29) — Documentation-sync pass following the iOS Submission Audit (2026-08-28), closing 7 stale-documentation findings.
- v2.2 (2026-08-29) — Second documentation-sync pass, closing 3 more audit findings.
- v2.3 (2026-08-29) — Noted Stage 3 of the audit's fix sequence (Xcode/iOS-SDK toolchain gate) in Section 13.
- v2.4 (2026-09-18) — Full codebase alignment audit (9 sections checked against source directly, not against prior doc text). Corrected: Horace's ability (renamed "Rear Kick" → "Horseshoe Throw," a thrown projectile, not a knockback — and the Crossfire combo effect that goes with it); the power pellet table's implied Sunflower frequency (auto-promoted to Golden Wheat every time — Sunflower can never actually roll); Wheat Field's level range (76–99 → 76–100, a real off-by-one that silently dropped a real, playable level from the doc); World Purchase's entry point (Settings → Shop hub); the Gameplay HUD's bottom-right button stack (added the undocumented Locker button); Main Menu's button count (2 → 3, added Exit); the Cosmetics entry screen (2 categories → 3, added Machines); the SFX table (6 events → 15, added robot-defeated/combo/7-of-8-characters' ability cues); the full cosmetics roster and pricing (2 categories/7 items/$3.99-flat → 3 categories/14 items/$1.99 hats+trails, $3.99 machines); the IAP product count and table (15 → 22 products, corrected pricing); ad mediation network (AdMob-only → AdMob + Unity Ads) and its live-approval status (confirmed **not live yet** on iOS, `Error 2080`); the parental-gate status (now built and wired, no longer an open gap); device-build and purchase-testing status (a real iOS TestFlight build exists and purchases are confirmed working end to end; Android remains in progress). Where store/dashboard status couldn't be verified from code, it was asked directly rather than assumed.
  - 2026-09-18 addendum: the Pac-Man IP-risk mitigation in Sections 15 and 17 was updated — the developer reassessed the risk directly and determined the game does not infringe; no formal legal/trademark review is being pursued. Noted as a business decision, not a legal opinion this document substantiates.
  - 2026-09-18 addendum: analytics instrumentation (Unity Gaming Services Analytics) was added in code — Sections 12, 13, 15, and 17 updated to move this from "Not Started"/"doesn't exist" to "wired in code, not yet verified flowing." Also updated the live Error 2080 ad-network investigation status in Section 13 (Store Availability "Not live yet" on both apps is the current leading suspect, escalated to LevelPlay support).
  - 2026-09-19 addendum: all 5 analytics custom events (level_start, level_complete, level_failed, purchase, ad_shown) and their parameters were registered in the Unity Analytics Event Manager dashboard; end-to-end verification on a real device is still pending (Android test first).
  - 2026-09-20 addendum: Android device testing. Android build now installs, launches and plays on a real device (custom manifest was missing its launcher activity). LevelPlay init works on Android after correcting the app key and supplying real Ad Unit IDs instead of ad unit names; ads still return `509 No fill`. iOS ad config corrected in code, not yet rebuilt or retested. Android-only D-pad/Pause position nudge added (iOS HUD layout unchanged). Sections 13 and 15 status lines updated.
  - 2026-09-20 addendum (website/legal): the studio website went live at www.farmfurygames.com (domains.co.za hosting). The Privacy Policy and Terms of Use are now hosted there (/privacy/, /terms/) and the app links to them instead of to private draft pages; the Privacy Policy was corrected to describe Unity Analytics (it had said no analytics was active) and the current ad networks; Terms governing law set to the Republic of South Africa. Both remain drafts pending legal review; contact mailboxes named in them still need creating.
  - 2026-09-24 addendum: Level Complete's Play button is now disabled until the star/score reveal and any New Character / New World unlock screen have finished. Tapping Play early (reported on Android) skipped both unlock screens permanently, since the unlocks are saved before they are shown.
  - 2026-09-24 addendum (attribution): decided not to integrate an MMP SDK (AppsFlyer was proposed) for now. In a child-directed app it would run with advertising IDs disabled, adding little over the Play install referrer and SKAdNetwork, and would need a new Families review plus Data safety and privacy-policy changes. Added a per-channel store campaign link scheme to Section 15 instead (Play UTM links, App Store campaign tokens). Links not yet used; no data verified. Analytics debug logging added for Development Builds; analytics itself still not verified on a device.
  - 2026-09-24 addendum (shorts): built a local pipeline (Tools/content-pipeline) that records Development Build gameplay from an Android phone, finds highlights from in-game log markers and renders vertical 9:16 shorts (poster opener, headline, "Free on Google Play"). Messaging leads with dodging robots and collecting crops; no kill/eat wording. Added bio/channel rows to the Section 15 link table and a TikTok/YouTube channel kit. No accounts created or videos posted yet; API uploads to both platforms stay private until each platform audits the app.
