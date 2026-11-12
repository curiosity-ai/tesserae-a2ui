// Shared helpers for the suite: a static server for the built sample, Chromium, page helpers that open a gallery
// page in a theme, and a tiny test runner.
import { createRequire } from 'node:module';
import { existsSync, mkdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import http from 'node:http';
import { readFile, stat } from 'node:fs/promises';

const here = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
function loadPlaywright() {
  for (const p of ['playwright', process.env.NODE_PATH && join(process.env.NODE_PATH.split(':')[0], 'playwright'), '/opt/node-tools/node_modules/playwright']) {
    if (!p) continue; try { return require(p); } catch (e) { /* next */ }
  }
  throw new Error('playwright is not installed: run `npm install` here (and `npx playwright install chromium`), or set NODE_PATH');
}
export const { chromium } = loadPlaywright();

export const SCREENS = process.env.SCREENS_DIR || resolve(here, 'screens');
mkdirSync(SCREENS, { recursive: true });
const CONFIG = process.env.SAMPLE_CONFIG || 'Debug';
export const SITE = process.env.SAMPLE_DIR || resolve(here, '..', 'bin', CONFIG, 'netstandard2.0', 'tps');
export const EXAMPLES_DIR = join(SITE, 'assets', 'examples');

/** The themes of the matrix, as the gallery's `?theme=` ids (the Default theme is "default"). */
export const THEMES = ['default', 'curiosity', 'stratus', 'retroos', 'rechenwerk', 'weniger', 'zeitung', 'blaupause', 'phosphor', 'glas', 'kristall', 'kontrast', 'memphis', 'washi', 'graustufe'];

const MIME = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.woff2': 'font/woff2', '.woff': 'font/woff', '.map': 'application/json' };

/** A tiny static server (no python needed); returns { url, close }. */
export async function serve(dir = SITE) {
  if (!existsSync(join(dir, 'index.html'))) throw new Error('No built sample in ' + dir + ': dotnet build Tesserae.A2UI.Sample');
  const server = http.createServer(async (req, res) => {
    try {
      let p = decodeURIComponent(new URL(req.url, 'http://x').pathname); if (p.endsWith('/')) p += 'index.html';
      const f = join(dir, p); if (!f.startsWith(dir)) { res.writeHead(403).end(); return; }
      const s = await stat(f); const ext = f.slice(f.lastIndexOf('.'));
      res.writeHead(200, { 'content-type': MIME[ext] || 'application/octet-stream', 'content-length': s.size, 'cache-control': 'no-store' });
      res.end(await readFile(f));
    } catch (e) { res.writeHead(404).end('not found'); }
  });
  await new Promise(r => server.listen(0, '127.0.0.1', r));
  return { url: 'http://127.0.0.1:' + server.address().port, close: () => new Promise(r => server.close(r)) };
}

export async function launch() { return chromium.launch(); }

/**
 * Opens a gallery route with a theme. Console errors are collected on page.__errors; the Sandbox-style noise Tesserae's own gallery
 * documents does not exist here, so any console error is a failure for the tests that check it.
 */
export async function open(browser, base, route, opts = {}) {
  const ctx = await browser.newContext({ viewport: opts.viewport || { width: 1400, height: 900 }, hasTouch: !!opts.touch, isMobile: !!opts.touch, deviceScaleFactor: opts.dpr || 1, reducedMotion: opts.reducedMotion ? 'reduce' : 'no-preference' });
  const page = await ctx.newPage();
  page.__errors = [];
  page.on('console', m => { if (m.type() === 'error') page.__errors.push(m.text()); });
  page.on('pageerror', e => page.__errors.push('pageerror: ' + e.message));
  const q = new URLSearchParams({ theme: opts.theme || 'default', dark: opts.dark ? '1' : '0' });
  await page.goto(`${base}/index.html?${q}#/view/${encodeURIComponent(route)}`);
  page.__ctx = ctx;
  if (!opts.noWait) await ready(page);
  return page;
}

/** Waits until the page's surface has drawn something (a data-a2ui-id element) or the page shows a problem. */
export async function ready(page, timeout = 20000) {
  await page.waitForSelector('.a2ui-surface [data-a2ui-id], .a2ui-surface [data-a2ui-problem]', { timeout });
  await sleep(900);   // the sections fade in; nothing is read or captured before that is over
}

export const sleep = ms => new Promise(r => setTimeout(r, ms));

export async function shot(page, name) { const f = join(SCREENS, name.endsWith('.png') ? name : name + '.png'); await page.screenshot({ path: f }); return f; }

/** The surface element of the first (or nth) host on the page. */
export const surface = (page, n = 0) => page.locator('.a2ui-surface').nth(n);

/** Text of the pane of the inspector tab currently shown. */
export async function inspectorText(page) { return page.locator('.tss-pivot-content, [class*="pivot"] [role="tabpanel"]').first().innerText().catch(() => ''); }

export class Suite {
  constructor(name) { this.name = name; this.results = []; }
  async test(title, fn) {
    const t0 = Date.now();
    try { await fn(); this.results.push({ title, ok: true, ms: Date.now() - t0 }); console.log(`  ok   ${title}`); }
    catch (e) { this.results.push({ title, ok: false, ms: Date.now() - t0, err: e }); console.log(`  FAIL ${title}\n       ${process.env.FULL_ERR ? String(e && e.message || e).split('\n').join('\n       ') : String(e && e.message || e).split('\n')[0]}`); }
  }
}
export function assert(c, msg) { if (!c) throw new Error(msg || 'assertion failed'); }
