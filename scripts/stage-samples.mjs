/**
 * Stage the compiled sample into `_site/` for publishing to GitHub Pages.
 *
 * The Transpose output folder is already a complete, self-contained site - `tps.json` generates
 * an index.html referencing every script relatively, so staging is a copy plus two small fixes:
 *
 *   - `.tps-manifest.<app>.json` is dropped. It is the compiler's own record of what it wrote
 *     (used to clean the output folder on the next build), not something the page fetches.
 *   - `.nojekyll` is written, so GitHub serves every path verbatim rather than running the
 *     content through Jekyll (which drops files and folders beginning with `_`).
 *
 * Deliberately not rewritten: nothing needs a <base> or an absolute-path fixup for the
 * `/tesserae-a2ui/` sub-path Pages serves from. Every reference in index.html is relative,
 * and 
 *
 * Usage: node scripts/stage-samples.mjs [source-dir] [dest-dir]
 * Cross-platform, no dependencies. Run after `dotnet build ... -c Release`.
 */
import { rmSync, cpSync, existsSync, writeFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const SOURCE = process.argv[2] ?? 'Tesserae.A2UI.Sample/bin/Release/netstandard2.0/tps';
const DEST = process.argv[3] ?? '_site';

if (!existsSync(join(SOURCE, 'index.html'))) {
  console.error(
    `No index.html in ${SOURCE} - build the sample first:\n` +
    '  dotnet build Tesserae.A2UI.Sample/Tesserae.A2UI.Sample.csproj -c Release'
  );
  process.exit(1);
}

// The gallery fetches its examples by URL; a site without them lists pages that can never load. They are copied by the
// sample's own _CopySampleExamples target after the compiler has written the rest, so a missing folder is a half-built site.
if (!existsSync(join(SOURCE, 'assets/examples/index.json'))) {
  console.error(`No assets/examples/index.json in ${SOURCE} - the sample example copy did not run.`);
  process.exit(1);
}

rmSync(DEST, { recursive: true, force: true });
cpSync(SOURCE, DEST, { recursive: true });

for (const entry of readdirSync(DEST)) {
  if (entry.startsWith('.tps-manifest.') && entry.endsWith('.json')) {
    rmSync(join(DEST, entry));
  }
}

writeFileSync(join(DEST, '.nojekyll'), '');

console.log(`Staged ${SOURCE} -> ${DEST}/`);
