// W03 (07_TESTING.md §5, Stage 2.1): every power creates its player root event and a visible
// change near its target (the cast effect, then the sim's). Driven through the bridge `power`
// command, which uses the power bar's own code path (game/ui/powers.lua).
import { test, expect } from '@playwright/test';
import { openGame, readTC, sendCmd, waitForTC, expectNoErrors } from './lib/game.mjs';
import { decodePng, averageRgb } from './lib/png.mjs';

const CASES = [
  { power: 'lightning', target: 'tanker', kind: 'power_lightning' },
  { power: 'weather', mode: 'rain', target: 'land', kind: 'power_rain' },
  { power: 'weather', mode: 'drought', target: 'land', kind: 'power_drought' },
  { power: 'temperature', mode: 'heat', target: 'sea', kind: 'power_heat' },
  { power: 'temperature', mode: 'chill', target: 'land', kind: 'power_chill' },
  { power: 'cull', species: 'grazers', target: 'land', kind: 'power_cull' },
  { power: 'introduce', species: 'grazers', target: 'land', kind: 'power_introduce' },
  { power: 'blight', target: 'farm', kind: 'power_blight' },
  { power: 'oil', target: 'sea', kind: 'power_oil' },
  { power: 'meteor', target: 'land', kind: 'power_meteor' },
];

function regionDiff(a, b, x, y, half) {
  let sum = 0, n = 0;
  for (let dy = -half; dy <= half; dy += 8) {
    for (let dx = -half; dx <= half; dx += 8) {
      const p = averageRgb(a, x + dx, y + dy, 4, 4);
      const q = averageRgb(b, x + dx, y + dy, 4, 4);
      sum += Math.abs(p[0] - q[0]) + Math.abs(p[1] - q[1]) + Math.abs(p[2] - q[2]);
      n++;
    }
  }
  return sum / n;
}

test('W03 powers: root event and a visible change near the target @ch2', async ({ page }, testInfo) => {
  test.setTimeout(900_000);
  const game = await openGame(page, { freeze: '1', speed: '0', gfx: 'low' }, { readyTimeout: 60_000 });
  await sendCmd(page, { cmd: 'camera', preset: 'village' });
  await sendCmd(page, { cmd: 'flush_view' }, 120_000);
  await sendCmd(page, { cmd: 'freeze_visuals', on: false });
  for (const cs of CASES) {
    const look = await sendCmd(page, { cmd: 'look', target: cs.target }, 120_000);
    expect(look.ok, `${cs.power}: look ${JSON.stringify(look)}`).toBe(true);
    await sendCmd(page, { cmd: 'flush_view' }, 120_000);
    await page.waitForTimeout(400);
    const before = decodePng(await page.screenshot());
    const res = await sendCmd(page, { cmd: 'power', power: cs.power, mode: cs.mode, species: cs.species, target: cs.target });
    expect(res.ok, `${cs.power}: ${JSON.stringify(res)}`).toBe(true);
    // the cast effect plays at once (bolt, pulse ring, falling meteor) ...
    const after1 = decodePng(await page.screenshot());
    // ... and the sim applies the command at the next tick
    await sendCmd(page, { cmd: 'set_speed', n: 1 });
    await waitForTC(page, (tc, k) => tc.last_power.startsWith(k), cs.kind, { timeout: 20_000 });
    await sendCmd(page, { cmd: 'set_speed', n: 0 });
    await page.waitForTimeout(600);
    const after2 = decodePng(await page.screenshot());
    const r = res.result;
    const x = Math.round(Math.min(1240, Math.max(40, r.sx || 640)));
    const y = Math.round(720 - Math.min(680, Math.max(40, r.sy || 360)));
    const d = Math.max(regionDiff(before, after1, x, y, 48), regionDiff(before, after2, x, y, 48));
    testInfo.annotations.push({ type: cs.kind, description: `cell ${r.cell} at ${x},${y}: diff ${d.toFixed(1)}` });
    console.log(`W03 ${cs.kind} cell ${r.cell} at ${x},${y}: diff ${d.toFixed(1)}`);
    expect(d, `${cs.kind}: visible change near the target`).toBeGreaterThan(5);
    await page.waitForTimeout(1300);   // let the pulse finish before the next cast
  }
  const tc = await readTC(page);
  expectNoErrors(tc, game.consoleErrors);
});
