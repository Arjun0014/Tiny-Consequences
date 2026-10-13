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

## 6. The systems in the picture (from the code; each line to be confirmed in real shots)

| System (simulated) | Drawn | Moves | Gap |
|---|---|---|---|
| Fields, crop stages, extra tilth (C3.6a) | the village's plots in its grid, crop colour by stage, furrows, hedges | field hands hoe, reap, sow along the rows (s17) | holdings by household not drawn |
| Fallow, the soil (D2-139) | unsown strips at the true share, turning yearly, some ploughed in summer (s17) | yearly | tiredness itself (yields) not drawn |
| Harvest, the sheaves (C3.7b, D2-149) | the stackyard: a thatched rick on staddles for each ~1,500 FU of sheaves the barns' bays do not hold, round the threshing floor - full from the reaping, emptying through the year as threshed, none before a hungry harvest (the old haystacks by the store's fill are gone) | reaping (s17); threshers in a ring at the floor swinging flails (pose 17) over the sheaves laid out on it - the floor drawn in use only while someone threshes there (s20, `act:Thresh`, `floorbusy`) | no flail in the hand (no held tools are drawn anywhere) |
| Woodlots felled (C1.2, W-FELL D2-150) | the woods' share thins; stumps where the felled trees stood (`PlanetState.Felled`, the felled share not grown back), coppice shoots on some, until the woods regrow over them and they rot (s20: drawn on ground felled bare too; the `woodlot` views frame the felled band - 211-242 stumps drawn) | woodcutters swing, with breathers (s17) | stumps small from 70 m |
| Quarry, mines, bog pits, charcoal (C1.3, C5) | quarry face and cut ground, pits, clamps | cutting and carrying to the pile, raking the clamp (s17) | - |
| Smithy (C1.4b) | smithy, smoke while a smith works | hammering at its front (s17) | - |
| Weaver's shed (C3.1) | shed, tenters while it has wares | at the loom (s17) | - |
| Kiln (C3.2) | kiln, clay pit, jars by the granary, ware for sale | the potter seated at work, fetching clay (s17) | - |
| Market (C1.6, C13.3) | cross, stalls, the widened market place | traders come and go | - |
| Ways, streets, lanes (C4.1, C13) | ribbons by wear along the streets (s17), streets and lanes by frontage | travellers walk them, loads in arms, carts | - |
| Carts (C4.2) | hand and ox carts with their people, parked in yards | yes | - |
| Herds, tending (D2-050), the stubble and the fallow (C3.7d, D2-155) | animals graze the day's ground - from the harvest to the sowing the village's own stubble - yard pens (s17); muck heaps behind the yards by the beasts' share (C3.7c) | herders walk with the herd (s17); carters on each household's own part of the fields after the harvest (s20, `act:Muck`) | grazing on the fallow strips not drawn (the strips are the shader's) |
| Boats, fishing, voyages (C9) | boats on the beach, out with their crews | rowing (s17); shore fishers hold their lines (s17) | - |
| Training (C1.5) | beaten ground by hours of practice | sparring (s17) | - |
| Palisades (D2-074, C7.1) | posts by condition, gates where the ways cross (s17) | raised stretch by stretch | - |
| Homes by means, plans, age (D2-114) | forms, roofs, extensions, gardens, wear | smoke, lit windows | - |
| Gardens (C3.7a, D2-143-145) | beds on each croft as the simulation measures it, by its season: fresh earth dug, in leaf, cleared as gathered, weedy where undug; one wattle fence round the croft | people hoe and stoop in the beds | the beds' crops by kind (one kit bed) |
| Headmen, the first household, lordship (C10.1-2, D2-152) | a standard by the first household's door in its polity's colour (a tributary flies its lord's; taller for a lord over more villages); the headman dressed in it | - | moots, successions as gatherings (not simulated as gatherings) |
| Famine, sickness, death (D2-151, D2-154) | gatherers in the wild; an empty stackyard before a hungry harvest; the burial ground - fresh earth for the last year's dead, grassed mounds with headstones sinking over decades, side by side across their rows on unploughed sward, kept clear of homes and works (a famine or an epidemic shows as a field of fresh graves) | - | the hungry's bodies (not drawn thinner); worn paths may cross a ground |
| Abandonment, ruins (D2-052) | empty homes weather, roofs go, walls fall | - | - |

## 7. Open

- B (§1): adults idle in daylight 21-24 % of those drawn: measured next with `timeuse` by age - whether the scheduler lacks work a medieval day had
  (wage work, service, households working their own holdings, repairs, crafts for sale) or this is the leisure of a land-bound village with nothing
  more to do. Never kept busy for show.
- The remaining few homes in lanes (3-9 a large world) and on the green (0-12).
- The visual-truth table of every system (§7, from real shots).

## 8. Real shots (session 17, second context)

The Unity side compiled first without the editor (`node tc2/scripts/unity.mjs cc`: Unity's csproj files over every source - the C# only) and then
in the batch editor with the shaders (`unity.mjs shots`; `TC2Pose` compiled in the lit and the shadow pass).

- **`s17a` (seed 4, year 300, a DEEP run's save) showed everyone standing in a row at their doors, facing out - and the log said why: "people drawn
  8275 (at work or on the move 0)".** A saved world lives its days again only from its next midnight (`SimHost.LoadWorld`); the shot was taken 37
  minutes after the load, before it - nobody was living a day, and `Presence` drew each at their door in no pose. (The town views also stood in
  the town's night: the shot hour was the first village's.) Fixed in `ShotRunner`: a loaded world lives into its lived day (three hours past the
  switch) before the shot hour; `--village town` puts the hour at the most peopled place (the town views' own). The game itself loads its own
  saves in lived time; this was a tools fault, but it is the picture Arjun described, so every shot of a loaded save must be read with the log's
  "at work or on the move" count.
- **`s17b`/`s17c` (the same world, a lived morning): "people drawn 3001 (at work or on the move 2155)".** The town at its own 10 h: people on the
  market square and in the streets, walking with their legs swinging, three neighbours talking (a hand raised), a handcart pulled, two seated
  by a door; houses along the streets and none on them; the ways meet at the cross; gardens behind the houses. The first village: ~60 people
  crossing its square in every direction, one drawing water at the well hand over hand, one carrying a load before them, traders at the stalls,
  the granaries with their jars. Open: why so many cross the square at 10 h in the sowing (field hands walking to the village's one field spot,
  visitors at the stalls?) - to be measured with `place --still`, not guessed.
- **The kitchen gardens drawn behind every lived house are worked by nobody and feed nobody** (D2-114 drew them "worked by its hands"; the
  simulation has no garden): a visual-truth fault of the presentation - the design that makes them real is `26` §2h (C3.7a).
- **Adults idle about 7 of the 12 daylight hours** outside the harvest (`timeuse --daylight --stages`, `26` §2h): B of §1 is real idleness, not a
  drawing fault - the household's own work of a medieval year is missing from the scheduler (§7 B; the garden first).
- **The gardens in real shots (session 18; `tc2/out/shots/s18a`, `s18b`: seed 4 `_g2` at year 300, lived to day 8 of the sowing and to day 20)**:
  the season reads - fresh ridged earth where a croft is dug, an olive rough where it is not yet, green beds in leaf; a town's built-up core has
  none. Two faults found and fixed (D2-145): every bed carried its own wattle on three sides, so a croft read as a stack of crates (now one fence
  along its outline), and the croft beside an end overlapped the croft behind (2 x 4 m a side counted twice, drawn twice, over the end yard where
  the woodpile and the cart stand). Before them, D2-144: southern crofts kept the north's calendar (dug at the reaping, gathered while the crop
  grew) - one `SettlementSystem.DayOfYear` for every use of a village's calendar.
