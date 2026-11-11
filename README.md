# Tiny Consequences

A tiny planet that lives on its own. You are its god: poke it, speed up time, and watch one small act ripple
through years, centuries and millennia. Every big change can be traced back through a chain of causes to
**Nature** or to **You**.

**This repo holds two versions of the game.** Pick the one you want:

| | **Version 1: Tiny Consequences** | **Version 2: Tiny Consequences 2.0** (shipped as *A Tiny World*) |
|---|---|---|
| Engine | [Defold](https://defold.com) 1.13.1, Lua | Unity 6.6 (`6000.6.4f1`), C# |
| What it is | A small living world with individual villagers; made for the Defold Game Jam 2026 (theme: *Chain Reaction*) | A full rewrite: an autonomous medieval civilisation inside a living miniature planet, with a History Book and offline progress |
| Status | Legacy. Kept as a reference, no longer developed | Active |
| Code | `game/`, `sim/`, `content/`, `assets/`, `tests/`, `tools/`, `scripts/` | `unity/TinyConsequences2/`, `tc2/` |
| Design docs | `docs/` (start at `docs/01_VISION.md`) | `docs/tc2/` (start at `docs/tc2/00_VISION.md`) |
| Runs in | A browser (HTML5) | A browser (WebGL / WebGPU), Windows, phones in landscape |

## The idea (both versions)

- **The world runs itself.** Weather, oceans, forests, animals, farms and villagers all live their own lives.
- **You only nudge it.** Lightning, drought, a meteor, a volcano. The world never exists to react to you.
- **Everything has a cause.** Pick any event and see the chain that led to it.
- **The simulation is deterministic.** The same seed always gives the same world, and the simulation code runs
  headless (no graphics), so it can be tested automatically.

---

## Version 1: Tiny Consequences (Defold)

Needs **Node.js 20+**, **Java 21+** and **LuaJIT**. The Defold editor is optional.

```bash
npm install
npm run setup    # checks your tools and downloads the pinned Defold build tool (bob)
npm run play     # builds the HTML5 game and opens it at http://localhost:8080
```

Other useful commands:

```bash
npm run sim -- --seed 42 --years 100 --map   # run the simulation headless and print the map
npm run test:sim                             # simulation tests and the Lua linter
npm run test:web                             # browser tests (Playwright)
npm run build                                # debug build
```

How it is built: the simulation in `sim/` is plain Lua 5.1 with no Defold APIs, so it runs under LuaJIT on the
command line and inside the HTML5 build with identical results. `game/` is the Defold side (rendering, UI, audio).
Progress notes are in `docs/PROGRESS.md`.

---

## Version 2: Tiny Consequences 2.0 / *A Tiny World* (Unity)

Needs **Node.js 20+** and **Unity Editor 6000.6.4f1** (install it with Unity Hub). Open
`unity/TinyConsequences2` in Unity to work on it in the editor, or use the scripts below.

```bash
npm install
node tc2/scripts/headless.mjs test     # simulation tests, no editor window, runs in seconds
node tc2/scripts/unity.mjs build-web   # Unity batch-mode WebGL build into tc2/web-build/web
node tc2/scripts/serve.mjs             # serve that build at http://localhost:8642
```

Things to know:

- The scripts look for Unity at the default Windows Unity Hub path. Set `TC2_UNITY` (path to `Unity.exe`) and
  `TC2_DOTNET` (path to a .NET 8 `dotnet`) if yours is elsewhere.
- Scripts that draw (Unity batch mode, browser tests) include a guard written for one maintainer's machine: they
  refuse to start while an AMD Radeon RX 5600M is present, because it crashes that PC. If that is not your
  hardware, set `TC2_ALLOW_DGPU=1`.
- Layout: `unity/TinyConsequences2/Assets/TC2/Core` is the deterministic simulation (plain C#, no Unity APIs) and
  `Game` is rendering and UI. `tc2/headless` compiles the same Core code as a .NET console app for tests and
  benchmarks. `tc2/scripts` holds the Node tooling. Start with `docs/tc2/03_ARCHITECTURE.md` and
  `docs/tc2/15_TESTING.md`.

---

## Repo map

```
game/  sim/  content/  tests/  tools/  scripts/  assets/   Version 1 (Defold, Lua)
unity/TinyConsequences2/  tc2/                              Version 2 (Unity, C#)
docs/                                                       Design docs: docs/ is V1, docs/tc2/ is V2
```

## Built with an AI agent

Both versions were built by one person directing [Claude Code](https://claude.com/claude-code). The files
`CLAUDE.md`, `NEXT_SESSION_PROMPT.md`, `docs/PROGRESS.md`, `docs/SESSION_HANDOFF.md`, `docs/DECISIONS.md` and their
`docs/tc2/` counterparts are the agent's working notes and memory between sessions. They are long, but they are also
the most detailed record of why things are the way they are.

## Credits and licence

The code is released under the [MIT licence](LICENSE). Third-party assets keep their own licences: the CC0
3D models by Quaternius, the SIL Open Font Licence fonts, and so on. The full lists are in
[`assets/CREDITS.md`](assets/CREDITS.md) (Version 1) and
[`unity/TinyConsequences2/Assets/TC2/CREDITS.md`](unity/TinyConsequences2/Assets/TC2/CREDITS.md) (Version 2).

The world's sound effects were generated with ElevenLabs and are subject to its terms (see the credits files).
