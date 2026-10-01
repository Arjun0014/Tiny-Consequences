# Session handoff — session 13 end (2 Oct 2026): a real economy, sea trade, dogs, oxen, floods

`docs/PROGRESS.md` (session 13 entry) and **D-100 … D-109** in `docs/DECISIONS.md` are the source
of truth. Arjun's rules: no subagents; the living world first; one heavy process at a time (shared
laptop); visible action = simulation; fix mechanisms, not appearances; no forced trade; hand off near
~900K context.

## State
- **main** holds everything. A normal launch opens the v3 world (`assets/worlds/v3.tcsave`, **regenerated
  this session** for founding oxen: `luajit tools/make_world.lua --v3 --seed 22 --settlements 6 --out
  assets/worlds/v3.tcsave`, state hash 1202821907): seed 22, 6 settlements, 145 people. Tekheim alone in
  the north (its water does not reach the southern sea); Zurug, Moomoo, Hottoodingle Hop (no dock) on
  one southern landmass; Hunuvale and Upper Linamist on another. `?world=v2` legacy.

## What became real this session
- **Sowing to need** (D-100, `settle.plan_sowing`): the grain the households fetch to eat is measured
  and decides the rows sown (a year and a half of bread less store and standing crop; best soil first;
  the rest of the fields lie idle and their owners know why). Grain stores no longer rise year on year.
- **The balanced pot and diet** (D-101): perishables, then a relish share (`diet_p` 0.35) of smoked
  fish/cheese, bread for the rest; a household's diet; bread-only or relish-only for weeks weakens.
  Households fetch bread even with fish at home.
- **Worth, news and exchange** (D-101, `sim/agents/econ.lua`): worth = base x clamp(1.6 - have/want);
  food and logs are the village's, wool, cloth, hides, leather a household's; villages know each
  other only from people who travelled (`s.know`, fading over 48 days); each morning the best exchange
  (`econ.best_trade`: the good to bring back is the one the trader's side wants most against what the
  other lets go for; geometric-middle price; worth the trip's days); on arrival the deals are worked
  out from what is there (`econ.deals`), goods carried in a pack door to door (`WORK.deal`,
  `PICK.owed`). Relations `s.rel[o]` (trust, deals, gifts, debts). Gifts within a village
  (`econ.plan_gift`, `share`). Fishing answers what fish is worth (`s.fish_worth`).
- **Sea trade** (D-102): sea ways over ocean cells (`trade.sea_route`), sea gates at the map's edge,
  a fisher skipper and a mate, cargo carried to the boat a load at a time, a `voyage` step drawn on
  the planet, landing at the other dock (`boat.away`), deals while the mate minds the boat, home,
  the hold carried home; storms keep boats in; a wreck drowns both and loses the cargo.
- **Relief** (D-103): a hungry village with nothing to sell asks the neighbour it trusts most; help
  given by trust and debt, carried home to the granary, remembered both ways.
- **Leather** (D-104): hides from every slaughter and hunt, tanning pits by water (10 days), leather,
  shoes that wear (barefoot is slower and cold), hides and leather traded and shared.
- **Wolves and dogs** (D-105): dogs and wolves built from the Pug rig (`build_animals.py --only dog`:
  scale applied with the clips; snout, tail); a dog per herding household (with the herd by day, at
  the pen by night, `sim/agents/dogs.lua`); a wolf near a pen sets the dog barking, the household
  wakes and drives the pack off; the night watch is a sleeper the wolves can wake, not a bar.
- **Floods** (D-106): the river against its running-mean width; floods wear and sweep bridges away;
  the ways change at once; rebuilt after the water falls.
- **River water** (D-107): dirt from houses and pens by the banks; the flux from foul water; a
  household that fell sick keeps to the well (dormant in the shipped world: wells are near).
- **Oxen** (D-108): gelded bullocks (founders bring one with its harness), yoked and walking ahead
  of the ploughman, rows 2.5x faster, leather harness.
- **Children** (D-109): by age band 2-5 play 70%; 6-8 play 55%, light work 11%; 9-12 play 28%, work 48%.

## Observed (shipped world, before the regeneration, two game years; see PROGRESS)
- 31-53 exchanges, all walked or sailed: Moomoo <-> Upper Linamist by sea (fish, grain, logs, cloth,
  both ways as their stocks swing), Hottoodingle Hop <-> Zurug by road (wool, cloth, leather, hides
  for fish and cheese); 12-63 gifts a village; no deaths from hunger; diets 0.37-0.48 relish.
- Wolves: 1-54 barks a village a year, 1-4 packs driven off, 0-3 dogs killed, 0-5 beasts lost.
- Oxen (regenerated world, one year): 134 ox-hours at Tekheim, 40-43 at Zurug and Hottoodingle Hop.

## Performance (release HTML5)
- After D-101/D-102: **a 12-day season 8.9 s** (envelope 31.1 ms/h) with
  `node scripts/prof_web.mjs --release --hours 288 --runs 2`.
- **At the end: 10.4-13.1 s** (envelope 36.0-45.6 ms/h; this laptop was loaded, single runs 39-56
  ms/h). The 6-hour windows after the first day are as before (25-41 ms/h, i.e. ~9-10 s a season);
  the first game day costs 90-200 ms/h: the start-up of gifts, trades and knowledge with their new
  paths (every household plans a gift on the first morning; first trips; dogs created). Timers (96 h,
  timers on): think.decide ~21-35 ms/h (172-279 us a call, up from 55: the new candidates), nav.path
  18-30 (210-315 us a call), act.start 19, wild.step 9, stock.step 8, settle.daily 3 (12 ms a call),
  dogs.step 7.5 -> 1.2 after the cut (wolves counted hourly, dogs woken by the wolves, longer steps).
- **Session 14, first**: bring the season back under 10 s - profile decide's candidates (leather,
  share, trade, children's gathering, eat-before-sleep) and the first-day start-up (spread the first
  morning's gifts and trades; cache `econ.village_stock` within a decision; cheaper `M.market`).

## Tools added
- `luajit tools/v3_econ.lua [--load F] [--days 96] [--every 12] [--out out/econ] [--sett K]`: every
  settlement's stocks (grain, food, relish, cheese, wool, fibre, yarn, cloth, clothes, hides,
  leather, logs, herds, granary extras), grain use/target/rows wanted, diet; the trades as they end;
  deaths with causes; the whole run's accounts (deals, gifts, sea hours, wrecks, barks, ox hours ...).
