// Chromium flags that make WebGL work headless (SwiftShader software GL), shared by
// playwright.config.mjs and scripts/shots.mjs.
export const CHROMIUM_ARGS = [
  '--use-angle=swiftshader',
  '--enable-unsafe-swiftshader',
  '--ignore-gpu-blocklist',
  '--enable-webgl',
  '--autoplay-policy=no-user-gesture-required',
];

export const VIEWPORT = { width: 1280, height: 720 };

/** Page URL for the game in test mode. */
export function gameUrl(base, params = {}) {
  const q = new URLSearchParams({ test: '1', skip_intro: '1', ...params });
  return `${base.replace(/\/$/, '')}/index.html?${q}`;
}
