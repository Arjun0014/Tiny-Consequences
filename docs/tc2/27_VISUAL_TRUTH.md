# 27 — The visual truth audit (session 17, Arjun's playtest of 10 Oct 2026)

Arjun opened the game and saw "many people apparently just standing still", "houses apparently standing on roads", a world that "does not
visibly communicate much of the enormous simulation underneath", and forests that still look alike (W-FOREST, `18` §8). This document is the
audit those observations asked for: each one **measured in the simulation's own state and in what the view draws from it**, then fixed at its
cause. Rule (D2-084, D2-089): presentation may elaborate a real activity, never invent one; the drawn world and the simulated world agree.

Tools (headless, read only): `place --load SAVE --still [--days N] [--every S] [--from H] [--to H]` (every person the view draws, sampled through
lived days: walking or standing, their activity, why, how long a standing bout lasts, where they stand - and, after the fix, as `Presence` draws
them); `place --load SAVE --roads [--sid N] [--ex E] [--show K] [--feature F] [--works] [--rawways]` (every standing building against the ground
the view draws: streets and lanes, the ways between villages as smoothed, the square or market place, the palisade, other buildings - at true size
and at the far size the view enlarges them to; and the livestock pens). Saves of another sim version: `scratchpad/v75.py` sets the header (the
format is unchanged).

## 1. People standing still - measured

Seeds 4 and 42 at year 300 (`_s2` saves), two lived days, every person sampled every 2 minutes in their village's daylight (7-19 h):

- **98.4 % of the people drawn stood still; 1.6 % walked.** The view (`WorldObjectsView.RebuildPeople`) drew a person after their step's walk
  at one point, facing one way, with no motion: the shader knew a walk and nothing else.
- What they were doing (shares of those drawn): **in the fields 26-28 %** (bouts of 225 minutes at one spot), **idle at home 21-24 %** (136 min),
  **children at play 20-24 %** (165 min), cooking 4-8 %, eating 5-6 %, woodcutting 2-4 % (195 min), building 1-3 %, at leisure on the green 3 %,
  making and mending at home 2-3 %, hunting 1-2 %, weaving 1.2 % (226 min), training 1 %, water 0.6 %, gathering, charcoal, mining, herding,
  smithing (228 min), quarrying, fishing, potting, hauling, sailing under 0.5 % each.
- **Classified.** A - the simulation active, the view static: about half of everyone drawn (field work, woodcutting, building, crafts, weaving,
  smithing, mining, quarrying, herding, fishing, training, water, hauling). B - genuinely idle: 21-24 % idle at home and ~3 % at leisure on the
  green (adults whose day has nothing the scheduler scores; see §4). Children's play (20-24 %) is real activity drawn as statues.
- **Where they stood**: cooks 80-87 %, eaters 97-99 %, makers 66-73 %, idle 21-22 % and builders 25-46 % of those standing stood *inside their
  own house's walls* (the step's spot is the home's middle +- a few metres): drawn half through a wall, or hidden by it.

## 2. People - fixed (`Core/Agents/Presence.cs`, the shader's poses)

One read-only answer to "where, facing where, doing what" (`Presence.Of`) serves the view, the picking, the camera's follow and the headless audit:
field hands hoe or reap along the rows of the village's plots (sowing walks the furrows), woodcutters swing at a tree with breathers, quarrymen
and miners cut and carry to the pile, smiths hammer facing the smithy (outside its walls), weavers work the loom at the shed, potters sit at the
wheel and fetch clay, builders hammer at the walls they raise (outside them) and fetch timber, herders walk round the herd and stand watching it,
hunters stalk and wait, children run about in play, water is drawn hand over hand at the well, hands at training spar facing each other,
neighbours at leisure turn to each other and talk (some sitting), the idle sit by their doors - talking with the neighbours idle at their doors
near by - eating and cooking happen under the roof (not drawn; the chimney's smoke shows the cooking), makers sit at the doorstep with the work in
their lap. Travellers walk the way as it is drawn (§3), carrying loads in their arms. The person shader (`Instanced.shader`, `TC2Pose`) poses the
kit's figure for sixteen codes - walk, hoe, reap, chop, hammer, sit, talk, carry, row, loom, draw water, spar, run, fish, sit at work, sit and talk -
in the lit pass and the shadow pass. The simulation's point stays the person's place; the drawn point keeps within its work's own ground.

**After** (the same lived days, `Presence`): 9-12 % of those drawn before are now indoors (not drawn), and **28-32 % neither move nor work** -
mostly the idle seated at their doors (74 % of the idle sit) and pauses within work (breathers, a herder watching, a hunter waiting); from 98.4 %.

## 3. Houses on roads - measured

`place --roads` on twenty mature saves (ten seeds at years 100 and 300, `_s2`). The simulation's own ground was nearly clean (homes in a street or
lane 0-4 a world, on the green 0-12, by a river 0-9: `Footprints.Open`). What the view drew was not:

