# Farm Fury: Arcade — "The Night the Robots Came"

Animated storyline series for marketing, made clip by clip in Kling AI and narrated by **Cluck** and **Bessie**.

Purpose: tell how the robots invaded, how the farm fought back, and give every character a personality people remember, so viewers want to play as them. Each episode works on its own as a short (TikTok / YouTube Shorts / Reels), and together they make a 4–5 minute launch film.

---

## 0. Ground rules (read before generating anything)

**Kid-safe wording.** The game is marketed to families. In narration, captions and on-screen text, robots get **zapped, bonked, busted, powered down, sent packing** or **sent back to the factory**. Never use: *kill, eat, die, dead, death, destroy, shoot, shot, gun, blood, murder*. (Same list as `Tools/content-pipeline/make_shorts.py` → `AVOID_WORDS`.) Ducky's ability is called **Water Skip** in marketing, not "Skip Shot".

**Names.** The chicken's official name is **Cluck**. Only Bessie calls her **"Clucky"**. It's a running joke between them, and it lets you use the nickname without renaming the character.

**Story facts that must match the game** (from `FarmFury_Arcade_GDD.md`):
- Every maze is a farm field **at night**, under warm moonlight.
- 8 heroes, 5 active robot types, 7 worlds: Corn Field, Vegetable Patch, Orchard, Wheat Field (free), then Frostbite Garden, Golden Sunset, Harvest Moon.
- Power crops (Golden Wheat, Rainbow) make robots turn blue and run away.
- Crops are collected, and there's a single robot factory spawn point in each maze.

**Reference images for Kling.** Upload these as the character reference (Elements / image reference) for every clip a character appears in. Without them Kling will drift off-model.

| Character | Reference image (in `Assets/_Project/Sprites/`) |
|---|---|
| Cluck | `Characters/CluckThumbsUp.png`, `Characters/Cluck_front.png` |
| Bessie | `Characters/BessieThumbsUp.png`, `Characters/BessieSlam.png` |
| Percy | `Characters/PercyThumbsup.png` |
| Woolly | `Characters/WoollyThumbsUp.png` |
| Ducky | `Characters/DuckyThumbsUp.png` |
| Horace | `Characters/HoraceThumbsUp.png` |
| Gerald | `Characters/GeraldThumbsUp.png` |
| Billy | `Characters/BillyThumbsUp.png` |
| Harvester (red) | `Robots/HarvestorRobot_front.png` |
| Scout (pink) | `Robots/ScoutRobot_front.png` |
| Patrol (cyan) | `Robots/PatrolRobot_Front.png` |
| Drifter (orange) | `Robots/DriftRobot_front.png` |
| Drone (silver) | `Robots/Drone.png` |

> ⚠️ **Billy vs Bessie:** Billy's art is black-and-white with a pink belly, so Kling can easily confuse him with Bessie. Always say *"goat with a grey goatee and curved tan horns, no bell"* for Billy, and *"cow with a gold cow-bell on a red collar"* for Bessie. Avoid putting both in one clip unless you're using separate reference slots for each.

**Style lock.** Paste this at the start of every prompt:

> `3D cartoon animation, family-friendly animated film style, warm moonlit farm at night, soft golden rim light, rich saturated colours, expressive faces, smooth motion, cinematic depth of field,`

**Negative prompt** (every clip):

> `realistic, photoreal, horror, scary, gore, weapons, guns, fire damage, text, subtitles, watermark, logo, extra limbs, extra fingers, deformed face, blurry, off-model, human farmer`

**Format.**
- Shorts: **9:16**. Launch film: **16:9**. Generate the hero clips in both if credits allow, otherwise generate 16:9 and crop to 9:16 (keep characters centred).
- Kling clips run 5s or 10s. Each "clip" below is one generation. Use **start/end frame** mode wherever a clip has to join cleanly to the next one.
- Narration is recorded separately and laid over the clips in editing. Two voices: Cluck (fast, bright, high) and Bessie (warm, low, unhurried). For the talk-show episode (Ep 3), use Kling's lip-sync on the close-ups.

---

## 1. The story in one paragraph

On a quiet harvest night, a giant mothership called the **Harvest Hauler** lands in the corn. It's run by **The Combine**, a robot brain with one instruction: *COLLECT EVERY CROP.* It sends out its Harvest Robots, who carve the fields into mazes so they can harvest them row by row, drill tunnels under the farm, and grab everything that looks like a crop: corn, cabbages, fences, the scarecrow's hat, and Woolly's wool. The animals scatter in panic. Cluck and Bessie, the farm's loudest and wisest residents, ring the old barn bell and call everyone to the barn. There, in the hayloft, grows the **Golden Sunflower**, the oldest plant on the farm. When the animals stand together around it, it bursts in a shower of golden pollen, and each animal gets a power that matches who they really are. The pollen is the one thing the robots' sensors can't handle, which is why power crops make them panic and run. Under Bessie's command and with Cluck's enthusiasm, the farm launches **Operation Harvest Home**: sneak into every maze at night, take the crops back, and send the robots packing, field by field, across all seven farms.

**Why it's always night:** the farmer is asleep and the robots run on moonlight-charged batteries, so they only harvest after dark.
**Why the fields are mazes:** the robots cut the crops into harvest corridors with their hedge-trimmer arms.
**Why there are tunnels:** the robots drilled them to move crops back to the factory, and the animals use them as shortcuts.
**Why power crops work:** they carry the Golden Sunflower's pollen, and to robot sensors that glow looks like a system error.

