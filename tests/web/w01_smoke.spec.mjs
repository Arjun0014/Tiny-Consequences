// W01 — Smoke: page loads, __TC.ready within 20 s, no console errors, no bridge errors.
import { test, expect } from '@playwright/test';
import { openGame, readTC, expectNoErrors } from './lib/game.mjs';
import { decodePng, averageRgb } from './lib/png.mjs';

const SKY_TOP = [0x2b, 0x6f, 0xc4];

test('W01 smoke: ready within 20 s, canvas renders, no errors @ch1', async ({ page }, testInfo) => {
  const game = await openGame(page);
  // Let a few more frames run and at least one periodic publish happen.
  await page.waitForTimeout(1200);
  const tc = await readTC(page);
  testInfo.annotations.push({ type: 'ready_ms', description: String(game.readyMs) });
  testInfo.annotations.push({ type: '__TC', description: JSON.stringify(tc) });

  expect(tc.ready).toBe(true);
  for (const k of ['seed', 'tick', 'year', 'speed', 'fps', 'sim_ms_avg', 'sim_ms_max', 'view_ms',
    'population', 'villages', 'events', 'last_headline', 'errors', 'state_hash']) {
    expect(tc, `__TC.${k} missing`).toHaveProperty(k);
  }
  expect(Array.isArray(tc.errors)).toBe(true);
  expect(tc.fps).toBeGreaterThan(0);
  expectNoErrors(tc, game.consoleErrors);

  // The canvas fills the viewport; WebGL draws the sky gradient (deep blue at the top) and a
  // planet in the middle (Stage 1.8: not sky-coloured at the centre).
  const box = await page.locator('#canvas').boundingBox();
  expect(box.width).toBeGreaterThan(1000);
  const png = await page.screenshot();
  await testInfo.attach('w01', { body: png, contentType: 'image/png' });
  const img = decodePng(png);
  const top = averageRgb(img, Math.round(box.x + 40), Math.round(box.y + 8), 8, 4);
  for (let i = 0; i < 3; i++) {
    expect(Math.abs(top[i] - SKY_TOP[i]), `top-left sky ${top.map(Math.round)} vs ${SKY_TOP}`).toBeLessThan(40);
  }
  const centre = averageRgb(img, Math.round(box.x + box.width / 2), Math.round(box.y + box.height * 0.55), 24, 24);
  const skyAt = averageRgb(img, Math.round(box.x + 40), Math.round(box.y + box.height * 0.55), 8, 8);
  const diff = centre.reduce((a, v, i) => a + Math.abs(v - skyAt[i]), 0);
  expect(diff, `planet centre ${centre.map(Math.round)} vs sky ${skyAt.map(Math.round)}`).toBeGreaterThan(40);
  expect(tc.prop_total, 'props baked').toBeGreaterThan(1000);
});
