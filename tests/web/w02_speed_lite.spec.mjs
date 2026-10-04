// W02-lite — the bridge round-trips commands: ping, set_speed (value reported back), and
// ticks follow the speed setting. Full W02 (rates per speed) lands in Stage 1.9.
import { test, expect } from '@playwright/test';
import { openGame, readTC, sendCmd, waitForTC, expectNoErrors } from './lib/game.mjs';

test('W02-lite bridge: ping + set_speed round-trip @ch1', async ({ page }) => {
  // (S23) gfx=low like the other functional specs: the cloud layer is marched per pixel, which the CPU
  // rasteriser (SwiftShader) pays seconds a frame for; on a real GPU it is free (D-180)
  const game = await openGame(page, { gfx: 'low' });

  const ping = await sendCmd(page, { cmd: 'ping', echo: 'hello' });
  expect(ping.ok).toBe(true);
  expect(ping.result.pong).toBe(true);
  expect(ping.result.echo).toBe('hello');

  for (const n of [3, 0, 2, 1]) {
    const res = await sendCmd(page, { cmd: 'set_speed', n });
    expect(res.ok, JSON.stringify(res)).toBe(true);
    expect(res.result.speed).toBe(n);
    const tc = await readTC(page);
    expect(tc.speed).toBe(n);
  }

  // Paused: the clock must not advance. Days (3): it must (8 h/s).
  await sendCmd(page, { cmd: 'set_speed', n: 0 });
  const t0 = (await readTC(page)).hour;
  await page.waitForTimeout(700);
  await sendCmd(page, { cmd: 'publish' });
  expect((await readTC(page)).hour).toBe(t0);

  await sendCmd(page, { cmd: 'set_speed', n: 3 });
  await waitForTC(page, (tc, start) => tc.hour >= start + 4, t0, { timeout: 5_000 });

  expectNoErrors(await readTC(page), game.consoleErrors);
});

test('bridge: bad commands are reported, not swallowed @ch1', async ({ page }) => {
  await openGame(page);
  const bad = await sendCmd(page, { cmd: 'set_speed', n: 9 });
  expect(bad.ok).toBe(false);
  expect(bad.error).toContain('invalid speed');
  const unknown = await sendCmd(page, { cmd: 'no_such_command' });
  expect(unknown.ok).toBe(false);
  let tc = await readTC(page);
  expect(tc.errors.length).toBe(2);
  expect(tc.speed).toBe(1); // unchanged

  // Errors raised outside the bridge's pcall (engine callbacks) reach __TC.errors too.
  await sendCmd(page, { cmd: 'clear_errors' });
  await sendCmd(page, { cmd: 'debug_error' });
  tc = await waitForTC(page, (t) => t.errors.length > 0 && t, null, { timeout: 5_000 });
  expect(tc.errors.join('\n')).toContain('debug_error requested');
});
