// Helpers for driving the game through the test bridge (05_ARCHITECTURE.md §10).
import { expect } from '@playwright/test';
import { gameUrl } from '../../../scripts/lib/browser.mjs';

// Console lines that are not game errors (none known yet; add with a reason).
const CONSOLE_ERROR_ALLOWLIST = [];

/**
 * Opens the game in test mode and waits for window.__TC.ready.
 * Returns { consoleErrors, consoleLog } arrays that keep filling while the page lives.
 */
export async function openGame(page, params = {}, { readyTimeout = 20_000 } = {}) {
  const consoleErrors = [];
  const consoleLog = [];
  page.on('console', (msg) => {
    const line = `[${msg.type()}] ${msg.text()}`;
    consoleLog.push(line);
    if (msg.type() === 'error' && !CONSOLE_ERROR_ALLOWLIST.some((re) => re.test(msg.text()))) {
      consoleErrors.push(msg.text());
    }
  });
  page.on('pageerror', (err) => consoleErrors.push(`pageerror: ${err.message}`));
  await page.goto(gameUrl('', params));
  const t0 = Date.now();
  try {
    await page.waitForFunction(() => window.__TC && window.__TC.ready === true, null, {
      timeout: readyTimeout,
      polling: 100,
    });
  } catch (e) {
    const tc = await readTC(page).catch(() => null);
    throw new Error(
      `__TC.ready not true within ${readyTimeout} ms\n__TC=${JSON.stringify(tc)}\n` +
      `console:\n${consoleLog.slice(-30).join('\n')}`,
    );
  }
  return { consoleErrors, consoleLog, readyMs: Date.now() - t0 };
}

export function readTC(page) {
  return page.evaluate(() => window.__TC);
}

let nextId = 1;

/** Queues a bridge command and waits until the game reports it processed. Returns last_cmd. */
export async function sendCmd(page, cmd, { timeout = 10_000 } = {}) {
  const id = `c${Date.now()}_${nextId++}`;
  await page.evaluate((c) => {
    window.__TC_CMDS = window.__TC_CMDS || [];
    window.__TC_CMDS.push(c);
  }, { ...cmd, id });
  const handle = await page.waitForFunction(
    (wanted) => window.__TC && window.__TC.last_cmd && window.__TC.last_cmd.id === wanted && window.__TC.last_cmd,
    id,
    { timeout, polling: 50 },
  );
  return handle.jsonValue();
}

/** Waits until predicate(__TC) is truthy (predicate runs in the page). */
export async function waitForTC(page, predicate, arg, { timeout = 10_000 } = {}) {
  const handle = await page.waitForFunction(
    ({ src, a }) => window.__TC && new Function('tc', 'a', `return (${src})(tc, a);`)(window.__TC, a),
    { src: predicate.toString(), a: arg },
    { timeout, polling: 50 },
  );
  return handle.jsonValue();
}

export function expectNoErrors(tc, consoleErrors) {
  expect(tc.errors, `bridge errors: ${JSON.stringify(tc.errors)}`).toEqual([]);
  expect(consoleErrors, `console errors:\n${consoleErrors.join('\n')}`).toEqual([]);
}
