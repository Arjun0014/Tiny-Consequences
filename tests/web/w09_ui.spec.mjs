// W09 (Chapter-1 scope) — UI: the inspector opens for a cell, a village, a person and the
// planet (bridge `select{...}` and the real click/pick path), HUD panels don't overlap and stay
// on screen at 1280x720, 1920x1080, 1024x768 and 844x390 with every text inside its panel (S26), and the speed buttons
// work with a real mouse click.
import { test, expect } from '@playwright/test';
import { openGame, readTC, sendCmd, waitForTC, expectNoErrors } from './lib/game.mjs';

// Defold samples the mouse button once per frame, so a click must span a few (slow) frames.
async function click(page, x, y) {
  await page.mouse.move(x, y);
  await page.mouse.down();
  await page.waitForTimeout(200);
  await page.mouse.up();
}

function overlaps(a, b) {
  return a[0] < b[2] - 0.5 && b[0] < a[2] - 0.5 && a[1] < b[3] - 0.5 && b[1] < a[3] - 0.5;
}

async function checkLayout(page, label, w, h) {
  await page.setViewportSize({ width: w, height: h });
  await page.waitForTimeout(1200);
  // (S26) and every text drawn inside its panel and on the screen (the GUI's own measure: ui{audit})
  await sendCmd(page, { cmd: 'ui', audit_request: true });
  await page.waitForTimeout(300);
  const ui = (await sendCmd(page, { cmd: 'ui', bounds: true, audit: true })).result;
  expect(Array.isArray(ui.audit) ? ui.audit : [], `${label}: texts leaving their panel`).toEqual([]);
  expect(ui.window.w, `${label} window`).toBe(w);
  const names = Object.keys(ui.bounds);
  for (const k of ['clock', 'stats', 'inspector']) expect(names, `${label}: ${k} visible`).toContain(k);
  for (const k of names) {
    const r = ui.bounds[k];
    expect(r[0], `${label} ${k} left`).toBeGreaterThanOrEqual(-0.5);
    expect(r[1], `${label} ${k} bottom`).toBeGreaterThanOrEqual(-0.5);
    expect(r[2], `${label} ${k} right`).toBeLessThanOrEqual(w + 0.5);
    expect(r[3], `${label} ${k} top`).toBeLessThanOrEqual(h + 0.5);
  }
  for (let i = 0; i < names.length; i++) {
    for (let j = i + 1; j < names.length; j++) {
      const a = names[i], b = names[j];
      expect(overlaps(ui.bounds[a], ui.bounds[b]), `${label}: ${a} overlaps ${b}`).toBe(false);
    }
  }
  return ui;
}

test('W09 UI: inspector for cell/village/person/planet, HUD layout, speed buttons @ch1', async ({ page }, testInfo) => {
  test.setTimeout(180_000);
  // gfx=low: the UI is what is tested; the CPU rasteriser can't draw the full scene fast enough
  const game = await openGame(page, { freeze: '1', speed: '0', gfx: 'low' }, { readyTimeout: 60_000 });
  let tc = await readTC(page);

  // cell (bridge) and planet
  let r = await sendCmd(page, { cmd: 'select', cell: 100 });
  expect(r.ok, JSON.stringify(r)).toBe(true);
  expect(r.result.kind).toBe('cell');
  expect(r.result.rows.join('|')).toContain('Temperature');
  r = await sendCmd(page, { cmd: 'select', planet: true });
  expect(r.result.title).toBe('The Planet');
  expect(r.result.rows.join('|')).toContain('CO₂');

  // village and person (needs Stage 1.7 villages)
  if (tc.villages > 0) {
    r = await sendCmd(page, { cmd: 'select', village: 'first' });
    expect(r.ok, JSON.stringify(r)).toBe(true);
    expect(r.result.kind).toBe('village');
    expect(r.result.rows.join('|')).toContain('People');
    r = await sendCmd(page, { cmd: 'select', person: 'first' });
    expect(r.ok, JSON.stringify(r)).toBe(true);
    expect(r.result.kind).toBe('person');
    expect(r.result.rows.join('|')).toContain('Age');
  } else {
    testInfo.annotations.push({ type: 'skipped', description: 'no villages in this world yet (Stage 1.7)' });
  }

  // the real picking path: click the middle of the planet -> something gets selected
  await sendCmd(page, { cmd: 'select', none: true });
  await sendCmd(page, { cmd: 'camera', preset: 'default' });
  await click(page, 560, 390);
  tc = await waitForTC(page, (t) => t.selection && t.selection.kind && t, null, { timeout: 5_000 });
  expect(['cell', 'village', 'person', 'ship', 'animal']).toContain(tc.selection.kind);
  const ins = (await sendCmd(page, { cmd: 'ui', inspector: true })).result.inspector;
  expect(ins && ins.title, 'inspector open after a click').toBeTruthy();

  // HUD layout at two resolutions (inspector open, debug overlay on)
  await sendCmd(page, { cmd: 'ui', debug: true });
  await checkLayout(page, '1280x720', 1280, 720);
  await checkLayout(page, '1920x1080', 1920, 1080);
  // (S26) a 4:3 window and a phone held sideways (the UI scaled up, the panels' heights from the room left)
  await checkLayout(page, '1024x768', 1024, 768);
  await checkLayout(page, '844x390', 844, 390);
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.waitForTimeout(1000);

  // speed buttons respond to a real mouse click (fast = 3rd button in the clock panel, where the game draws it)
  const ui = (await sendCmd(page, { cmd: 'ui', bounds: true })).result;
  const fb = ui.buttons.speed3;
  expect(fb, 'the third speed button is drawn').toBeTruthy();
  const fastX = (fb[0] + fb[2]) / 2;
  const topY = 720 - (fb[1] + fb[3]) / 2;                // bounds are y-up; mouse is y-down
  await click(page, fastX, topY);
  await waitForTC(page, (t) => t.speed === 2, null, { timeout: 5_000 });
  // inspector close button clears the selection
  await sendCmd(page, { cmd: 'select', planet: true });
  const b2 = (await sendCmd(page, { cmd: 'ui', bounds: true })).result.bounds.inspector;
  await click(page, b2[2] - 24, 720 - (b2[3] - 24));
  await waitForTC(page, (t) => t.selection === false, null, { timeout: 5_000 });

  expectNoErrors(await readTC(page), game.consoleErrors);
});
