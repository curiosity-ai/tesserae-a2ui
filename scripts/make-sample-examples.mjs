/**
 * Copies the A2UI basic-catalog examples (v1.0) out of the upstream snapshot in `.reference/` into the sample
 * gallery's assets and writes `index.json`, which says how the gallery groups them.
 *
 * The examples themselves are never edited: they are the upstream protocol samples, and the gallery's job is to
 * show that the renderer draws every one of them. Run it again after refreshing `.reference/`.
 *
 * Usage: node scripts/make-sample-examples.mjs
 */
import { readdirSync, readFileSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const SRC = join(root, '.reference', 'catalogs', 'basic', 'v1', 'examples');
const DEST = join(root, 'Tesserae.A2UI.Sample', 'assets', 'examples');

// Which gallery group each example belongs to, by its file number. Anything not listed goes to "Cards".
const GROUPS = [
  ['Basics',               ['00_simple-text', '00_row-layout', '00_complex-layout', '00_formatted-text', '00_interactive-button', '35_markdown-text', '36_modal']],
  ['Forms and validation', ['00_simple-login-form', '09_login-form', '02_email-compose', '10_notification-permission', '07_task-card', '19_software-purchase', '22_credit-card', '32_advanced-form-validator', '30_live-invitation-builder']],
  ['Lists and templates',  ['34_child-list-template', '03_calendar-day', '04_weather-current', '12_chat-message', '13_coffee-order', '18_track-list', '21_shipping-status', '33_financial-data-grid']],
  ['Streaming',            ['00_incremental', '31_incremental-dashboard']],
  ['Media',                ['06_music-player', '26_podcast-episode', '29_movie-card']],
];
const ORDER = ['Basics', 'Forms and validation', 'Lists and templates', 'Cards', 'Streaming', 'Media'];

rmSync(DEST, { recursive: true, force: true });
mkdirSync(DEST, { recursive: true });

const index = [];
for (const file of readdirSync(SRC).filter(f => f.endsWith('.json')).sort()) {
  const id = file.replace(/\.json$/, '');
  const text = readFileSync(join(SRC, file), 'utf8');
  const doc = JSON.parse(text);
  const group = (GROUPS.find(([, ids]) => ids.includes(id)) || ['Cards'])[0];
  const surfaces = [...new Set(doc.messages.map(m => Object.values(m).find(v => v && typeof v === 'object' && v.surfaceId)?.surfaceId).filter(Boolean))];
  const components = new Set();
  for (const m of doc.messages) {
    const list = m.createSurface?.components || m.updateComponents?.components || [];
    for (const c of list) components.add(c.component);
  }
  index.push({
    id, name: doc.name, description: doc.description, group,
    messages: doc.messages.length, surfaces, components: [...components].sort(),
    streaming: doc.messages.length > 3 && doc.messages.some(m => m.updateComponents) ,
  });
  writeFileSync(join(DEST, file), text);
}

index.sort((a, b) => ORDER.indexOf(a.group) - ORDER.indexOf(b.group)
  || (GROUPS.find(([g]) => g === a.group)?.[1].indexOf(a.id) ?? 0) - (GROUPS.find(([g]) => g === b.group)?.[1].indexOf(b.id) ?? 0)
  || a.id.localeCompare(b.id));

writeFileSync(join(DEST, 'index.json'), JSON.stringify(index, null, 2) + '\n');
console.log(`Wrote ${index.length} examples to ${DEST}`);
