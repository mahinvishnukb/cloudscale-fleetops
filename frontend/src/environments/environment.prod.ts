/**
 * Production API origin.
 *
 * Vercel builds are static, so there is no server-side env var at runtime — the value is
 * baked in at build time. Set FLEETOPS_API_URL in the Vercel project and let the
 * `prebuild` step rewrite this file, or simply edit the constant before deploying.
 */
export const environment = {
  production: true,
  apiBaseUrl: 'https://fleetops-api-q9bg.onrender.com',
};
