# TC2-15 — Testing

Tests validate **mechanisms**, never scripted outcomes (TC1 D-204). Bad: "village A collapses in year 37".
Good: "with sustained crop failure and no other food, stocks fall by consumption and households seek
their own responses".

## 1. Where tests run

| Runner | Command | Contents |
|---|---|---|
| Headless (dotnet, seconds) | `node tc2/scripts/headless.mjs test` | The determinism lint first (no float arithmetic in TC2.Core, D2-057), then core unit, invariant, determinism, save/load, catch-up, scenario and long-run tests |
| Unity EditMode (batchmode) | `node tc2/scripts/unity.mjs test` | the same Core tests under Mono + Unity-specific tests |
| Web (Playwright) | `node tc2/scripts/webtest.mjs` | build loads, renders, no console errors, world hash equals headless, save/reload in IndexedDB, in-browser benchmark |

## 2. Kinds of tests

1. **Unit**: RNG distribution and keying, DMath accuracy and identities, calendar, queue ordering,
   cubed-sphere neighbours and areas, serialization round trips.
2. **Invariants** (also as debug audits during long runs): see `04` §8 - no past events, one live step
   per agent, conservation, travel index = rebuilt index, every door reachable, every child cared for.
3. **Determinism**: same seed -> same hash; slicing invariance; save/load equivalence at random event
   boundaries; catch-up equivalence; **cross-runtime** (dotnet vs Unity vs Web) checkpoints.
4. **Scenarios by premise**: a state the world could reach (a fire beside a road with a traveller on it;
   a failed harvest; an isolated island) and an assertion about the *mechanism* (the traveller perceived
   the fire and re-decided with it as the cause; stocks fell by consumption).
5. **Long runs**: 1, 10, 100, 1,000 years over several seeds: no NaN, no stuck agents, populations and
   fields inside plausible bands, event rates per century in range, ledger and save sizes in budget.
   Extinction and recovery cases are allowed outcomes, reported, not failed.
6. **Performance regression**: benchmark baselines with tolerances on deterministic counters.

## 3. Rules

- A failing simulation is never hidden by scripting the expected result; fix the mechanism or widen a
  statistical band with a DECISIONS entry.
- Every bug found in a long run gets a premise test of its mechanism.
- Tests share one world-building helper (`TestWorlds`) so premises are states the world could reach.
