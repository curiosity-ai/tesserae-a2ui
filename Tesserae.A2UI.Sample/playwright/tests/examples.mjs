// Every A2UI example from the specification renders without a console error or a renderer problem, in the default theme.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

export async function run({ browser, base, suite, L }) {
  const index = JSON.parse(readFileSync(join(L.EXAMPLES_DIR, 'index.json'), 'utf8'));
  await suite.test('the gallery lists every example file', async () => {
    L.assert(index.length === 43, 'expected 43 examples, found ' + index.length);
  });

  for (const ex of index) {
    await suite.test(`${ex.group} / ${ex.name} renders`, async () => {
      const page = await L.open(browser, base, ex.name, { theme: 'default' });
      try {
        const problems = await page.locator('.a2ui-example-stage [data-a2ui-problem]').allInnerTexts();
        L.assert(problems.length === 0, 'renderer problems: ' + problems.join(' | '));
        const n = await page.locator('.a2ui-example-stage [data-a2ui-id]').count();
        L.assert(n >= 1, 'nothing rendered');
        const errors = await page.locator('.a2ui-error').allInnerTexts();
        L.assert(errors.length === 0, 'renderer errors: ' + errors.join(' | '));
        // An example's remote images (posters, album art) are other people's servers: a blocked or slow one is not a renderer failure.
        const own = page.__errors.filter(e => !/Failed to load resource/.test(e));
        L.assert(own.length === 0, 'console errors: ' + own.join(' | '));
        await L.shot(page, 'example-' + ex.id + '-default-light');
      } finally { await page.__ctx.close(); }
    });
  }
}
