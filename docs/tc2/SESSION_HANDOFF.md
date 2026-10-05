# TC2 Session handoff

## Session 1 (5 Oct 2026) — in progress

### Exact state
- Docs written: `docs/tc2/` 00-17, LEGACY_AUDIT, DECISIONS (D2-001..014), PROGRESS, this file,
  NEXT_SESSION_PROMPT.
- Unity project created (empty, built-in pipeline defaults) at `unity/TinyConsequences2`; `.gitignore`
  extended for Unity/TC2 outputs.
- Not yet: Core code, headless runner, scripts, URP setup, POC, benchmarks.

### Important paths
- Unity editor: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Unity.exe`
- Bundled dotnet: `C:/Program Files/Unity/Hub/Editor/6000.6.4f1/Editor/Data/DotNetSdk/dotnet.exe`
- Local Unity manuals: `.../Editor/Data/Documentation/en/Manual/` (authoritative for 6.6)
- Built-in package sources/docs: `.../Editor/Data/Resources/PackageManager/BuiltInPackages/`

### Pitfalls found so far
- Bash `cd` into the Unity project changes the session's primary directory display; use absolute paths.
- Unity docs HTML is large: read with the stripper in the scratchpad (`h2t.py`) or targeted regex.

### Next steps
1. `tc2/headless` project + test runner; Core foundation with tests.
2. Proof of concept in Core; headless runs, history dump, save/load equivalence, catch-up.
3. Unity: URP + project settings via editor script; Game host and views; batch tests; Web build;
   Playwright smoke + hash + screenshots.
4. Benchmarks; fix; commit; docs.
