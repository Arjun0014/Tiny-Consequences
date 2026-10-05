# TC2-12 — Saving, loading and offline progression

Decisions: D2-009 (save format and stores), D2-010 (offline progression).

## 1. What a save is

**The exact world state at an event boundary**, not a snapshot plus replay: clock, id counters, RNG
counters, every table, every field, the event queue with sequence numbers and versions, processes,
history ledger and registry, command log, configuration and world laws, plus metadata. Loading
reconstructs the World and rebuilds derived indexes; continuing must equal never having stopped
(**save/load equivalence test**, run at random event boundaries in every test pass).

## 2. Format (v1)

```
Header (uncompressed)
  magic "TC2W" | formatVersion u16 | flags u16 | simVersion u32 | seed u64
  worldTime i64 | savedRealUtcMs i64 | offline: mode u8, catchUpTarget i64, catchUpStartedUtcMs i64
  summary: year, population, settlements, polities, last headline (UTF-8, length-prefixed)
  compression u8 | payloadLength u32 | uncompressedLength u32 | payloadHash u64
Payload (compressed): sections [tag u32][version u16][length u32][bytes]...
  CONF config + world laws | CLCK clock, id counters | SCHD event queue | PLNT fields | GEOL plates
  PLAC places | PEOP persons, households | FAUN fauna | SOCI society | KNOW knowledge
  HIST ledger + registry | CMDS command log | HASH state hash of the world after load-time rebuild
```
- Explicit field-by-field binary writers (no reflection, no Unity serialization); little-endian.
- Unknown sections are skipped (forward compatibility); missing sections take defaults; each section has
  its own version and migration.
- The stored state hash is verified after load (detects corruption and version drift).
- Compression: Deflate if it works in the Web build (to be proven), else a built-in LZ codec.
- Size targets: < 5 MB for a 2,000-person world with a few centuries of history.

## 3. Stores

| Store | Where | Notes |
|---|---|---|
| `FileSaveStore` | editor, desktop, headless | `<root>/saves/<slot>.tc2`, write to temp then rename, keep `.bak` |
| `WebIndexedDbStore` | Web build | a `.jslib`: database `tiny-consequences-2`, object store `saves`; fixed keys independent of the page URL (Unity's `persistentDataPath` changes with the hosting path); `navigator.storage.persist()` requested; an in-memory fallback where IndexedDB is blocked (Safari iframes), with export/import of a save file |
| `WavedashRemoteStore` | Wavedash | later: Remote Storage upload/download of the same bytes after `Wavedash.SDK.Init()` |

Two slots (A/B) written alternately so a crash mid-write never loses the world; metadata names the newest
valid slot.

## 4. When to save

Autosave every 60 real seconds while playing (every 20 s at speeds >= 1 day/s), on the page becoming hidden
(`visibilitychange` -> JS handler writes the latest serialized bytes kept in JS memory, then Unity is asked
for a fresh one if it still runs), on quit in the editor/desktop, and periodically during catch-up.

## 5. Offline progression

On load, `elapsed = now_utc - savedRealUtcMs` (clamped to >= 0; capped). The player's **offline mode**
converts it to world time:

| Mode | Rate | Cap per return | Engine |
|---|---|---|---|
| Paused | 0 | - | none |
| Gentle | 1 world year / real hour (~24 / day) | 100 years | ACTIVITY |
| Living (default) | 4 years / hour (~96 / day) | 400 years | ACTIVITY (COARSE beyond its budget, later) |
| Ancient | 25 years / hour (~600 / day) | 3,000 years | COARSE (later) |
| Deep time | 200 years / hour (~4,800 / day) | 20,000 years | COARSE / planet-only eras (later) |

Flow:
1. Read the save; show "You were away 26 hours. In Living, 104 years will pass." with Live them
   (default, auto after a few seconds) / fewer / none.
2. Store the catch-up target and start time in the world's metadata (a closed tab resumes the *same*
   catch-up, it does not recompute from a new timestamp).
3. Run `AdvanceTo(target)` in frame slices with a large budget; agents are not drawn; the screen shows the
   world date, a slowly updating planet, and the historical events as they are recorded.
4. Save periodically during catch-up; on arrival save, then enter the present; the History Book opens on
   "While you were away".

Nothing is summarised or invented: the catch-up *is* the simulation. Equivalence test: catching up N
years in frame slices equals advancing N years in one call (identical hash).

## 6. Foundation scope (session 1)

Binary format v1, file store, Web IndexedDB store, save/load equivalence tests (headless and Unity),
offline elapsed -> target computation with modes and caps, a catch-up runner in headless and in the
Unity host, a debug option to simulate an absence ("pretend 26 hours passed").
