// Playwright config for browser tests (docs/07_TESTING.md §5). Run via `npm run test:web`.
// Tests tag their chapter in the title (e.g. "W01 smoke @ch1"); only chapters <= TC_CHAPTER run.
import { defineConfig } from '@playwright/test';
import { CHROMIUM_ARGS, VIEWPORT } from './scripts/lib/browser.mjs';

const chapter = Number(process.env.TC_CHAPTER || 1);
const port = Number(process.env.TC_TEST_PORT || 8181);
const chapterTags = Array.from({ length: Math.max(1, chapter) }, (_, i) => i + 1).join('|');

export default defineConfig({
  testDir: 'tests/web',
  testMatch: /.*\.spec\.m?js$/,
  outputDir: 'test-results',
  timeout: 90_000,
  expect: { timeout: 20_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  grep: new RegExp(`@ch(${chapterTags})\\b`),
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    browserName: 'chromium',
    headless: process.env.TC_HEADED ? false : true,
    viewport: VIEWPORT,
    deviceScaleFactor: 1,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    launchOptions: { args: CHROMIUM_ARGS },
  },
  webServer: {
    command: `node scripts/serve.mjs --port ${port} --quiet`,
    url: `http://127.0.0.1:${port}/index.html`,
    reuseExistingServer: false,
    timeout: 20_000,
    stdout: 'ignore',
    stderr: 'pipe',
  },
});
