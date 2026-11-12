// `npm test`: runs every file in tests/ (or --only a,b) against the built sample. Exit code 1 if anything failed.
import { readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as L from './lib.mjs';

const here = dirname(fileURLToPath(import.meta.url));
const onlyArg = process.argv.indexOf('--only');
const only = onlyArg > 0 ? process.argv[onlyArg + 1].split(',') : null;
const files = readdirSync(join(here, 'tests')).filter(f => f.endsWith('.mjs')).sort().filter(f => !only || only.includes(f.replace(/\.mjs$/, '')));

const srv = await L.serve();
const browser = await L.launch();
let failed = 0, total = 0;
for (const f of files) {
  console.log(`\n${f}`);
  const mod = await import('./tests/' + f);
  const suite = new L.Suite(f);
  try { await mod.run({ browser, base: srv.url, suite, L }); } catch (e) { suite.results.push({ title: 'suite setup', ok: false, err: e }); console.log('  FAIL suite setup\n       ' + (e && e.stack || e)); }
  total += suite.results.length; failed += suite.results.filter(r => !r.ok).length;
}
await browser.close(); await srv.close();
console.log(`\n${total - failed}/${total} passed${failed ? `, ${failed} FAILED` : ''}`);
process.exit(failed ? 1 : 0);
