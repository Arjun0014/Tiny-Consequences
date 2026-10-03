# V3-50 — The primitive properties shared by R6 (conflict) and R6.5 (living planet)

Session 20 (3 Oct 2026), D-148. Research: `40_RESEARCH_CONFLICT_PLANET.md`. Audit:
`41_PLANET_AUDIT.md`. R6 plan: `60_R6_CONFLICT.md`. R6.5 plan: `65_R6_5_LIVING_PLANET.md`.

**The question**: what small set of inherent properties lets the existing world react naturally to
theft, raids, lightning, fire, flood, snow, quakes and eruptions - with very little code per
hazard and none per outcome?

**The answer in one line**: things have *places, owners and condition*; dangers are *physical
things at places*; people *notice* what reaches their senses, *weigh* what to do with what they
have and whom they love, *can be hurt*, and *remember specific deeds* that travel only with
people. Everything else - theft, feud, rescue, evacuation, orphan care, rebuilding, migration - is
the existing economy, family and travel reading those properties.

## 0. The primitive test
For each property: (1) meaningful in the world on its own; (2) makes sense if the test scenario
never happens; (3) used by at least two systems; (4) local state/behaviour, not an outcome;
(5) different combinations give different results; (6) inspectable ("why?"); (7) removing an input
changes the result rather than breaking a script. The table in §9 applies it.

