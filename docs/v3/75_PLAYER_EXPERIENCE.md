# V3-75 — Playing the living world: watch, notice, go there, understand, intervene (session 21, D-172)

## 1. The problem
The simulation is deep enough that **interesting things happen where the player is not looking**: a
fire at night in Zurug, a band of hungry men walking toward Krikeivorn, a household loading its boat,
a river coming over its banks at Little Leelee. Without help the player sees a quiet planet.

## 2. The loop
**WATCH** the planet at a watch speed -> **NOTICE** (the world news; the world's sound) -> **GO THERE**
(click the story: the camera flies to the place, close in, the right person or building selected) ->
**UNDERSTAND** (the story's lines, the inspector's whys, "Why?" - the chain of real causes ending at
NATURE, PEOPLE or YOU) -> **INTERVENE** (a power that writes physical state, `70_R7_GOD_POWERS.md`) ->
**WATCH THE CONSEQUENCES** (new stories, marked YOU at the end of their chain).

## 3. Screen
- Top left: the clock and speeds (as now). Top right: the planet's numbers (as now).
- **Left column, under the clock: World News** - up to five story cards, newest and most important first.
- Right: the inspector (as now). Bottom: the powers (R7).
The left column is free of the inspector, so news and inspection are seen together.

## 4. Stories, not events
The feed never shows raw simulation events. A **story** is an incident at a place that can grow while it
lasts and then closes. It reads only real state:
- **deeds** (`V.deeds`: burnt, fell, flood, wave, ash, snow, strike, quake, take, return, pay, refuse,
  wound, kill, death, help, lost ...) - new ones by a cursor (the list only grows);
- **events** (`W.E`: eruption, quake, marriage, birth, founding ...) - by a cursor;
- **live state** polled once a game hour per village: things burning (`s.burn`), flood water (`s.fl`),
  the sea drawing back (`s.wave`), the trapped (`s.trapped`), a band forming or on the road (`s.raid`,
  `trip.band`), households moving or founding (`hh.migrate`, `hh.found`), snow lying (`s.snow`).

### Levels
| Level | What | Examples |
|---|---|---|
| **BREAKING** (danger now, physically present) | fire, flood water, quake, sea wave, eruption, raid at the village, a killing, lightning on someone | "A house is burning in Zurug" -> "2 houses lost - contained after 6 hours" |
| **STORY** (a change in people's lives) | theft found out, claims and amends, a household leaving or arriving, a place founded, kin come to help, children taken in, a marriage across villages, relief sent, a fair, deaths of the young | "Three sheep were taken from Tekheim" -> "claimed back from Zurug: returned" |
| **SLOW** (pressure building) | hunger at the edge, a hard winter killing beasts, heat withering the corn, a thin harvest, herds lost to wolves | "Hunger in Krikeivorn: 3 households at the edge" |

### Aggregation
One story per incident: its key is (kind, village, the incident's own id - the quake deed, the raid, the
household, the season). Further deeds of the same incident become its lines ("2 houses lost", "1 death",
"3 families lodging"), its counts update in place, and it stays **active** while its live state lasts
(the fire burns, the water stands, the band is on the road). It closes after a quiet spell (12 game hours)
and leaves the feed after a while; the history keeps it.

### Importance and spam
Score = level weight (BREAKING 100, STORY 40, SLOW 20) + magnitude (deaths x 12, houses x 6, people
touched x 2 ...) + 25 if caused by YOU + recency. Per kind and village a cooldown; minimum magnitudes (a
tree struck and nothing burnt is not news; one death of old age is a line in the village's own story, not
a card). At most five cards; a BREAKING story always shows while active.

## 5. Click -> go there
Every story has a focus: the village, a point on its map (the fire, the water's edge nearest the houses,
the band, the household's house or the gate they left by), and an entity to select (the person who saw it,
the hurt, the leader of the band, the head of the household). Clicking a card flies the camera to the
point (close in), selects the entity (the inspector opens on it), and - for an active BREAKING story at a
speed above Watch - slows time to Watch so the player arrives while it is happening.

## 6. Why? - causes, never text
A story's chain is built from real links, newest first, up to its root:
- a deed's `answers` (a house fell <- the flood; a return <- the taking; help <- the burnt house);
- a deed's `event` and an event's parents (`W.E`): the flood <- the river's flow tag (`W.C.flow`, the
  hydrology's blame) <- the rain <- **YOUR deluge**;
- a household's decision's `cause` (S21: the heaviest harm it suffered lately, a deed): a household
  leaving <- its field drowned <- the flood <- ...;
- a taking's motive: hunger (the household's harm deed) or a grievance (the wrong it answers).
The root is **YOU** (an event whose actor is the player), **NATURE** (an event or deed of nature: weather,
quake, wolves, illness, age) or **PEOPLE** (a deed done by a person - and their own push may itself trace
further back). The card shows the root as a badge; "Why?" shows the links with their days ago.

## 7. Never theatre
A card is only ever a reading of state. "A raid is gathering" needs a real party (`s.raid` with members);
"Village under attack" needs raiders on its map; "A house is burning" needs a burning house; a migration
card needs people on the road. Sounds follow the same rule (`80_WORLD_AUDIO.md`).

## 8. Implementation
- `sim/news.lua`: the story engine (pure Lua, an observer: it reads W and never writes W.S; its own runtime
  state `W.newsrt`; deterministic for a given update sequence). `news.update(W, now)` incremental;
  `news.cards(W, n)` the top stories; `news.chain(W, story)` the links; `news.focus(W, story)`.
- `tools/v3_news.lua`: the feed over a headless run (with premises), to test aggregation and causes.
- `game/ui/newsfeed.lua` + the HUD: cards built in code like the inspector; click -> `controls`/camera +
  `selection`; "Why?" expands the chain under the card.
- Audio cues by level (`80_WORLD_AUDIO.md`).

## 9. Status (session 21)
Built (D-172, D-175): the story engine with disasters, conflict, pressure and everyday life; five cards under the
clock; click -> go there; Why? -> the chain; cues by level; bridge `news{n, go}`; `tools/v3_news.lua`; tests
`test_v3_news` x3. Not yet: a history list, relief asked and given / orphans taken in / harvests as stories,
per-village aggregation of everyday stories when the world is busy, tuning from play.
Session 22 (D-178): a story for every power cast (what it changed; rooted at YOU; repeated casts merged), a deed and
story for culled animals, lightning stories that name what was struck (bolt icon until something catches), no "the
fire is out" when nothing burned, lines as a woods fire grows, at most two everyday cards (`news.EVERYDAY_MAX`).
