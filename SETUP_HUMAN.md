# Human setup (Arjun) — do this once, ~30–45 minutes

Everything else is done by Claude Code. These are the steps it can't do for you.

## 1. Install tools

You need: **Git**, **Node.js 20+**, **Java JDK 21+**, **LuaJIT**, **Google Chrome** (for playtesting), **Claude Code** (latest), and optionally the **Defold editor** (handy for poking at the project; use the same version Claude Code pins in `tools/defold-version.json`).

**macOS (Homebrew)**
```
brew install git node luajit
brew install --cask temurin          # any JDK 21 or newer is fine
```

**Ubuntu / Debian / WSL2**
```
sudo apt update && sudo apt install -y git luajit openjdk-21-jdk
# Node 20+ via nvm:
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/master/install.sh | bash
nvm install --lts
```
Playwright's browser needs system libraries. Install them now (it asks for your sudo password), so Claude Code isn't blocked in Stage 1.1:
```
npx playwright install-deps chromium
```
Optional but useful: `sudo apt install -y lua5.1 lua-bitop` lets the tests also run under plain Lua 5.1, the same language version the browser build uses.

**Windows**: the smoothest path is **WSL2 with Ubuntu** and doing everything inside it (use the Ubuntu commands above). Your Windows browser can open `http://localhost:8080` served from WSL. Native Windows also works if you install Git, Node (`winget install OpenJS.NodeJS.LTS`), a JDK 21+ (`winget install EclipseAdoptium.Temurin.21.JDK`) and a LuaJIT build (luajit.org/install.html), but LuaJIT is the fiddly part.

Check:
```
git --version && node --version && java -version && luajit -v
```

## 2. Create the project folder

1. Make an empty folder `tiny-consequences` and copy in everything from this package (`CLAUDE.md`, `README.md`, `SETUP_HUMAN.md`, `STARTER_PROMPT.md`, `HUMAN_INPUTS.md`, `.claude/`, `docs/`).
2. Fill in what you can in `HUMAN_INPUTS.md` (at least your OS).
3. `git init && git add -A && git commit -m "Design docs"`.
4. Optional but recommended: create a **private** GitHub repo and push, so work is backed up.

## 3. Start Claude Code

From inside the folder:

- **Recommended — auto mode**, if your plan has it: `claude --permission-mode auto`. A safety classifier approves actions, so it can work for hours without stopping.
- Otherwise: `claude`. The project settings in `.claude/settings.json` auto-accept file edits and pre-approve the common build/test/git commands. Accept the **workspace trust** prompt so those rules apply. It will still occasionally stop to ask about a command outside that list; answer "Yes, and don't ask again" for anything routine, so it doesn't ask twice.

Then paste the **Initial prompt** from `STARTER_PROMPT.md`.

While it works you don't need to do anything. If it hits something only you can do, it writes exact instructions under "Blockers" in `docs/PROGRESS.md` and tells you.

If a session ends or runs out of context, start a new one and paste the **Resume prompt**.

## 4. Playtesting (end of each chapter)

1. `npm run play` → opens the game in your browser.
2. Read `docs/playtests/chapter-N.md` for what to try and the questions.
3. Write your notes in `docs/playtests/chapter-N-feedback.md` (format in `docs/playtests/README.md`; bullet points are fine).
4. Paste the **After-playtest prompt** from `STARTER_PROMPT.md`.

Planned playtests: end of 29 Sep, 1 Oct, 2 Oct, and the final check on 4 Oct. After playtest 3, the After-playtest prompt runs all of Chapter 4. Use the Final-day prompt only if a session dies during the release stage.

## 5. Accounts (do before 3 Oct)

- **itch.io**: make sure you've clicked **Join jam** on the Defold Game Jam 2026 page. You'll upload the zip and submit there.
- **Wavedash**: create a developer account, create a game, and put its **game_id** in `HUMAN_INPUTS.md`. Claude Code writes `wavedash.toml` and can install the Wavedash CLI; you do the interactive **login** yourself (then mark it in `HUMAN_INPUTS.md`). The itch page must link to the Wavedash version for the Wavedash prize.

## 6. Optional assets

Drop anything you want used into `assets_inbox/` and mention it in feedback:
- A music track you have the rights to (e.g. from the Ovani free sampler jam participants receive — check its licence).
- Any CC0 model or sound pack.

Claude Code won't download music from unknown sources, and credits every third-party asset in `assets/CREDITS.md`.

## 7. Submission (4 Oct)

Claude Code prepares the builds, the itch/Wavedash page text (including the required **AI-use disclosure**), screenshots and a GIF. You upload to itch.io (or let it run the upload script once you're logged in), deploy to Wavedash, put the Wavedash link on the itch page, and submit to the jam. Target: done by **4 Oct, 20:00 IST**.
