// W07 (Chapter-1 scope) — planet view: every camera preset renders a planet with props, the
// view stays within its budgets (draw calls, props), fast-forward updates the view without
// errors, and new_world rebuilds everything. The full screenshot set is `npm run shots`.
import { test, expect } from '@playwright/test';
import { openGame, readTC, sendCmd, waitForTC, expectNoErrors } from './lib/game.mjs';
import { decodePng, averageRgb } from './lib/png.mjs';

const PRESETS = ['continent_a', 'continent_b', 'pole', 'village', 'close'];

test('W07 view: presets, budgets, run_ticks, new_world @ch1', async ({ page }, testInfo) => {
  test.setTimeout(240_000);
  const game = await openGame(page, { freeze: '1', speed: '0', gfx: 'low' }, { readyTimeout: 60_000 });
  for (const preset of PRESETS) {
    const res = await sendCmd(page, { cmd: 'camera', preset });
    expect(res.ok, JSON.stringify(res)).toBe(true);
    await page.waitForTimeout(400);
    // finish LOD/dirty rebuilds synchronously (the headless CPU rasteriser makes frames slow)
    await sendCmd(page, { cmd: 'flush_view' }, 120_000);
    await waitForTC(page, (tc) => tc.chunks_dirty === 0, null, { timeout: 60_000 });
    const tc = await readTC(page);
    testInfo.annotations.push({ type: preset, description: `props ${tc.prop_count}/${tc.prop_total} draws ~${tc.draw_calls}` });
    expect(tc.draw_calls, `${preset} draw calls`).toBeLessThanOrEqual(100);
    expect(tc.prop_count, `${preset} visible props`).toBeGreaterThan(500);
    const png = await page.screenshot();
    const img = decodePng(png);
    // the middle of the frame is planet (land, sea, snow), not the sky of that row
    const c = averageRgb(img, 640, 400, 40, 40);
    const sky = averageRgb(img, 30, 400, 8, 8);
    const diff = c.reduce((a, v, i) => a + Math.abs(v - sky[i]), 0);
    expect(diff, `${preset}: centre ${c.map(Math.round)} vs sky ${sky.map(Math.round)}`).toBeGreaterThan(40);
  }
  // fast-forward a year; colours and chunks refresh without errors
  const ff = await sendCmd(page, { cmd: 'run_ticks', n: 48 });
  expect(ff.ok).toBe(true);
  await waitForTC(page, (tc) => tc.ff_remaining === 0 && tc.tick >= 48, null, { timeout: 60_000 });
  // regenerate a different world
  const nw = await sendCmd(page, { cmd: 'new_world', seed: 7 });
  expect(nw.ok).toBe(true);
  await waitForTC(page, (tc) => tc.ready === true && tc.seed === 7 && tc.prop_total > 1000, null, { timeout: 90_000 });
  await sendCmd(page, { cmd: 'camera', preset: 'continent_b' });
  expectNoErrors(await readTC(page), game.consoleErrors);
});