- **The ways between villages were drawn across the lots** (seed 4 at year 300: **286 homes** cut by a drawn way at true size; seed 2: 260; seed
  42: 136). A way was drawn along its route - the A* path from the village's centre to the next cell's centre, smoothed - straight across the ground
  the village had laid out along its streets, and its travellers walked it so.
- **The view enlarged buildings far off** (up to 1.6x, D2-118) checking only their neighbours - not the streets, the lanes, the square or the ring.
- **Lanes were drawn from 20 m along their line; the simulation keeps them clear of houses only from 26 m** (`Streets.Line`).
- **Livestock pens**: `LivestockView` laid a second ring of fences behind each home with beasts, "away from the square", whatever stood there -
  in a street town that runs along the street (seed 4 at year 300: of 168 pens, 26 reached a street or lane, 81 another home); a yard pen checked
  for free ground (`Footprints.YardFree`) was already drawn by `WorldObjectsView`.
- **Palisades**: gates were set toward neighbours' centres, not where ways cross the ring; rings were laid clear of homes only, so 14 works (training
  grounds beyond the homes) stood astride seed 4's rings.
- **Streets laid through standing houses** (C13.3 allows three a street: they come down when left): the drawn street ran under the house.
- Wells, the market's cross and the stores stand on the square by design (a market place widens round them): not faults.

## 4. Houses on roads - fixed

- **`Core/Agents/WayLines.cs`** - a way as it is drawn and walked: in a village with streets it comes in along the street nearest its bearing,
  from the square out past the village's last home (a street's line is kept clear of houses all its length), then round the village's edge to
  where the route leaves the built ground; through a village it passes, in by the street nearest where it comes, over the square, out by the street
  nearest where it goes; points close to each corner keep the drawing's smoothing (Chaikin) from cutting across lots; no road is drawn over a
  square. Travellers are read onto it by their share of the route. Homes cut by a drawn way at year 300: **seed 4 286 -> 1, seed 42 136 -> 0**.
- **`Footprints.Room`** also bounds the far enlargement by the drawn streets, lanes, square and ring (`OpenScale`).
- **Lanes are drawn from where the simulation lines them** (26 m).
- **Pens**: the animals spend the night in the yard pen behind their home where it has free ground; the second ring of fences is laid only where
  there is none.
- **Gates** where the streets run out through the ring and where each worn way crosses it as drawn; **rings are laid clear of the works standing
  beyond the homes too** (`Conflict.Astride`; `--nowallworks` the rule before - a simulation change, measured in §6).
- **A street is not drawn through a building standing in its line**: it stops at the walls and goes on past.

## 5. Fallow in the picture

The soil (D2-139) rests 13-17 % of a bound village's arable. The terrain shader drew a strip worked where its hash fell under **1.15 x** the share
sown - most of the rest hidden - and the same strips always lay fallow. Now the share drawn worked is the village's own (its sown hectares against
its arable), the resting strips turn from year to year (`_TC2Year`), fallow reads as rough grazed grass, and in the growing season some of it lies
ploughed (the fallow's summer ploughing).

## 6. Open

- B (§1): adults idle in daylight 21-24 % of those drawn: measured next with `timeuse` by age - whether the scheduler lacks work a medieval day had
  (wage work, service, households working their own holdings, repairs, crafts for sale) or this is the leisure of a land-bound village with nothing
  more to do. Never kept busy for show.
- The remaining few homes in lanes (3-9 a large world) and on the green (0-12).
- The visual-truth table of every system (§7, from real shots).