---

## 2. Character bible

The narrators first, then the crew. Each character gets a **personality**, a **catchphrase** for barks and captions, a **way of moving** to put in Kling prompts, and a **power origin** (the moment in Ep 6 where the pollen shows what they already were).

### Cluck the Chicken — narrator, heart of the team
- **Look:** round golden-orange chicken, big red comb, wide eyes, orange beak and feet.
- **Personality:** a tiny chicken with a big attitude. She talks fast, always rushes in first, and treats every moment as the biggest moment of her life. Brave beyond her size, a little dramatic, completely loyal.
- **Catchphrase:** *"Cluck. Chase. Collect. CHAOS!"* (the game's tagline is her battle cry)
- **Moves:** quick scurrying steps, flapping wings when excited, puffing out her chest.
- **Power (Egg Drop):** she's the one who never stops leaving surprises behind her. The pollen turns her eggs golden and robot-busting.
- **Relationship with Bessie:** hates being called "Clucky", secretly loves it.

### Bessie the Cow — narrator, the commander
- **Look:** big black-and-white cow, pink nose, tan horns, gold bell on a red collar.
- **Personality:** the farm's calm, dry-witted mother figure. She's seen storms, foxes and tractors come and go, and a spaceship is just one more thing. She speaks slowly, misses nothing, and her few words land hard.
- **Catchphrase:** *"Steady, Clucky."* / *"Now that's what I call a ground rule."*
- **Moves:** slow, heavy, deliberate steps that shake the ground a little. She rings her bell to get attention.
- **Power (Ground Slam):** she has always been the ground the farm stands on. When she stamps, the earth listens.

### Percy the Pig — the happy-go-lucky one
- **Look:** round pink pig, big grin, black hooves.
- **Personality:** cheerful, snack-obsessed, always the last to notice danger and the first to laugh about it. He's tougher than he looks and rolls with everything, literally.
- **Catchphrase:** *"Roll with it!"*
- **Power (Bounce Roll):** he spent his whole life rolling in the mud. Now he rolls like a bowling ball.

### Woolly the Sheep — the overthinker
- **Look:** fluffy cream wool, black face and legs.
- **Personality:** anxious, polite, worries about everything ("What if there are MORE robots? What if they're behind me?"). Always wished there were more of her to get things done.
- **Catchphrase:** *"Ewe can never have too many of me."*
- **Power (Triple Clone):** the pollen grants her wish: two extra Woollys, who are just as nervous.

### Ducky the Duck — the cool one
- **Look:** bright yellow duck, half-closed relaxed eyes, orange bill.
- **Personality:** laid-back, unbothered, a surfer type. He's the only one who isn't panicking, mainly because he hasn't noticed yet.
- **Catchphrase:** *"Just go with the flow, dude."*
- **Power (Water Skip + Water Spout):** he has always been happiest in the pond. Now he can skip across water like a stone, and blast a golden water spout from his bill that sends robots spinning. *(Matches the game since 2026-10-15: next to water he skips across; anywhere else he fires the Water Spout, 3 tiles, bonking the first robot it hits.)*

### Horace the Horse — the show-off
- **Look:** chestnut horse, white blaze, black mane, big expressive eyes.
- **Personality:** a charming rodeo show-off who loves an audience. He's a horseshoe-toss champion and won't let anyone forget it. Underneath, a real team player.
- **Catchphrase:** *"Ringer!"*
- **Power (Horseshoe Throw):** years of practice at the horseshoe pitch. Now his throws glow gold and send robots spinning.

### Gerald the Turkey — the grump
- **Look:** big dark-brown turkey, huge fanned tail, red face, permanently furious eyebrows.
- **Personality:** permanently offended by everything: the robots, the noise, the moon, Cluck's enthusiasm. His temper is the farm's secret weapon.
- **Catchphrase:** *"You want feathers? I'll GIVE you feathers!"*
- **Power (Feather Burst):** he has always ruffled his feathers when annoyed. Now his temper comes out as a swirling golden burst of feathers that flies straight ahead and bonks the first robot in its path. He does NOT grow or puff up any more. *(Matches the game since 2026-10-19: the old Puff Up was removed; Feather Burst flies 3 tiles. Iron Stampede makes it fire in all four directions.)*

### Billy the Goat — the stubborn one
- **Look:** black-and-white goat, curved tan horns, grey goatee, determined frown. No bell.
- **Personality:** stubborn and headstrong. He charges first and thinks never. He's head-butted every gate on the farm, and he's fiercely protective of the little ones.
- **Catchphrase:** *"Head first. Questions later."*
- **Power (Mud Sling):** his headbutts bounce off robot armour (Ep 2), so he does what goats do best with mud: he flings it. A golden glob of mud flies straight ahead and splats the first robot it hits; if it misses, it leaves a sticky mud puddle that catches the next robot to roll in. *(Matches the game since 2026-10-19: Mud Sling replaced Headbutt Through; 3 tiles, the puddle lasts 3 seconds.)*

### The Robots — "and their angle"

They're not evil. They're **over-efficient**. The Combine told them to *collect every crop*, and they take it far too literally. They're funny because they're so serious.

| Robot | Personality | Angle / voice line (robotic text-to-speech) | Matches in-game behaviour |
|---|---|---|---|
| **Harvester** (red, tracked, big arms) | The foreman. Blunt, single-minded, always walks straight at you. | *"CROP DETECTED. COLLECTING."* | Direct pursuit |
| **Scout** (pink, little hat, antenna) | The smart one. A know-it-all that predicts where you're going. | *"Calculating your next move… you will go LEFT."* | Targets ahead of the player |
| **Patrol** (cyan, tank body) | The rule-follower. Always works with Harvester and boxes you in from the other side. | *"Flanking procedure engaged. Partner, go."* | Flanks opposite Harvester |
| **Drifter** (orange, square head, scowl) | Moody and indecisive. Brave from a distance, nervous up close. | *"I'm coming for y— actually, never mind."* | Chases far, retreats close |
| **Drone** (silver, four rotors, grin) | The creepy one. Floats over walls, giggles, never stops. | *"Walls? How… quaint. Hee hee hee."* | Ignores interior walls |
| **The Combine** (the mothership brain) | Never seen fully. A huge red eye in the Harvest Hauler. | *"COLLECT. EVERY. CROP."* | Story only |

> Heavy Robot is in the game's data but not spawned in any level, so leave it out of the launch story. Keep it in reserve as a **"something bigger is coming"** post-launch teaser for a future update.

---

## 3. Episode plan

| Ep | Title | Length | Purpose |
|---|---|---|---|
| 0 | Teaser: "Something Landed in the Corn" | 15–20s | Hook / launch countdown |
| 1 | The Night the Robots Came | ~40s | The arrival and landing |
| 2 | Chaos on the Farm | ~40s | Animals' dismay and chaos |
| 3 | Cluck & Bessie: The Debrief | ~45s | Commentary talk-show on the landing |
| 4 | The Barn Meeting *(made 2026-10-19)* | ~42s | All eight in the barn: Cluck's rally, Bessie's plan, the hayloft secret. Replaces "The Barn Bell" below as the direct follow-on to Ep 3's "Ring the bell"; the animals are already gathered (no bell or arrival shots). Ends on "NEXT: THE GOLDEN GLOW". |
| (5) | Meet the Metal | ~45s | The robots and their angle. Moved after the barn meeting (2026-10-19); can also run as the five robot solo shorts |
| — | The Barn Bell | ~35s | Call to arms. Superseded by Ep 4 The Barn Meeting; its lines were reused there |
| 6 | The Golden Glow | ~60s (+8 solo shorts) | Characters receive abilities |
| 7 | Operation Harvest Home | ~45s | Military-style briefing |
| 8 | Into the Mazes | ~50s | Maze run through all 7 worlds |
| 9 | Combo Chaos (bonus) | ~35s | Shows the character-swap combos |
| End | End card | 4s | Call to action |

Each clip below has: **Refs** (images to attach), **Prompt** (paste after the style lock), **Camera**, **VO** (voice-over) and **SFX/Music**.

---

### EPISODE 0 — Teaser: "Something Landed in the Corn"

**0.1** · 5s · Refs: none
**Prompt:** Quiet moonlit cornfield at night, tall golden corn stalks swaying, a red barn and windmill in the distance, fireflies drifting. Suddenly a huge red light sweeps across the field from the sky, and the corn bends flat in a circle from a strong downdraft.
**Camera:** slow push in toward the barn, then a sharp tilt up to the sky.
**VO — Cluck (whisper):** "Bessie… Bessie, are you awake?"
**SFX:** crickets, then a deep mechanical hum.

**0.2** · 5s · Refs: Cluck, Bessie
**Prompt:** Inside a cosy barn at night, a round golden-orange chicken peeks wide-eyed through a gap in the wooden planks with red light flickering across her face; behind her a large black-and-white cow with a gold bell opens one sleepy eye.
**Camera:** close-up on Cluck's eye, rack focus to Bessie.
**VO — Bessie (deadpan):** "I am now, Clucky."
**VO — Cluck:** "Don't call me— THERE'S A SPACESHIP IN THE CORN!"

**0.3** · 5s · Refs: Harvester, Scout, Drone
**Prompt:** Low angle in the cornfield, a ramp lowers from a glowing spaceship, three cartoon robots roll and fly down it in silhouette, red glowing eyes snapping on one by one.
**Camera:** low static, slight dolly back.
**Robot VO:** "COLLECT. EVERY. CROP."
**Title card:** *FARM FURY: ARCADE — The night the robots came.*

---

### EPISODE 1 — The Night the Robots Came

**1.1** · 10s · Refs: none
**Prompt:** Wide establishing view of a peaceful family farm at night under a giant full moon: cornfield, vegetable patch, apple orchard, wheat field, red barn with a bell tower, a windmill, a duck pond. Warm light in the barn window. Calm and cosy.
**Camera:** slow aerial glide over the farm toward the barn.
**VO — Cluck:** "It was harvest night. The corn was golden, the carrots were crunchy, and everybody was fast asleep…"
**VO — Bessie:** "…except one chicken who'd had too much corn before bed."
**Music:** gentle, folksy farm theme.

**1.2** · 5s · Refs: none
**Prompt:** Night sky full of stars; one star grows bigger and redder, revealing a huge round spaceship shaped like a combine harvester, with spinning harvester blades along its front, giant tractor-tread landing legs and a single big red eye. Big glowing letters on the side read "HARVEST HAULER".
**Camera:** static wide on the sky, ship descending toward camera.
**Note:** if Kling garbles the lettering, generate without text and add "HARVEST HAULER" in editing.
**SFX:** rising whoosh, deep hum.

**1.3** · 5s · Refs: none
**Prompt:** The giant harvester spaceship touches down in the middle of the cornfield, landing legs thudding into the soil, a ring of dust and loose corn kernels blasting outward, scarecrow's hat flying off and spinning through the air.
**Camera:** wide, slight camera shake on impact.
**SFX:** heavy THOOM, corn rustling.

**1.4** · 5s · Refs: none
**Prompt:** Close-up of the spaceship's giant red mechanical eye opening, scanning lines sweeping across the farm, a holographic list floating in front of it showing cartoon icons of corn, carrots, apples, wheat, then a fence and a scarecrow.
**Camera:** slow push into the eye.
**Robot VO (The Combine):** "Target: farm. Instruction: collect every crop. Every. Single. One."

**1.5** · 10s · Refs: Harvester, Scout, Patrol, Drifter, Drone
**Prompt:** A large ramp lowers from the spaceship, white light pours out, and a line of cartoon robots rolls out in formation: a big red tracked harvester robot with long arms, a small pink robot with a little hat, a cyan tank-like robot, an orange square-headed robot with a scowl, and a round silver drone with four propellers flying above them. Their red eyes glow.
**Camera:** low angle at the bottom of the ramp, robots roll past the lens.
**VO — Cluck:** "And out came… them."
**VO — Bessie:** "The Harvest Robots."
**SFX:** marching servos, beeps.

**1.6** · 10s · Refs: Harvester, Patrol
**Prompt:** The red harvester robot and the cyan tank robot use spinning hedge-trimmer arms to cut neat corridors through the tall cornfield, turning it into a glowing maze seen from above; corn cobs fly into a hopper on the red robot's back.
**Camera:** top-down drone view slowly rising to reveal the full maze pattern.
**VO — Bessie:** "In one night they turned our fields into mazes. Harvest corridors, they called them."
**VO — Cluck:** "Rude, is what I called them."

---

### EPISODE 2 — Chaos on the Farm

**2.1** · 5s · Refs: Percy
**Prompt:** Round pink pig sleeping happily in a mud puddle by the barn, snoring, a half-finished corn cob on his tummy; a red robot arm reaches in and plucks the corn cob away; the pig wakes up, looks at his empty tummy, then up, and his jaw drops.
**Camera:** medium, static.
**VO — Cluck:** "Percy lost his midnight snack."
**Percy bark:** "…Hey! That was for later!"

**2.2** · 5s · Refs: Woolly
**Prompt:** Fluffy cream sheep with a black face running in panicked circles in a field while a silver four-propeller drone hovers above her, its suction nozzle pulling tufts of wool off her back like cotton candy.
**Camera:** handheld follow, comedic.
**Woolly bark:** "Not the wool! It took me a YEAR to grow this!"
**Robot VO (Drone):** "Hee hee. Fluffy crop."

**2.3** · 5s · Refs: Ducky
**Prompt:** Relaxed yellow duck floating on his back in a moonlit pond with sunglasses on his head; behind him a pink robot rolls by carrying an armful of cabbages; the duck slowly lowers his sunglasses and raises an eyebrow.
**Camera:** low over the water surface.
**Ducky bark:** "…Dude. Not cool."

**2.4** · 5s · Refs: Gerald
**Prompt:** Big brown turkey with a huge fanned tail standing furiously on a fence post, his red face getting redder, steam puffing from his nostrils, as an orange square-headed robot carries away the fence rail next to him.
**Camera:** slow push in on Gerald's furious face.
**Gerald bark:** "That. Was. My. FENCE."

**2.5** · 5s · Refs: Horace
**Prompt:** Chestnut horse with a black mane posing proudly next to a wall of shiny horseshoe-toss trophies; a red robot arm reaches in and scoops the trophies into its hopper; the horse's proud smile freezes.
**Camera:** medium, the horse turns slowly to camera.
**Horace bark:** "…Did that tin can just take my trophies?"

**2.6** · 5s · Refs: Billy
**Prompt:** Black-and-white goat with curved tan horns and a grey goatee stamping his hoof, lowering his head and charging a cyan tank robot, bouncing comically off its armour and landing on his back, legs in the air, stars circling his head.
**Camera:** side-on wide.
**Billy bark (dizzy):** "Head first… questions… later…"

**2.7** · 10s · Refs: Cluck, Percy, Woolly, Ducky
**Prompt:** Comedic chaos montage across the farmyard at night: chickens running in all directions, feathers everywhere, a pig and a sheep bumping into each other, a duck dives into a bucket, robots with red eyes rolling past collecting crops, a scarecrow spinning. Red searchlights sweep across the yard.
**Camera:** fast whip-pans between moments.
**VO — Cluck:** "It was CHAOS. Feathers! Wool! Panic! Somebody screamed!"
**VO — Bessie:** "That was you, Clucky."
**VO — Cluck:** "…It was a brave scream."

---

### EPISODE 3 — Cluck & Bessie: The Debrief

*A sports-commentary / talk-show episode. Two hay bales, a lamp, a chalkboard behind them. Use lip-sync for the close-ups. It's played like a post-match analysis of the landing.*

**3.1** · 5s · Refs: Cluck, Bessie
**Prompt:** A cosy barn corner set up like a TV sports studio: two hay bales as seats, a hanging lantern, a chalkboard behind. A golden-orange chicken in a tiny headset and a black-and-white cow with a gold bell in a headset sit side by side facing camera, the chicken bouncing with energy, the cow perfectly calm.
**Camera:** static two-shot, slight push in.
**VO — Cluck:** "Welcome back to Barnyard Breakdown, I'm Cluck—"
**VO — Bessie:** "—and I'm Bessie. Let's look at the replay."

**3.2** · 5s · Refs: none (or re-use clip 1.3)
**Prompt:** The spaceship landing shown on an old wooden-framed TV in slow motion, chalk-style circles and arrows animating on top of it.
**Camera:** static on the screen.
**VO — Cluck:** "Look at that landing! Right in the corn! Zero style points!"
**VO — Bessie:** "Flattened forty rows. Very efficient. Very rude."

**3.3** · 5s · Refs: Cluck
**Prompt:** Close-up of the chicken in a headset waving her wings wildly while talking, feathers flying off, chalk dust in the air.
**Lip-sync — Cluck:** "Bessie, they took the CORN. The corn, Bessie! What's next, the EGGS?"

**3.4** · 5s · Refs: Bessie
**Prompt:** Close-up of the black-and-white cow in a headset, calm, one eyebrow slowly rising, gold bell glinting.
**Lip-sync — Bessie:** "Steady, Clucky. Robots are fast. Robots are strong. But robots can't think like a farm."

**3.5** · 5s · Refs: Cluck, Bessie
**Prompt:** The cow taps the chalkboard with a hoof; a chalk drawing of the farm animates, showing tiny animal doodles surrounding a robot doodle; the chicken leans in, amazed.
**Camera:** over-shoulder onto the chalkboard.
**VO — Bessie:** "Eight of us. Eight ways of being stubborn."
**VO — Cluck:** "Is this… a PLAN?"
**VO — Bessie:** "It's the start of one. Ring the bell."

**3.6** · 5s · Refs: Cluck, Bessie
**Prompt:** Both look at camera; the chicken grins and does a wing thumbs-up, the cow gives a hoof thumbs-up, sparkles appear around them.
**VO — Cluck:** "Stay tuned, farm fans!"
**Caption:** *Next: Meet the Metal.*

---

### EPISODE 4 — Meet the Metal

*Each robot gets a "wanted poster" intro: a short clip, a stamp, and a line in the style of a nature documentary from Bessie and Cluck. Each clip also works as a 5–10s solo short.*

**4.1** · 5s · Refs: Harvester
**Prompt:** Big red tracked robot with long arms rolling straight down a moonlit maze corridor toward camera, never turning, corn cobs flying into its hopper, red eyes locked forward.
**Camera:** static at the end of the corridor; the robot fills the frame.
**VO — Bessie:** "The Harvester. The boss. It sees you, it comes for you. No tricks."
**VO — Cluck:** "Just like Gerald at dinner time."
**Stamp:** *HARVESTER — DIRECT PURSUIT*

**4.2** · 5s · Refs: Scout
**Prompt:** Small pink robot with a little hat and antenna stopping at a maze junction, a holographic arrow projecting ahead of it, then it zips ahead to cut off the path.
**Camera:** top-down, then a quick zoom.
**VO — Cluck:** "The Scout. Thinks it knows where you're going."
**Robot VO (Scout):** "Calculating your next move…"
**VO — Bessie:** "Usually it does."
**Stamp:** *SCOUT — PREDICTS YOUR PATH*

**4.3** · 5s · Refs: Patrol, Harvester
**Prompt:** Cyan tank-like robot and red harvester robot approaching a corner of the maze from opposite sides, giving each other a mechanical high-five, red eyes glowing.
**Camera:** top-down, slow push.
**VO — Bessie:** "The Patrol. Never works alone. Its partner comes from the front; it comes from behind."
**Stamp:** *PATROL — THE FLANKER*

**4.4** · 5s · Refs: Drifter
**Prompt:** Orange square-headed robot with a scowl charging bravely down a maze corridor, then stopping, sweating oil, and backing away nervously when it gets close to the camera.
**Camera:** low angle, comedic hold.
**Robot VO (Drifter):** "I'm coming for y— …actually, never mind."
**VO — Cluck:** "The Drifter. Big talk from far away."
**Stamp:** *DRIFTER — MOODY*

**4.5** · 5s · Refs: Drone
**Prompt:** Round silver drone with four propellers and a grin gliding straight over the hedge walls of a moonlit maze, ignoring the corridors, eyes glowing, giggling.
**Camera:** low angle looking up as it passes overhead.
**Robot VO (Drone):** "Walls? How quaint. Hee hee."
**VO — Bessie:** "The Drone. Floats over walls. Watch the sky."
**Stamp:** *DRONE — IGNORES WALLS*

**4.6** · 5s · Refs: all 5 robots
**Prompt:** All five robots lined up in front of the giant spaceship like a team photo, red eyes glowing; then a single rare golden-wheat stalk drifts into frame glowing bright gold, and the robots' screens flash with error symbols, their eyes turning blue as they scramble backwards in panic.
**Camera:** static, then a quick zoom on the glowing wheat.
**VO — Cluck:** "But they've got one weakness…"
**VO — Bessie:** "…and it grows on our farm."
**Robot VO:** "ERROR. ERROR. GOLDEN PROTOCOL NOT FOUND."

---

### EPISODE 5 — The Barn Bell

**5.1** · 5s · Refs: Bessie
**Prompt:** Black-and-white cow climbing the barn ladder to the bell tower under a huge full moon, her gold bell catching the moonlight, robots' red searchlights sweeping the fields below.
**Camera:** low angle looking up, moon behind her.
**VO — Cluck:** "Bessie hadn't climbed that ladder in years."
**VO — Bessie:** "And I'm feeling every one of them."

**5.2** · 5s · Refs: Bessie, Cluck
**Prompt:** The cow rings the big old barn bell with her horns while the chicken holds on to the rope, flapping. Golden sound-wave rings ripple out across the farm.
**Camera:** wide, slow arc around the bell tower.
**SFX:** deep BONG, BONG, BONG.

**5.3** · 10s · Refs: Percy, Woolly, Ducky, Horace
**Prompt:** Across the moonlit farm, animals hear the bell and look up: a pink pig climbs out of mud, a fluffy sheep peeks out from a bush, a yellow duck paddles to the pond's edge, a chestnut horse lifts his head. One by one they start running toward the glowing red barn.
**Camera:** quick cuts, each animal turns to camera.
**VO — Bessie:** "When the barn bell rings, everyone comes home."

**5.4** · 5s · Refs: Gerald, Billy
**Prompt:** The grumpy turkey stomps toward the barn muttering, while the goat gallops past him so fast the turkey's tail feathers spin.
**Gerald bark:** "Fine. FINE. But I'm not happy about it."
**Billy bark:** "You're never happy about it!"

**5.5** · 10s · Refs: Cluck, Bessie, Percy, Woolly, Horace (group; re-run for Ducky, Gerald, Billy if Kling can't fit all 8)
**Prompt:** Inside the big barn lit by lanterns, eight farm animals gather in a circle on the hay, looking nervous but determined. A golden-orange chicken jumps onto a barrel to speak. Dust floats in the lantern light.
**Camera:** slow 360° orbit around the group.
**VO — Cluck (on the barrel):** "They took our crops. They cut up our fields. They took WOOLLY'S WOOL!"
**Woolly:** "It was a really good fleece."
**VO — Cluck:** "So who's with me?!"
**Billy:** "Head first!"
**Gerald:** "…Ugh. Fine."
**Note:** 8 characters is a lot for one Kling generation. Plan to generate two half-groups (4 + 4) and cut between them, or use the wide shot with silhouettes plus reaction close-ups.

---

### EPISODE 6 — The Golden Glow

*The heart of the series. A group clip, then each animal's power reveal. Each reveal also works as a solo short ("Meet Percy").*

**6.1** · 10s · Refs: Cluck, Bessie
**Prompt:** In the barn hayloft, moonlight through a round window falls on an enormous ancient sunflower growing out of the old boards, its petals gently glowing gold. The chicken and the cow look up at it in awe.
**Camera:** slow tilt up the stem to the glowing flower head.
**VO — Bessie:** "Every farm has a secret. Ours is up in the hayloft."
**VO — Cluck:** "The Golden Sunflower. Older than the barn. Older than Bessie!"
**VO — Bessie:** "Careful."

**6.2** · 10s · Refs: group (as 5.5)
**Prompt:** The eight farm animals stand in a circle around the giant golden sunflower, touching hooves, wings and feet in the middle. The flower opens fully and bursts in a slow-motion shower of glowing golden pollen that swirls around every animal like fireflies.
**Camera:** top-down looking straight down at the circle, slowly spinning.
**VO — Bessie:** "When we stood together, the sunflower answered."
**Music:** big orchestral swell.

**Power reveals** — 5s each. Same structure every time: the animal glows gold, does their move once, then strikes a pose. Use the ability-cast SFX from the game for each.

| Clip | Refs | Prompt (after style lock) | VO / bark |
|---|---|---|---|
| **6.3 Cluck** | Cluck | Golden pollen settles on the chicken; she proudly lays a glowing golden egg, it sparkles, and a red robot rolling over it is bonked into a spin with a burst of sparkles. | Bessie: "Clucky got Egg Drop." Cluck: "And it's CLUCK!" |
| **6.4 Bessie** | Bessie (`BessieSlam.png`) | The cow rises onto her back legs and stamps down; a golden shockwave ring ripples out across the hay, bouncing two robots away and leaving the ground glowing for a moment. | Cluck: "Bessie got Ground Slam." Bessie: "Now that's what I call a ground rule." |
| **6.5 Percy** | Percy | The pig tucks into a ball and rolls forward like a golden bowling ball down a barn aisle, knocking a robot over like a bowling pin, then pops up grinning. | Percy: "Roll with it!" |
| **6.6 Woolly** | Woolly | The sheep glows, there's a puff of wool, and suddenly three identical sheep stand side by side looking at each other in surprise. | Woolly ×3: "Ewe can never have too many of me." |
| **6.7 Ducky** | Ducky | The duck skips across a moonlit pond in three golden bounces like a skipping stone, lands on the far bank, then blasts a glowing golden water spout from his bill that sends a robot spinning backwards, dripping. He slides his sunglasses on. | Ducky: "Just go with the flow, dude." |
| **6.8 Horace** | Horace | The horse rears up, spins a glowing golden horseshoe and throws it down a corridor; it hits a robot with a clang and the robot spins away. | Horace: "Ringer!" |
| **6.9 Gerald** | Gerald | Golden pollen settles on the furious turkey; his tail fans out, he shakes himself hard, and a swirling burst of glowing brown and gold feathers shoots straight down a corridor, bonking a robot into a spin. He stays his normal size. | Gerald: "You want feathers? I'll GIVE you feathers!" *(new line, record)* |
| **6.10 Billy** | Billy | Golden pollen settles on the goat; he scoops a glob of glowing golden mud from a puddle and flings it down a corridor. It splats a robot, which spins away dripping; a second glob misses and leaves a sticky golden puddle that the next robot rolls straight into and gets stuck. | Billy: "Okay… maybe MUD first." *(new line, record; a callback to his failed headbutt in Ep 2)* |

**6.11** · 5s · Refs: all 8 (silhouettes OK)
**Prompt:** All eight animals in a heroic line in the barn doorway, glowing faintly gold, moon behind them, their shadows stretching long across the farmyard toward the distant spaceship.
**Camera:** low angle, slow push.
**VO — Cluck:** "Robots, meet the Farm Fury."

---

### EPISODE 7 — Operation Harvest Home

*Military-style briefing in the barn. Bessie is the commander, Cluck the over-eager second-in-command.*

**7.1** · 5s · Refs: Bessie
**Prompt:** Barn interior turned into a command room: a big chalkboard map of the farm divided into seven fields, lanterns, a pointer stick. The cow wears a tiny green army beret and stands at the board like a general.
**Camera:** slow dolly along the line of animals to reveal Bessie.
**VO — Bessie:** "Listen up, farm. This is Operation Harvest Home."

**7.2** · 5s · Refs: Cluck
**Prompt:** The chicken in an oversized army helmet that keeps slipping over her eyes salutes too hard and falls off her hay bale.
**Cluck:** "Ma'am, yes ma'am!"
**Bessie:** "Sit down, Clucky."

**7.3** · 10s · Refs: Bessie
**Prompt:** Close-up of the chalkboard as the cow's pointer taps each part of a chalk maze drawing, which animates: dotted crop lines lighting up, a glowing golden wheat icon, a tunnel arrow wrapping from one side to the other, a robot factory symbol in the centre.
**Camera:** insert close-ups following the pointer.
**VO — Bessie:**
"Objective one: collect every crop in the maze. Every last kernel.
Objective two: find the golden crop. Grab it, and the robots turn blue and run.
Objective three: the tunnels on the edges take you to the other side. Use them.
And watch the factory. That's where they come from."
*(This is the How-to-Play tutorial, told as a briefing.)*

**7.4** · 5s · Refs: Bessie
**Prompt:** The cow taps the chalkboard where the farm map shows seven fields, with the last three behind a chalk drawing of a padlock with a golden glow.
**VO — Bessie:** "Four fields to start. Three farms beyond the hills."
**VO — Cluck:** "Frostbite Garden. Golden Sunset. Harvest Moon!"

**7.5** · 5s · Refs: Bessie, Cluck
**Prompt:** The cow places her hoof in the centre, the chicken's wing on top, then more hooves, wings and feet join from all sides in a team huddle; golden sparkles rise.
**VO — Bessie:** "We swap in, we swap out, we work together. Nobody gets left in the maze."
**All:** "FARM FURY!"

---

### EPISODE 8 — Into the Mazes

*The action montage. Every world gets one clip showing a different character's move in its setting. Use the game's real backdrop art as the start frame where possible, so the trailer matches what players will see: `Sprites/UI/World1_Cornfield.png` (Corn Field), and `Sprites/Cosmetics/CosmeticType_MazeTheme/FrostbiteGarden_Backdrop.png`, `GoldenSunset_backdrop.png`, `Harvest_Backdrop.png` for the three purchase worlds. Vegetable Patch, Orchard and Wheat Field have no standalone backdrop file, so describe them in the prompt or take a screenshot of a level in that world.*

| Clip | World | Refs | Prompt (after style lock) | VO |
|---|---|---|---|---|
| 8.1 | Corn Field | Cluck, Harvester | Golden-orange chicken sprinting down a moonlit corn maze corridor collecting glowing corn kernels, a red robot chasing; she drops a golden egg and the robot behind her bonks over in sparkles. | Cluck: "Corn Field. MY turf." |
| 8.2 | Vegetable Patch | Percy, Scout | Pink pig rolling at speed between rows of cabbages and carrots behind lattice fences, knocking a pink robot over like a bowling pin. | Percy: "Roll with it!" |
| 8.3 | Orchard | Horace, Patrol | Chestnut horse galloping between fruit tree trunks under a deep purple night sky, throwing a golden horseshoe that bounces a cyan tank robot away; apples fall. | Horace: "Ringer!" |
| 8.4 | Wheat Field | Woolly, Drifter | Three identical fluffy sheep running in different directions through a wheat-sheaf maze on cracked dry earth, confusing an orange robot that spins in circles. | Woolly: "Which one's the real me? …Me neither." |
| 8.5 | Frostbite Garden | Ducky, Drone | Yellow duck skipping across a frozen pond in a snowy night garden maze in three golden bounces, then blasting a golden water spout upward that knocks a silver drone tumbling out of the air. | Ducky: "Ice, ice, ducky." |
| 8.6 | Golden Sunset | Billy, Harvester | Goat with curved horns flinging a glowing golden mud ball down a maze glowing with warm sunset-orange light; it splats a red robot, which spins away dripping. | Billy: "Head first!" |
| 8.7 | Harvest Moon | Gerald, Bessie | Under a giant orange harvest moon, the turkey blasts a swirling golden feather burst and the cow stamps a golden shockwave at the same moment, sending four robots tumbling in every direction. | Bessie: "Harvest Moon." Gerald: "And I'm STILL mad." |
| 8.8 | All | Golden wheat | A rare glowing golden wheat stalk is grabbed; every robot in the maze turns blue and flees in panic, little blue error clouds above their heads. | Cluck: "Golden crop! RUN, robots, RUN!" |

**8.9** · 5s · Refs: all 8 (or silhouettes)
**Prompt:** Dawn starting to break on the horizon as all eight animals stand on the barn roof, the spaceship in the distance with a few robots retreating toward it, but its big red eye still glowing.
**VO — Bessie:** "We took back the corn that night."
**VO — Cluck:** "But the Combine's still out there…"
**VO — Bessie:** "Then so are we."

---

### EPISODE 9 (bonus) — Combo Chaos

*Shows the character-swap combo system, the game's unique selling point. The names match the in-game callouts. Each clip shows the first character tagging out, the second tagging in, and a big gold combo title card.*

| Clip | Combo | Prompt (after style lock) | Title card / VO |
|---|---|---|---|
| 9.1 | FEATHER STORM (Cluck → Woolly) | Chicken high-fives the sheep in a burst of feathers; the sheep splits into three, and each copy leaves a trail of golden eggs. | **FEATHER STORM!** |
| 9.2 | EARTHQUAKE ROLL (Bessie → Percy) | Cow stamps the ground and the pig rolls on the shockwave like a surfer, rolling three times further down the corridor, glowing gold, knocking robots aside like bowling pins. | **EARTHQUAKE ROLL!** |
| 9.3 | IRON STAMPEDE (Bessie → Gerald) | Cow taps the turkey; he shakes out a golden feather burst in all four directions at once, bonking robots on every side of the junction. | **IRON STAMPEDE!** |
| 9.4 | CROSSFIRE (Billy → Horace) | The goat bucks a horseshoe up into the air, the horse catches it and throws it twice as far down the corridor in a golden streak. | **CROSSFIRE!** |
| 9.5 | FULL FURY (5+ animals) | Five animals tag in one after another in quick flashes; a golden shockwave freezes every robot on the maze in place with stars circling their heads. | **FULL FURY!** Cluck: "Swap. Combo. FURY!" |
---

### END CARD (every episode)

**E.1** · 4s · Refs: Cluck, Bessie
**Prompt:** Chicken and cow side by side giving thumbs up in front of the moonlit barn, golden sparkles; space at the top for the logo and at the bottom for store badges.
**VO — Cluck:** "Farm Fury: Arcade!"
**VO — Bessie:** "Free to play. Come help us out."
**Overlay:** game logo · "Cluck. Chase. Collect. Chaos." · **Google Play** badge now; add the **App Store** badge the day Apple approves (the badge and QR card are already in `Tools/store-assets`).

---

## 4. Production workflow

1. **Lock the look first.** Generate clip **1.1** (the farm) and **6.2** (the golden circle) first. If those two look right, the style lock works; adjust it before generating anything else.
2. **One character at a time.** For each character, generate their reveal clip (6.3–6.10) first and keep the best frame as an extra reference image for all later clips. This keeps them on-model.
3. **Start/end frames for continuity.** Wherever two clips cut together in the same location (1.2 → 1.3 → 1.4, 5.1 → 5.2), export the last frame of the first clip and use it as the start frame of the next.
4. **Record narration before editing.** Cluck and Bessie's lines decide the timing. Trim clips to the voice, not the other way round.
5. **Backgrounds in-clip, no green screen.** Generate characters with their background in the clip. Chroma-keying Kling output was unreliable in the original Farm Fury project.
6. **Captions.** Burn in captions for every line (most shorts are watched on mute), and run them through the content pipeline's word check before posting:
   ```
   python -c "from make_shorts import check_wording; check_wording('your caption here')"
   ```
   (run from `Tools/content-pipeline`).
7. **Tracked links.** Post with the per-channel store links from GDD §15 so you can see which episode drives installs.

### Suggested release order

| Week | Post |
|---|---|
| Launch −1 | Ep 0 teaser, then Ep 1 |
| Launch week | Ep 2, Ep 3, Ep 4 (one every 2 days), with the robot solo clips 4.1–4.5 as filler shorts |
| Launch day (App Store live) | Ep 5 + Ep 6 together, then the full launch film (Eps 1–8 cut together, 16:9) on YouTube |
| Launch +1 | The 8 "Meet the crew" solo reveals (6.3–6.10), one per day |
| Launch +2 | Ep 7 briefing (as the "How to play" video), Ep 8, Ep 9 combos |
| Later | Heavy Robot teaser: "Something bigger is coming…" for the first content update |

### Credit budget (rough)

About 70 clips in total (≈55 × 5s, ≈15 × 10s). Plan for **2–3 generations per clip** to get a keeper, so roughly 150–200 generations. Hero clips (1.2–1.5, 6.2, 6.11, 8.8) deserve more attempts; the bark clips usually work in one or two.
