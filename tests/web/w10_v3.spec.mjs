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
