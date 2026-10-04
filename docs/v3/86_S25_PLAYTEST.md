# V3-86 — The submission session: Arjun's playtest as the authority (session 25, D-205-D-213)

Session 24's harness said every power worked; Arjun's own play said several still did not *feel* real. This session took his
playtest as the authority, reproduced each complaint in the real game (headless Chromium on the GPU, the game's own power path, its
own news and "Your actions"), fixed the cause and looked again. Scripts and shots in `out/s25/` (not committed).

## 1. What was visibly broken in the human playtest, and why

| Complaint | Cause found |
|---|---|
| A meteor = fire + quake; people react to the fires, not to the rock | the crater was 3 tiles on a 128-tile map; the strike cued a *quake* alarm, which the quake itself replaced a moment later - and a quake alarm out of doors makes nobody move |
| Wild animals sitting by the catastrophe | they bolted from the blast as from a bolt (0.35 units) and came back |
| Trees standing in fire for weeks; "trees + permanent orange flames" after a season | the village map's fire was out in hours, but a planet cell the map covers in part burnt a week (0.25 of its forest a tick), lit one map tree a day, and the view drew that cell's flames over the map's untouched trees |
| An animal inside the flames stands there | a beast lost 1.5 x heat of its health an hour: an hour to die in a fire |
| An island hit = some burning trees | the blast did nothing to the planet's cover (only fire and a cull of wild herds) |
| Oil: "I clicked and nothing happened" | the slick was drawn dark blue-purple at 3 x oil - deep water's own colour; thin oil not at all |
| Powers spammable | cooldowns of a few real seconds |
| Go there -> the village's centre | power stories carried the square; a flood with no wet building fell back to the square; an eruption went to a village |
| The skip menu under the news | both on the same GUI layer, the news built later; text layers draw over every box |
| Fullscreen button dead | it called `Module.toggleFullscreen`, which this Defold loader does not have |
| No menu, no settings, no tutorial, no guide | not built |
| (found playing) a click on a tree's crown struck the bare ground | the ray met the planet's sphere, not the map's drawn ground; a crown stands above its trunk |
| (found playing) a view crash whenever a boat sailed to a site | `trade` read as a global in `agents_view.update_travellers` |

## 2. What changed (see DECISIONS)

- **D-205** powers recharge in game time: lightning 2 h, air and rain 12 h, beasts, fields and oil 2 days, the sea 3, the mountain and
  the fault 4, **the meteor 7 days**; the bar shows the pie, the dimmed icon, "5d", "ready in 6 d 4 h (game time)".
- **D-206** the meteor: crater 0.2 (16 tiles), blast 0.62, flash 0.8; the strike is its own danger (`impact`), never replaced by the quake
  or the fires for its first half hour; everyone within 1.1 runs straight from it (babes snatched up) and stands looking back; beasts
  and wild animals bolt 3.5x as far; the planet's cells under the blast lose their woods and grass by the blast's share and are scarred;
  the crater's double rim, ejecta and rubble by its size; a dust column settling over 36 game hours.
- **D-207** fire ends: a beast in the flames dies in minutes (`fire_beast` 12); the planet's fire in v3 burns out in 2-3 days; a planet fire
  coming onto a map lights a front of trees there and its flames are the map's (never drawn over the map's trees); the fire's story says
  how long it burnt by when it went out.
- **D-208** Go there: the cast's point (kept on its root event), the crater, the flood's own water, the place of a death, the person on
  the move (followed), the eruption's mountain, a far cast's cell.
- **D-209** oil: the same `F.oil` drawn as a black glossy slick with ragged edges, a rainbow film and brown foam at the shore; a thin film
  shows (sqrt); 54 cells of oil.
- **D-210** title screen, pause menu (Esc / "Menu"), settings (volumes, sound, clouds, shadows, fullscreen, tutorial), credits; "How the
  World Works" in 8 illustrated pages; dropdowns above the news; fullscreen by the API with a pseudo-fullscreen fallback.
- **D-211** the opening tutorial (below). **D-212** aim at the drawn tree and the drawn ground.

## 3. Evidence in the real game (`out/s25/`)