## 1. What already exists and is reused (no new state)
| Primitive | Where | Used for in R6 / R6.5 |
|---|---|---|
| **Ownership** of houses, larders, boats, beasts, fields, iron, querns; village stores | `people.lua` households, `stock.lua` `a.owner`, `econ.lua` pools | what can be taken, lost, inherited, returned |
| **Need** (a household's larder and share of the granary against its mouths) | `migrate.push`, `econ.view` | the pressure to take, to ask, to leave |
| **Worth** (a good is worth what its household lacks) | `econ.value`, `econ.worth` | the gain of taking; compensation; terms of trade after shocks |
| **Knowledge of places and other villages** from real contact | `econ.learn` (snapshot), `s.lore`, `econ.on_learn` hook | whom one could rob; news of deeds |
| **Trust and debts between villages/households** | `econ.rel`, `econ.debts` | restraint; damaged by harm, repaired by settlement |
| **Kin** (parents, children, siblings, spouses) across villages | `people.is_kin`, `kin.lua`, D-120/D-139 | cross-cutting ties restrain; kin avenge, shelter, take in orphans |
| **Affinity** between persons | `people.bond` / `rel_v` | who joins whom; who helps whom |
| **Physical travel** by road and sea, carrying, droving | `think.trip_legs`, `trade.road`, `stock.halter/led_by/leave_map/join_map` | thieves lead beasts away; claimants walk; news walks |
| **Interrupts** (cut a step, re-decide with a reason) | `agents/init.lua sys.interrupt`, `act.interrupt_hook` | noticing danger |
| **Dogs and night watches** | `dogs.lua alarm`, `think.guard_herd`, `drive_off` | defenders woken; guarding after losses |
| **Animal flight with alarm spreading** | `wild.threat`, `wild.alarm` | animals fleeing fire, raiders, floods |
| **Health** 0..1 with a day at the edge before death | `people`, `life.daily` (D-147) | injury sits beside it |
| **Building condition and repair** | houses weather (`footing_wear`), `repair_candidates`, bridges `b.cond` swept by floods | damage by fire, quake, raid |
| **Families after a death** | widowhood, `life.take_in` (orphans), `settle.estates` (heirs), mentors replaced | sudden-death aftermath needs no new controller |
| **Planet fields read by agents** | `F.temp`, `F.soil_water`, `F.precip`, `F.fish`, `F.forest` | climate shocks already reach crops, pasture, fish |

## 2. New primitive A — **Deeds**: what happened, by whom, to whom, known by whom
A deed is a small record of a significant act or event: `{ id, kind, t, at (map), x, y, by (person
or 0 = nature/unknown), by_hh, by_s, to (person), to_hh, to_s, what, n, fu (its worth to the one who
suffered or gained it), harm (true) or help (false), answers (the deed it answered), settled_by }`.
Kinds grow with use: `take` (a theft), `took_back`, `return`, `pay` (compensation), `refuse`,
`wound`, `kill`, `rescue`, `relief`, `ferry`, `death` (by nature: lightning, fire, flood ...),
`burnt`, `lost` (a house, a store, a bridge).

**Knowing a deed** is per village and per witness: `s.deeds[id] = { t, by, by_s, how }` - what that
village believes (the doer known by name, by village, or not at all) and how it came to know
(saw it, found it, tracks, told by someone). Witnesses are listed on the deed (`d.saw`). A village
learns a deed when one of its people saw it or found it (at once) or when people who know it meet
its people (the existing `econ.on_learn` contact hook: trade, fairs, visits, migrants, brides).

**Read, never stored as a score**: grievance (household or village -> village), gratitude,
reputation ("Zurug men take cattle"), fear of a place, blame for a chain, and the inspector's why
are computed from the deeds a village knows, by worth, closeness of the victim (own household >
kin > neighbours: social substitutability, A4), age (half-lives: goods ~2 local years, blood ~10)
and whether settled. A cached number may exist for speed; it is a sum of listed deeds.

Passes the test: theft, raids, rescues, relief, lightning deaths, fires and floods all write
deeds; grievance, trade trust, claims, guarding, migration, chronicles and blame read them.

## 3. New primitive B — **Danger, noticed**
A danger is something physical at a place that can hurt what is near it: a wolf on the hunt, a
stranger taking beasts, a man with a weapon, a burning house or tree, water over the ground, a
lightning stroke, shaking ground, falling ash. Each kind belongs to its own system (wild, conflict,
fire, flood ...), which knows where it is and how it harms; the **shared** part is how it is
noticed and answered.

- **Cues** (`danger.cue`): a sight (within sight range: shorter at night, blocked indoors), a sound
  (within hearing range by loudness: barking, shouting, bleating beasts, thunder, a crash, the
  roar of fire), a smell (smoke). Sleepers wake to sounds only within a part of the range; indoors
  halves hearing; children and elders hear the same but answer differently.
- **Noticing** sets `p.alarm = { kind, x, y, t, src, why }` and interrupts the person after their
  own reaction delay with the reason ("The dogs are barking at the fold").
- **Social cues**: people running, shouting or carrying water are themselves cues; news ("the
  wolves are about", "Zurug men took our ewes") is a deed or a danger heard of, not a cue.
- Animals have the same: `wild.threat` (people, wolves, running dogs) is extended by danger kinds
  (fire, water) - the alarm already spreads through a band one by one.

## 4. New primitive C — **Urgent choice** (interrupt objective)
When `p.alarm` is fresh, `think.decide` weighs protective actions before ordinary work
(the PADM, B1; attachment, B1):
| Action | For | Weighed by |
|---|---|---|
| **gather** dependants | a parent whose babe/child is elsewhere; a child goes home or to a parent | attachment first: where my children are; separation is the strongest pull |
| **flee / keep away** | anyone from fire, water, a raider, a wolf too close | how near and how fast it comes; own health and hurt; carrying a babe |
| **shelter** | storm, hail, ash, lightning in the open | a building near that protects from *this* kind |
| **confront / drive off** | the able and bold, at their own beasts, house or kin | boldness, stake (own beasts, own house, kin there), allies close, arms, the danger's strength |
| **fight fire / bail / dig** | the able near a burning, flooding or fallen house | water at hand, numbers, own house or kin's |
| **rescue** | anyone hurt, trapped or stranded who is kin, friend or near | closeness, boldness, the danger in the way |
| **save** beasts, stores, the boat | owners | worth to the household, time, danger |
| **watch** | an ongoing threat (a pack about, raiders known, a rising river) | remembered losses, the dark |
This replaces the special cases one by one (`drive_off` for wolves is the first "confront",
`shelter` for a downpour the first "shelter").

## 5. New primitive D — **Injury**
`p.hurt` (0..1), `p.hurt_kind` (wound, burn, broken, bruise), `p.hurt_t`, `p.hurt_deed`.
- Walking slower, working slower (`1 - hurt`), no journeys above 0.3, kept to bed above 0.6.
- Heals each day (faster fed and tended by someone of the household at home: the tender gives
  hours of work); a wound can fester untended; above 0.5 a daily chance of death ~ hurt^2.
- Death by injury is a death with a deed (who or what did it).
Sources: blows (R6), lightning, burns, falls and falling walls (quake), animals (a boar, a wolf
at a hunter), the sea. Care is ordinary household time; the lost work is ordinary labour shortage.

## 6. New primitive E — **Condition and contents of things**
Buildings, bridges, boats, fields and stores already have state; hazards act on it physically:
fire consumes thatch, timber, the grain and goods inside; water ruins what lies low; shaking
cracks walls by construction (stone footings and log walls hold better than rubble); thieves carry
contents away. Below a threshold a building is a **ruin** (a site to clear and rebuild: logs,
labour, existing house planning). No "disaster damage" number: what was there is gone or not.

## 7. New primitive F — **Boldness** (risk tolerance) and **arms**
- `bold` (0..1), a trait derived from each person's key (no save change), alongside chronotype,
  pace, diligence, sociability and patience. Read by: taking things, confronting, chasing,
  rescuing into danger, sailing in doubtful weather, founding far. Men and women differ only by
  what the world asks of them (strength and carrying), not by a sex factor on boldness.
- **Arms** are things a household owns: the axe and knife it has (better with iron edges,
  `hh.iron`), a hunting spear, later a shield. Carried only when danger is known (on a watch, on a
  raid, a claim to a hostile village). Made by existing craft and the forge; no generic spawning.

## 8. Planet fields read on the maps (R6.5)
Each a reading of an existing planet field, never a new disaster number: **snow depth**
(`F.ice` and snowfall when cold: walking, forage reach, hay, roofs), **cold/heat**
(`F.temp`, `heat_anom`: health, fuel, crops' frost and heat), **dryness** (soil water and days since
rain: fire), **wind** (the season's wind table: fire spread, sailing), **water** (`F.flow`: rivers
already re-laid daily; water over low tiles), **ash** (a deposition the eruption writes into
`F.fertility`/grass and onto maps), **shaking** (a quake's intensity at a place, an event).

## 9. The test applied
| Property | 1 meaningful | 2 without the scenario | 3 two+ systems | 4 local | 5 combos differ | 6 why | 7 graceful |
|---|---|---|---|---|---|---|---|
| Deeds | a thing happened | yes (relief, rescue, deaths) | grievance, trust, claims, guarding, chronicle, blame | per village knowledge | settled/unsettled, kin/not, old/new | the deed itself | no deed known -> no grievance |
| Danger noticed | senses | wolves, storms already | wild, dogs, people, birds | distance, sleep, indoors | night vs day, indoors | "the dogs were barking" | nobody near -> nobody reacts |
| Urgent choice | protective action | yes | fire, raid, flood, wolves, storm | own state | children away vs home | the choice's reason | no danger -> ordinary day |
| Injury | bodies break | hunters, falls | combat, lightning, fire, quake, animals | one person | fed/tended/alone | "hurt in the fight at the fold" | no hurt -> nothing |
| Condition | things wear | weathering already | raid, fire, flood, quake, storm | one building | stone vs log, full vs empty | "burnt in the fire" | intact -> nothing |
| Boldness / arms | temperament, tools | founding, sailing | theft, defence, rescue, chase | one person | timid man, bold widow | "bold enough to try" | 0.5 default |

## 10. What NOT to build
- No war flag, hostility score, morale meter, panic state, disaster severity number, grief debuff,
  per-hazard fear (`raid_fear`, `fire_fear`), evacuation order, aftermath controller, scripted
  retaliation, "raid count >= 2 -> palisade", captives as counters, teleported loot, groups with one
  position, conflicts resolved off the map.
- No outcome is written by a hazard: lightning makes heat and a hit at a point; who dies, who
  burns, who takes the children, who rebuilds and who leaves is the people's.

## 11. Determinism and cost
- Every choice by `rng` streams keyed by person/deed/time; deeds are a list (ids ascending), village
  knowledge iterated by sorted ids, never `pairs()` order; text kept in state via `sfmt`; no `^`.
- Deeds and dangers are rare: costs are paid when something happens, not every tick. Theft and
  conflict candidates are evaluated only for households under real need or holding a grievance
  (a cached per-household value refreshed daily). Perception scans the members of one map when a
  cue happens (tens of people), never every hour.
- New modules must respect bob's rule: `deeds.lua` and `danger.lua` require only leaf modules
  (people, config, rng, fmt, clock, dmath); the interrupt is injected (`danger.interrupt`);
  `conflict.lua` is required by think and act and requires neither.
