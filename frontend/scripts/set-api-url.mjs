/**
 * Bakes the API origin into environment.prod.ts before a production build.
 *
 * An Angular build is static: there is no server at runtime to read an environment
 * variable from, so the origin has to be substituted at build time. Vercel exposes
 * project environment variables to the build step, which is where this runs.
 *
 * Set FLEETOPS_API_URL in the Vercel project (Settings -> Environment Variables).
 * With it unset the committed default is left untouched, so a local
 * `npm run build:prod` still works.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, resolve } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const target = resolve(here, '../src/environments/environment.prod.ts');

const raw = process.env.FLEETOPS_API_URL?.trim();

if (!raw) {
  console.log('[set-api-url] FLEETOPS_API_URL not set; keeping the committed default.');
  process.exit(0);
}

let url;
try {
  url = new URL(raw);
} catch {
  console.error(`[set-api-url] FLEETOPS_API_URL is not a valid URL: ${raw}`);
  process.exit(1);
}

if (url.protocol !== 'https:' && url.hostname !== 'localhost') {
  // A non-HTTPS origin would make the browser block the SignalR upgrade from a
  // page served over HTTPS, and the failure surfaces as an opaque mixed-content
  // error rather than anything that names the cause.
  console.error(`[set-api-url] refusing a non-HTTPS origin: ${raw}`);
  process.exit(1);
}

// Trailing slashes produce '//api/vessels' once joined with the paths in the services.
const origin = url.origin;

const source = readFileSync(target, 'utf8');
const pattern = /(apiBaseUrl:\s*)'[^']*'/;

// Test for the pattern separately from testing whether the content changed. An
// already-correct value produces an identical string, and treating that as
// "pattern not found" fails the build for a file that was perfectly fine.
if (!pattern.test(source)) {
  console.error('[set-api-url] could not find apiBaseUrl in environment.prod.ts');
  process.exit(1);
}

const updated = source.replace(pattern, `$1'${origin}'`);

if (updated === source) {
  console.log(`[set-api-url] apiBaseUrl already ${origin}; nothing to do.`);
  process.exit(0);
}

writeFileSync(target, updated);
console.log(`[set-api-url] apiBaseUrl set to ${origin}`);
