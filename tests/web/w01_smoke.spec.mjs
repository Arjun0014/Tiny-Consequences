// W01 — Smoke: page loads, __TC.ready within 20 s, no console errors, no bridge errors.
import { test, expect } from '@playwright/test';
import { openGame, readTC, expectNoErrors } from './lib/game.mjs';
import { decodePng, averageRgb } from './lib/png.mjs';

const SKY = [0x5f, 0xd0, 0xf0];

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

  // The canvas fills the viewport and WebGL actually draws the sky-blue clear colour.
  const box = await page.locator('#canvas').boundingBox();
  expect(box.width).toBeGreaterThan(1000);
  const png = await page.screenshot();
  await testInfo.attach('w01', { body: png, contentType: 'image/png' });
  const img = decodePng(png);
  const rgb = averageRgb(img, Math.round(box.x + box.width / 2), Math.round(box.y + box.height / 2), 16, 16);
  for (let i = 0; i < 3; i++) {
    expect(Math.abs(rgb[i] - SKY[i]), `centre pixel ${rgb.map(Math.round)} vs sky ${SKY}`).toBeLessThan(12);
  }
});