| Case | At once | Later | News / Go there / Your actions |
|---|---|---|---|
| Meteor on Tekheim's square (`play_cat.mjs village`) | 11 of 31 dead, 20 hurt, 16 -> 3 buildings (every house), 291 fires, 14 running from it, 33 beasts dead | 6 h: 3 fires; a season: 0 fires, the crater and scorch, 8 camps, 2 houses rebuilt, 16 alive | "Fire from the sky at Tekheim" YOU; Go there -> the crater (0.013, -0.037); 41 consequences (the dead by name, the quake, hunger, children taken in) |
| Meteor on Hottoodingle Hop's woods (`forest`) | 209 of 404 trees thrown flat, 296 alight; wild animals within 0.5: 7 -> 2 | 2 h: 111 burning; a season: 0 burning, 240 snags, 116 trunks, 12 standing; wild near it 0 | "The fire is out after 29 hours" |
| Meteor on a one-cell island (`island`) | forest 0.86 -> 0.09, grass 0.50 -> 0.15, the ash biome; crater rim of rock | a season: forest 0.09, grass 0.25 | "A meteor fell far from any village" YOU |
| Oil off Zurug (`oil`) | 11 units over 11 sea cells, the bay black | 2 d: fish 7.98 -> 6.52; 8 d: 0.87, the slick along the shore | "Oil on the coast at Zurug - 10 gulls oiled", "Fewer fish off Zurug - 24%"; Go there -> the fouled shore |
| Flood at Little Leelee (`probe_goto.lua`) | | | Go there -> (-0.29, 0.45), 0.55 m of water (was the square) |
| Fire lifecycle (`probe_fire.lua`) | | map fires out by 16 h; the planet's by 48 h (was 192) | |
| Cooldowns (`ui_check2.mjs`) | a second meteor: "Meteor is recharging: ready in 7 d" | | |
| Fullscreen (`fs_check.mjs`) | entered and left by the button (state shown); refused -> pseudo-fullscreen, left again | | |

## 4. The tutorial (`game/ui/tutorial.lua`)

Shown after Play the first time (skippable at every step, replayable from the menus). A coach card at the top; a gold ring or glow on
what to use; each step waits for the player to do it:
0. "This tiny world lives without you" - 1. look (drag/zoom, or "Take me there") - 2. inspect someone - 3. choose Lightning (ringed) -
4. strike the ringed tree at the edge of a wood by the village (the ground there is dry: `tutorial_dry`, a logged command that sets
the soil water only) - 5. "The world noticed": the bolt's own news story glows; click it (another story: "that was another story");
if nothing caught: "the world decides that, not you" and "Strike another tree" - 6. Go there and watch (12 s) - 7. Your actions:
"You caused one thing. Everything after it belongs to the simulation" - 8. skip to dawn / a day - 9. skip a season - 10. "Your one
lightning bolt now has N consequences" with its first real branches (`sim/chain.rows`).

## 5. Known gaps

- The tutorial's strike step relies on the player clicking near the ring; the bolt still takes what is at the point (the world decides).
- Dust puffs close in read as soft hexagons (the smoke mesh).
- No village sits on a small island in the shipped world: an island hit is the planet's cells only (no people there to flee).

## 6. The second playtest (later the same night, D-214-D-219)

Arjun played the submitted build and found: the guide's text running under its buttons (and a page of seven boxes off the
panel); step 5 of the tutorial unclear; the skip menu overlapping "Your actions" and the tutorial's label; the game opening close
in instead of on the whole planet, and skips with no sense of time passing; "Your actions" not feeling like a chain reaction; the
tutorial's chain (a bolt, a tree, a few trees) a poor showcase; oil with no visible substance; the meteor with no crater seen and a
poor animation; blight with no physical effect; time controls not taught; a file path in the credits.

What changed: the guide re-laid by measure (D-219); the game opens on the whole planet and a skip rises, turns the planet through
its days and nights and comes back down (D-214); the skip menu opens at the left, "Your actions" stepping aside, the tutorial's
labels beside the rows; the tutorial rebuilt around a meteor at the edge of the woods beside the village, with the speeds taught and
two skips (D-215) - in the real game: the strike killed 7, threw down 3 houses and 2 other buildings, set 362 things alight, laid 168
trees flat, 12 people ran; the chain 21 consequences at once, 25 a day on, 30 a season on; the meteor's fall, shock and its mark on the
planet seen from every height (D-216); oil as black glossy blobs drifting on the current, and the spill bursting out (D-217); blight
taking a third of a field at once and rotting it in days (D-218); "Your actions" with the chain's size, icons by kind and new links
glowing (D-219); credits written for players.

