// W02 — Speed: sim hours advance at the expected rates per speed setting (docs/world/15 §6):
// 0 = paused, 1 = 0.2 h/s, 2 = 1.25, 3 = 8 (a day in 3 s), 4 = 168 (a week a second),
// 5 = 2,688 (16 weeks a second), 6 = max (as many hours as the 8 ms frame budget allows).
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
  return { rate: (b.hour_f - a.hour_f) / dt, fps: b.fps, sim_ms_avg: b.sim_ms_avg, sim_ms_max: b.sim_ms_max };
}

test('W02 speed: hour rates per speed level @ch1', async ({ page }, testInfo) => {
  test.setTimeout(180_000);
  const game = await openGame(page, { freeze: '1', speed: '0', gfx: 'low' }, { readyTimeout: 60_000 });
  // keep the headless renderer light: whole-planet view, no inspector
  await sendCmd(page, { cmd: 'camera', preset: 'whole' });

  const r0 = await rate(page, 0, 1.5);
  const r1 = await rate(page, 1, 5);
  const r2 = await rate(page, 2, 3);
  const r3 = await rate(page, 3, 3);
  const r4 = await rate(page, 4, 3);
  const r5 = await rate(page, 5, 3);
  const r6 = await rate(page, 6, 3);
  const rows = { r0, r1, r2, r3, r4, r5, r6 };
  testInfo.annotations.push({ type: 'rates', description: JSON.stringify(rows) });
  console.log('W02 rates', JSON.stringify(rows));

  expect(r0.rate, 'paused').toBe(0);
  expect(r1.rate, 'watch ≈ 0.2 h/s').toBeGreaterThan(0.12);
  expect(r1.rate, 'watch ≈ 0.2 h/s').toBeLessThan(0.3);
  expect(r2.rate, 'brisk ≈ 1.25 h/s').toBeGreaterThan(0.9);
  expect(r2.rate, 'brisk ≈ 1.25 h/s').toBeLessThan(1.6);
  expect(r3.rate, 'days ≈ 8 h/s').toBeGreaterThan(5.5);
  expect(r3.rate, 'days ≈ 8 h/s').toBeLessThan(10.5);
  // weeks: 168 h/s unless the per-frame budget caps it on a slow headless frame rate
  expect(r4.rate, 'weeks').toBeLessThan(200);
  expect(r4.rate, 'weeks').toBeGreaterThan(60);
  // seasons: 2,688 h/s, in practice capped by the planet week's cost in the debug build
  expect(r5.rate, 'seasons beats weeks').toBeGreaterThan(r4.rate * 1.5);
  // max: budget-bound and not slower than seasons (± frame noise)
  expect(r6.rate, 'max ≥ seasons').toBeGreaterThan(r5.rate * 0.85);
  // debug build under the CPU rasteriser: a planet week can take ~10-40 ms, so this is only a
  // sanity bound; W08 checks the real budget on the release build
  expect(r6.sim_ms_avg, 'max stays near its frame budget').toBeLessThan(8 + 60);

  // pause again: the tick freezes
  await sendCmd(page, { cmd: 'set_speed', n: 0 });
  await page.waitForTimeout(300);
  await sendCmd(page, { cmd: 'publish' });
  const p0 = (await readTC(page)).hour_f;
  await page.waitForTimeout(800);
  await sendCmd(page, { cmd: 'publish' });
  expect((await readTC(page)).hour_f).toBe(p0);
  expectNoErrors(await readTC(page), game.consoleErrors);
});
