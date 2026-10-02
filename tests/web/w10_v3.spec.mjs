// W10 — the v3 agent world is the default game (D-078, D-079): a normal launch opens it at ▶▶,
// the watch ladder stops at ▶▶▶ (faster is an explicit skip), a skip to dawn hides the people,
// shows progress and lands on the target with everyone where the sim put them; ?world=v2 still
// opens the legacy world.
import { test, expect } from '@playwright/test';
import { openGame, readTC, sendCmd, waitForTC, expectNoErrors } from './lib/game.mjs';

test('W10 v3 default: launch at ▶▶, watch ladder, skip to dawn @ch1', async ({ page }) => {
  const game = await openGame(page, { world: null, gfx: 'low' }, { readyTimeout: 60_000 });
  let tc = await readTC(page);
  expect(tc.v3, 'the default launch is a v3 world').toBe(true);
  expect(tc.speed, 'v3 starts at ▶▶').toBe(2);
  expect(tc.population).toBeGreaterThan(10);

  // the watch ladder: 0-3 only
  const bad = await sendCmd(page, { cmd: 'set_speed', n: 4 });
  expect(bad.ok).toBe(false);
  expect(bad.error).toContain('invalid speed');
  await sendCmd(page, { cmd: 'clear_errors' });
  const ok = await sendCmd(page, { cmd: 'set_speed', n: 3 });
  expect(ok.ok).toBe(true);

  // a person to check against after the skip
  const before = await sendCmd(page, { cmd: 'v3_person', pid: 'worker' });
  expect(before.ok).toBe(true);
  const t0 = (await readTC(page)).hour_f;
  const sk = await sendCmd(page, { cmd: 'v3_skip', kind: 'dawn' });
  expect(sk.ok, JSON.stringify(sk)).toBe(true);
  const to = sk.result.to;
  expect(to).toBeGreaterThan(t0);
  expect(to - t0).toBeLessThanOrEqual(24.5);
  tc = await waitForTC(page, (t, target) => !t.skipping && t.hour_f >= target - 1e-6 && t, to, { timeout: 90_000 });
  expect(tc.speed, 'the watch speed is kept after a skip').toBe(3);
  const after = await sendCmd(page, { cmd: 'v3_person', pid: before.result.id });
  expect(after.ok).toBe(true);
  expect(Number.isFinite(after.result.x) && Number.isFinite(after.result.y)).toBe(true);
  expectNoErrors(await readTC(page), game.consoleErrors);
});

test('W10 legacy: ?world=v2 still opens the v2 world @ch1', async ({ page }) => {
  const game = await openGame(page, { world: 'v2', speed: '0', gfx: 'low' }, { readyTimeout: 60_000 });
  const tc = await readTC(page);
  expect(tc.v3).toBe(false);
  expect(tc.population).toBeGreaterThan(100);
  expectNoErrors(tc, game.consoleErrors);
});

// (S14) The browser's Lua 5.1 lives the same v3 world as LuaJIT (the headless tools and tests):
// before sim/dmath.lua and sim/fmt.lua the two had diverged within a game day (last-bit
// differences in sin/cos/exp/pow, a %.0f tie formatted two ways, a pairs() order).
test('W10 determinism: the v3 world in the browser equals LuaJIT @ch1', async ({ page }) => {
  test.setTimeout(240_000);
  const { execFileSync } = await import('node:child_process');
  const { resolveLuajit } = await import('../../scripts/lib/util.mjs');
  const game = await openGame(page, { world: null, speed: '0', gfx: 'low' }, { readyTimeout: 60_000 });
  const r = await sendCmd(page, { cmd: 'v3_hash', hours: 36, every: 12 }, { timeout: 180_000 });
  expect(r.ok, JSON.stringify(r)).toBe(true);
  const lj = execFileSync(resolveLuajit(), ['tools/v3_hash.lua', '--hours', '36', '--every', '12', '--from', String(r.result.start)], { encoding: 'utf8' });
  const ref = [...lj.matchAll(/hour\s+(\d+)\s+hash\s+(\d+)/g)].map((m) => [Number(m[1]), m[2]]);
  expect(ref.length).toBe(4);
  for (const x of r.result.hashes) {
    const y = ref.find((q) => q[0] === x.hour);
    expect(x.hash, `state hash at +${x.hour} h`).toBe(y && y[1]);
  }
  expectNoErrors(await readTC(page), game.consoleErrors);
});