- `luajit tools/v3_kids.lua`: children's days by age band.
- Scenario scripts (not in the repo) were used to force a failed harvest, a starving village, a
  flood and a far well; the tests `trade_answers_real_want` and `hungry_village_asks_for_help` keep
  the first two.

## Next (in order)
0. **Performance**: the season is 10.4-13.1 s (target <= 10 s); see Performance above.
1. **Watch the new world** in the browser: sea voyages (Moomoo-Upper Linamist), visiting boats at a
   dock, oxen ploughing, dogs with flocks, wolves met at a pen (shots_look `--what dock|flock|wolves`).
2. **R4 left**: livestock traded alive (a drover leading beasts; culls offered to neighbours short of
   animals); help sought by sea; relations shaping marriages and migration; larger trading boats.
3. **R5 begin**: household migration/founding from real push and pull (land, hunger, crowding,
   relatives or trade partners elsewhere), travelling physically, founding through agents.
4. Rivers: repairs of damaged bridges, river boats if a reason appears.
5. Keep the season <= 10 s; candidates if it grows: dogs' and wild steps off-screen, cached deal
   computations.

## Findings and pitfalls (session 13)
- **Trade without want is fake**: test trade by creating the want (a failed harvest, a starving
  village), never by expecting trips in the shipped world.
- **Long steps starve people** unless hunger is integrated hour by hour (road legs, voyages, minding a
  boat) — and a trip whose last drop has empty hands must still end (`act.end_trip`).
- A daytime "exhausted" sleep used to last until the next morning: now `nap_h`.
- `pairs()` over goods: sum with `econ.ORDER` (float order differs between LuaJIT and the HTML5 Lua).
- bob rejects require cycles (econ -> trade -> wild -> stock -> settle): inject (`settle.econ`,
  `dogs.wild`, `wild.dogs`, `act.drown_hook`, `act.stock_push`, `dogs.interrupt_hook`).
- `addw` takes at most seven format arguments.
- Blender 5 actions are layered (layers/strips/channel bags); `transform_apply` fails headless: use
  `Armature.transform` and scale the location keys.
- A decision that runs once a day must also run when the window opens (sowing: at each local midnight
  and after the day's field changes), and undecided fields must default to "wait".
- Heredocs with apostrophes break here: write patch scripts and doc fragments with the Write tool.

## Known oddities
- Tekheim's sea does not reach the others: it trades with nobody (alone on its landmass).
- Wells are near every house in the shipped world, so nobody drinks river water (the flux is dormant).
- Moomoo's bridges stand on a stream that does not flood; floods elsewhere meet no bridges.
- Dogs and wolves come from the pug: short legs (the legs were not lengthened).
- The ploughman holds a hoe (no plough model).
