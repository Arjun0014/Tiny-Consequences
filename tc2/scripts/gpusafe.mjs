// (session 27, the person's word: "don't use the RX 5600 graphics, that causes the PC to restart") Every job that draws - Unity in batchmode
// (shots, Web builds, tests) and headless Chromium (the Web test, perf, book pages) - runs on the integrated GPU only:
//  - each exe that draws is set to Windows' "power saving" GPU preference (HKCU\Software\Microsoft\DirectX\UserGpuPreferences, what
//    Settings > Graphics writes) - Unity's editor and Playwright's own Chromium; the person's Chrome keeps its own setting;
//  - while the RX 5600M (PCI VEN_1002 DEV_731F) is present at all, no drawing job starts (exit 3) unless TC2_ALLOW_DGPU=1 is set.
// After the restart of 26 Oct 2026 the card stands "phantom" (not present): jobs run on the integrated AMD Radeon.
import { spawnSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const DGPU = 'PCI\\VEN_1002&DEV_731F*';

/** True when the RX 5600M is present (enumerated and working, not a phantom left from an earlier boot). */
export function dgpuPresent() {
  const r = spawnSync('powershell', ['-NoProfile', '-Command',
    `$d = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -like '${DGPU}' }; if ($d) { 'present ' + $d.Status } else { 'absent' }`], { encoding: 'utf8' });
  const out = (r.stdout || '').trim();
  if (r.status !== 0 || !out) return true;   // (cannot tell: assume it is there)
  return out.startsWith('present');
}

/** Sets an exe's Windows GPU preference to power saving (the integrated GPU); idempotent. */
export function powerSaving(exe) {
  if (!exe || !fs.existsSync(exe)) return false;
  const win = path.resolve(exe).replace(/\//g, '\\');
  const r = spawnSync('reg', ['add', 'HKCU\\Software\\Microsoft\\DirectX\\UserGpuPreferences', '/v', win, '/t', 'REG_SZ', '/d', 'GpuPreference=1;', '/f'], { encoding: 'utf8' });
  return r.status === 0;
}

/** Playwright's own Chromium executables (full and headless shell, every installed revision). */
export function playwrightChromiums() {
  const base = path.join(process.env.LOCALAPPDATA || '', 'ms-playwright');
  const out = [];
  if (!fs.existsSync(base)) return out;
  for (const d of fs.readdirSync(base)) {
    for (const rel of ['chrome-win64/chrome.exe', 'chrome-win/chrome.exe', 'chrome-headless-shell-win64/chrome-headless-shell.exe', 'chrome-win/headless_shell.exe']) {
      const p = path.join(base, d, rel);
      if (fs.existsSync(p)) out.push(p);
    }
  }
  return out;
}

/** The guard every drawing job calls first: power saving for the exes it will use; refuses (exit 3) while the RX 5600M is present. */
export function guardGpu(label, exes = []) {
  for (const e of exes) powerSaving(e);
  if (process.env.TC2_ALLOW_DGPU === '1') return;
  if (dgpuPresent()) {
    console.log(`${label}: REFUSED - the RX 5600M is present (it restarts the PC under load). Disable it in Device Manager, or set TC2_ALLOW_DGPU=1 to override.`);
    process.exit(3);
  }
}

/** For browser scripts: the guard, with Playwright's Chromium set to power saving. */
export function guardBrowser(label) { guardGpu(label, playwrightChromiums()); }
