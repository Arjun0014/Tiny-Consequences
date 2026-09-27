// W06 — In-browser self-test (07_TESTING.md §5): determinism in the real HTML5 runtime.
// The bridge `selftest` builds the same world twice inside the page (the pre-spun default
// world, then a small generated seed), applies the same test action (ignite 3 forested
// cells), runs 2 years and returns both state hashes. They must be identical, and the live
// world must be untouched.
// It also compares both hashes with the same self-test run under LuaJIT (tools/selftest.lua):
// identical, so the headless sim suite speaks for the browser build (cross-runtime determinism).
import { execFileSync } from 'node:child_process';
import { test, expect } from '@playwright/test';
import { openGame, readTC, sendCmd, expectNoErrors } from './lib/game.mjs';
import { ROOT, resolveLuajit } from '../../scripts/lib/util.mjs';

function luajitReference() {
  const luajit = resolveLuajit();
  if (!luajit) return null;
  return JSON.parse(execFileSync(luajit, ['tools/selftest.lua'], { cwd: ROOT, encoding: 'utf8' }));
}

test('W06 selftest: identical state hashes in the HTML5 runtime @ch1', async ({ page }, testInfo) => {
  test.setTimeout(240_000);
  const game = await openGame(page, { speed: '0' }, { readyTimeout: 60_000 });
  const tc0 = await readTC(page);
  expect(tc0.prespun, 'default start uses the pre-spun world').toBe(true);

  const a = await sendCmd(page, { cmd: 'selftest', years: 2 }, { timeout: 120_000 });
  expect(a.ok, JSON.stringify(a)).toBe(true);
  testInfo.annotations.push({ type: 'default world', description: JSON.stringify(a.result) });
  console.log('W06 default', JSON.stringify(a.result));
  expect(a.result.source).toBe('default.tcsave');
  expect(a.result.tick1).toBe(96);
  expect(a.result.hash1).toBe(a.result.hash2);
  expect(a.result.events1).toBe(a.result.events2);
  expect(a.result.ignited).toBe(3);
  expect(a.result.live_unchanged, 'live world untouched').toBe(true);

  const b = await sendCmd(page, { cmd: 'selftest', years: 1, seed: 5 }, { timeout: 180_000 });
  expect(b.ok, JSON.stringify(b)).toBe(true);
  testInfo.annotations.push({ type: 'seed 5', description: JSON.stringify(b.result) });
  console.log('W06 seed 5', JSON.stringify(b.result));
  expect(b.result.hash1).toBe(b.result.hash2);
  expect(b.result.live_unchanged).toBe(true);

  const ref = luajitReference();
  if (ref) {
    console.log('W06 LuaJIT reference', JSON.stringify(ref));
    expect(a.result.hash1, 'default world: HTML5 hash == LuaJIT hash').toBe(ref.default.hash);
    expect(a.result.events1).toBe(ref.default.events);
    expect(b.result.hash1, 'seed 5: HTML5 hash == LuaJIT hash').toBe(ref.seed5.hash);
    expect(b.result.events1).toBe(ref.seed5.events);
  } else {
    testInfo.annotations.push({ type: 'skipped', description: 'LuaJIT not found: cross-runtime check skipped' });
  }

  expectNoErrors(await readTC(page), game.consoleErrors);
});
