// The component showcase in every Tesserae theme, light and dark (THEMES=stratus,washi limits the matrix).
// Each capture is named after the theme and mode, so a defect is reported with where it shows up.
export async function run({ browser, base, suite, L }) {
  const only = process.env.THEMES ? process.env.THEMES.split(',') : L.THEMES;
  for (const theme of only) {
    for (const dark of [false, true]) {
      const mode = dark ? 'dark' : 'light';
      await suite.test(`showcase renders in ${theme} ${mode}`, async () => {
        const page = await L.open(browser, base, 'Showcase', { theme, dark, viewport: { width: 1500, height: 2300 } });
        try {
          const own = page.__errors.filter(e => !/Failed to load resource/.test(e));
          L.assert(own.length === 0, 'console errors: ' + own.join(' | '));
          const isDark = await page.evaluate(() => document.body.classList.contains('tss-dark-mode'));
          L.assert(isDark === dark, `body dark-mode class is ${isDark}, expected ${dark}`);
          const n = await page.locator('.a2ui-surface [data-a2ui-id]').count();
          L.assert(n >= 60, 'only ' + n + ' components rendered');
          L.assert(await page.locator('.a2ui-surface [data-a2ui-problem]').count() === 0, 'a component reported a problem');
          // The text of a Text component must not be the same colour as what it sits on.
          const same = await page.evaluate(() => {
            const el = document.querySelector('[data-a2ui-id="t_body"]');
            const fg = getComputedStyle(el).color;
            let bg = 'rgba(0, 0, 0, 0)';
            for (let e = el; e; e = e.parentElement) { const b = getComputedStyle(e).backgroundColor; if (b !== 'rgba(0, 0, 0, 0)' && b !== 'transparent') { bg = b; break; } }
            return fg === bg;
          });
          L.assert(!same, 'text has the same colour as its background');
          await page.locator('.a2ui-surface').first().screenshot({ path: L.SCREENS + `/showcase-${theme}-${mode}.png` });
        } finally { await page.__ctx.close(); }
      });

      await suite.test(`more components render in ${theme} ${mode}`, async () => {
        const page = await L.open(browser, base, 'More Components', { theme, dark, viewport: { width: 1500, height: 2300 } });
        try {
          await page.locator('.a2ui-surface').first().getByText('Step index: 0').waitFor({ timeout: 8000 });
          const own = page.__errors.filter(e => !/Failed to load resource/.test(e));
          L.assert(own.length === 0, 'console errors: ' + own.join(' | '));
          L.assert(await page.locator('.a2ui-surface [data-a2ui-problem]').count() === 0, 'a component reported a problem');
          await page.locator('.a2ui-surface').first().screenshot({ path: L.SCREENS + `/more-${theme}-${mode}.png` });
        } finally { await page.__ctx.close(); }
      });

      await suite.test(`dashboard components render in ${theme} ${mode}`, async () => {
        const page = await L.open(browser, base, 'Dashboard Components', { theme, dark, viewport: { width: 1500, height: 1500 } });
        try {
          await page.locator('.a2ui-surface').first().getByText('$9,200.50').waitFor({ timeout: 8000 });
          const own = page.__errors.filter(e => !/Failed to load resource/.test(e));
          L.assert(own.length === 0, 'console errors: ' + own.join(' | '));
          L.assert(await page.locator('.a2ui-surface [data-a2ui-problem]').count() === 0, 'a component reported a problem');
          await page.locator('.a2ui-surface').first().screenshot({ path: L.SCREENS + `/dashboard-${theme}-${mode}.png` });
        } finally { await page.__ctx.close(); }
      });
    }
  }
}
