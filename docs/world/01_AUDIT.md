# W01 — Audit of the simulation at commit 4e108b7

Written 27 Sep 2026 before the redesign (D-050). Baseline verified first: `npm run test:sim`
quick = 79/80 (the one failure is P07: the detectors grew +55% with last session's heatwave /
sea-ice detectors and were never re-baselined).

The question asked: *which parts are a functioning world, and which are simulation theatre?*

## What is genuinely a world (keep)

| Part | Why it is real | Keep as |
|---|---|---|
| `sim/rng.lua`, `grid.lua`, `fields.lua`, `calendar.lua`, `world.lua` command queue/log | counter-based keyed randomness, SoA fields, logged commands, deterministic replay | foundation (clock extended to hours) |
| Causality: `events.lua`, `blame.lua`, `ledger.lua`, detectors framework | every field change carries a dominant-driver tag; detectors re-tag; chains of 5–8 links already emerge | foundation, extended to stocks and entities |
| Save/replay (year snap + command replay) | bit-identical reloads (P02/P03) | foundation (hours instead of weeks) |
| Climate: temperature, moisture/advection/orography, ice, hydrology/floods, global carbon ledger | physically shaped couplings, rain shadows, deserts, floods and droughts emerge, tuned bands | planet layer, weekly |
| Vegetation, fertility, fire, biomes with hysteresis | logistic growth, succession timescales (grass 1–3 y, forest 30–60 y), fire spread with wind/fuel | planet layer, weekly |
| Ocean plankton/fish biomass, currents, oil | a biomass model is the right level for fish (Schaefer); oil is conserved | planet layer; fish is fished by real boats |
| Worldgen (plates, continents, regions, names, spin-up) | constraints hold on 20/20 seeds | keep; settle rewritten |
| Test harness, opcount-based perf gate, lint, tools | fast deterministic verification | keep; add behavioural tests |
| Renderer v2 (terrain/water shaders, model kit, prop chunks, movers with walk cycles, shadows, post) | a capable engine for a sim-driven view | keep; feed it real entities |

## What is theatre or too shallow (replace)

1. **Houses are a number.** `v.houses ≈ pop / 4`, "rebuilt one a month" for free — literally the
   *population > threshold → spawn house* pattern. No building exists, no wood is used, nobody
   builds, nobody lives in a particular house.
2. **Wood is not a resource.** Woodcutting is a CO₂ term and a negligible forest decrement
   (0.00001 per person-month). There is no fuel need, no timber, no construction, no stock.
3. **Jobs are labels.** A farmer has no field, a fisher no boat, a woodcutter no woodlot. Output is
   a village aggregate (`farmers × 30 person-months`, `fishers × 2.5/month`) regardless of time,
   distance, weather or who is actually working.
4. **Boats are decoration.** `W.ent.boats` is rebuilt monthly from a count and teleported over the
   fishing cells. The catch is computed in the village formula whether or not boats exist.
   Destroying a boat changes nothing except a small oil spill.
5. **Tankers have no economy.** They circle fixed lanes carrying "lamp oil" to nobody. Their only
   function is to be sunk.
6. **No trade, no transport.** Blight "trade" is a probability roll; migrants teleport between
   villages in the same tick; there is no cargo, no route, no voyage, no arrival.
7. **People are statistical rows.** Monthly Weyl-offset rolls for pairing, births and deaths;
   births scale with a village-level food ratio; pairing ignores housing; no household, no home.
8. **No days.** One tick is a week; ▶ runs two weeks per second. There is no day/night, no daily
   weather, no sleep, no working day. The sun follows the camera.
9. **Animals are invisible fields.** Grazers/predators are densities with a sound Holling-II
   model, but the view spawns random deer/wolf puppets near dense cells; there are no herds,
   breeding seasons, visible migrations or kills.
10. **The view invents behaviour.** `game/view/puppets.lua` sends villagers to random targets
    ("farmers to the fields while the local season has crops … the rest visit houses"). The
    behaviour is plausible-looking but not the sim's.

## Consequences for chains

Chains today can pass through fields (fire → crop → famine), but not through *things people do or
own*: there is no way for "the boat never came back" or "there was no wood for the new house" to
matter, because boats and houses do not matter. The player's interventions on people-scale things
(sinking a trade boat, killing the woodcutters, burning the woodlot) have no economic path.

## Verdict

Keep the planet layer, the causality system and the infrastructure. Replace villages, people,
ships/boats and the view's behaviour layer with a settlement/agent layer built on real stocks,
flows, labour time, buildings, vessels and households, running on an hourly clock with local
solar time. Then replace the fauna density-only model with herd/pack agents coupled to the same
fields. Details in `docs/world/10_…` onwards; decision D-050.
