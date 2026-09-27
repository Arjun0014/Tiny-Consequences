// W02 — Speed: ticks advance at the expected rates per speed setting (04_PLAYER.md §3):
// 0 = paused, 1 = 2 ticks/s, 2 = 8, 3 = 48, 4 = max (as many as the 8 ms frame budget allows).
// Headless Chromium renders WebGL on the CPU (SwiftShader), so frames are slow: rates are
// measured as sim ticks over wall time with tolerances, and "max" is checked to be
// budget-bound (sim ms per frame within budget) and at least as fast as "faster".
import { test, expect } from '@playwright/test';
import { openGame, readTC, sendCmd, expectNoErrors } from './lib/game.mjs';

// what's tested is the speed controller, not the CPU rasteriser: keep frames light (W08 note)
test.use({ viewport: { width: 640, height: 360 } });

async function rate(page, level, seconds) {
  await sendCmd(page, { cmd: 'set_speed', n: level });
  await page.waitForTimeout(600);                      // settle (backlog, frame pacing)
  await sendCmd(page, { cmd: 'publish' });
  const a = await readTC(page);
  const t0 = Date.now();
  await page.waitForTimeout(seconds * 1000);
  await sendCmd(page, { cmd: 'publish' });
  const b = await readTC(page);
  const dt = (Date.now() - t0) / 1000;
  return { rate: (b.tick - a.tick) / dt, fps: b.fps, sim_ms_avg: b.sim_ms_avg, sim_ms_max: b.sim_ms_max };
}

test('W02 speed: tick rates per speed level @ch1', async ({ page }, testInfo) => {
  test.setTimeout(180_000);
  const game = await openGame(page, { freeze: '1', speed: '0' }, { readyTimeout: 60_000 });
  // keep the headless renderer light: whole-planet view, no inspector
  await sendCmd(page, { cmd: 'camera', preset: 'whole' });

  const r0 = await rate(page, 0, 1.5);
  const r1 = await rate(page, 1, 3);
  const r2 = await rate(page, 2, 3);
  const r3 = await rate(page, 3, 3);
  const r4 = await rate(page, 4, 3);
  const rows = { r0, r1, r2, r3, r4 };
  testInfo.annotations.push({ type: 'rates', description: JSON.stringify(rows) });
  console.log('W02 rates', JSON.stringify(rows));

  expect(r0.rate, 'paused').toBe(0);
  expect(r1.rate, 'normal ≈ 2/s').toBeGreaterThan(1.3);
  expect(r1.rate, 'normal ≈ 2/s').toBeLessThan(2.8);
  expect(r2.rate, 'fast ≈ 8/s').toBeGreaterThan(5.5);
  expect(r2.rate, 'fast ≈ 8/s').toBeLessThan(10.5);
  // faster: 48/s unless the per-frame sim budget (6 ms) caps it on a slow headless frame rate
  expect(r3.rate, 'faster').toBeLessThan(60);
  expect(r3.rate, 'faster').toBeGreaterThan(12);
  expect(r3.rate, 'faster beats fast').toBeGreaterThan(r2.rate * 1.5);
  // max: budget-bound (8 ms/frame + at most one tick of overshoot) and not slower than faster
  // (when both are capped by the frame budget they measure the same rate, ± frame noise)
  expect(r4.rate, 'max ≥ faster').toBeGreaterThan(r3.rate * 0.85);
  expect(r4.sim_ms_avg, 'max stays within its frame budget').toBeLessThan(8 + 12);

  // pause again: the tick freezes
  await sendCmd(page, { cmd: 'set_speed', n: 0 });
  await page.waitForTimeout(300);
  await sendCmd(page, { cmd: 'publish' });
  const p0 = (await readTC(page)).tick;
  await page.waitForTimeout(800);
  await sendCmd(page, { cmd: 'publish' });
  expect((await readTC(page)).tick).toBe(p0);
  expectNoErrors(await readTC(page), game.consoleErrors);
});
