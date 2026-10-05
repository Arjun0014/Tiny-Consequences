// TC2 compaction recovery hooks (Claude Code 2.1.289 semantics, read from the installed binary):
//   PreCompact   (matcher field "trigger": manual|auto)  exit 0 -> stdout is appended to the compaction instructions
//   SessionStart (matcher field "source": ...|compact)   exit 0 -> stdout is shown to Claude
// This script never commits, stages or edits tracked files. It writes one snapshot of the working state to an
// ignored folder (tc2/out/compact/) before a compaction and prints it back, with the recovery protocol, after.
// It always exits 0 so a failure here can never block a compaction or a session start.
import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const mode = process.argv[2] || 'pre';
const root = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const snapDir = path.join(root, 'tc2', 'out', 'compact');
const snapFile = path.join(snapDir, 'last_precompact.md');

function readStdin() {
  try { return fs.readFileSync(0, 'utf8'); } catch { return ''; }
}

function sh(cmd, ms = 5000) {
  try {
    return execSync(cmd, { cwd: root, timeout: ms, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
  } catch { return '(unavailable)'; }
}

function heavyProcesses() {
  if (process.platform !== 'win32') return sh('ps -eo pid,etime,comm | grep -E "dotnet|Unity|blender|chrome" | head -20');
  return sh('powershell -NoProfile -Command "Get-Process | Where-Object { $_.ProcessName -match \'^(dotnet|Unity|blender)$\' } | ' +
    'Select-Object Id,ProcessName,StartTime,@{n=\'MB\';e={[math]::Round($_.WorkingSet64/1MB)}} | Format-Table -AutoSize | Out-String -Width 160"', 8000);
}

function snapshot(trigger) {
  const lines = [
    `# TC2 state captured just before a context compaction (${trigger})`,
    '',
    `- When: ${new Date().toISOString()}`,
    `- Branch: ${sh('git rev-parse --abbrev-ref HEAD')}  HEAD: ${sh('git log -1 --format="%h %s" --no-color').slice(0, 220)}`,
    '',
    '## git status --short',
    '```',
    sh('git status --short') || '(clean)',
    '```',
    '',
    '## git log --oneline -8',
    '```',
    sh('git log --oneline -8 --no-color').split('\n').map(l => l.slice(0, 200)).join('\n'),
    '```',
    '',
    '## Heavy processes (dotnet / Unity / blender) at compaction',
    '```',
    heavyProcesses() || '(none)',
    '```',
  ];
  return lines.join('\n');
}

const PROTOCOL = `CONTEXT WAS COMPACTED. DO NOT CODE YET. Restore TC2 (Tiny Consequences 2.0) state first:
1. read docs/tc2/NEXT_SESSION_PROMPT.md;
2. read docs/tc2/SESSION_HANDOFF.md;
3. read docs/tc2/PROGRESS.md CURRENT STATE;
4. read the newest DECISIONS entries (docs/tc2/DECISIONS.md);
5. read the domain docs for the area in progress (docs/tc2/03-17);
6. git status;
7. git log --oneline -20;
8. inspect any running process / output file named in the handoff or in the snapshot below (long headless runs write to tc2/out/);
9. reconstruct the exact current step (dirty files = work in progress; compare with the handoff's next steps);
10. only then resume implementation, autonomously, without asking routine questions.
Never stage legacy TC1 files in [tc2] commits. One heavy process at a time.`;

try {
  const input = readStdin();
  let data = {};
  try { data = JSON.parse(input || '{}'); } catch { data = {}; }

  if (mode === 'pre') {
    const trigger = data.trigger || 'unknown';
    let snap = '';
    try {
      fs.mkdirSync(snapDir, { recursive: true });
      snap = snapshot(trigger);
      fs.writeFileSync(snapFile, snap + '\n');
    } catch { /* the instructions below still go out */ }
    process.stdout.write(
`TC2 context is about to compact. Ensure the latest major work is committed or safely checkpointed and durable docs are current: DECISIONS, PROGRESS CURRENT STATE, SESSION_HANDOFF, NEXT_SESSION_PROMPT, measurements and exact next step.
For the summary: begin it with "CONTEXT WAS COMPACTED - run the TC2 recovery protocol (docs/tc2/NEXT_SESSION_PROMPT.md, SESSION_HANDOFF.md, PROGRESS.md CURRENT STATE, newest DECISIONS, git status, git log) before any edit." Then preserve exactly: the current task and the precise step in progress; files edited but not committed and why; background processes still running (what, command, output file, expected end); the latest test, benchmark and long-run numbers; decisions made but not yet written to docs/tc2/DECISIONS.md; the next command to run. A snapshot of git state and heavy processes was written to tc2/out/compact/last_precompact.md.
`);
  } else if (mode === 'post') {
    let snap = '';
    try {
      const st = fs.statSync(snapFile);
      if (Date.now() - st.mtimeMs < 6 * 3600 * 1000) snap = fs.readFileSync(snapFile, 'utf8');
    } catch { /* no snapshot */ }
    process.stdout.write(PROTOCOL + '\n');
    if (snap) process.stdout.write('\n--- snapshot written by the PreCompact hook (tc2/out/compact/last_precompact.md) ---\n' + snap + '\n');
  }
} catch {
  if (mode === 'post') process.stdout.write(PROTOCOL + '\n');
}
process.exit(0);
