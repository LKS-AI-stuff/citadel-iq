/** @type {import('tailwindcss').Config} */
// Tailwind v4 auto-detects content and no longer reads most of this file without an explicit
// `@config` directive; it's kept only for editor tooling. Preflight is disabled in src/index.css
// instead, by importing tailwindcss/theme.css + utilities.css directly (the only way to opt out
// of Preflight in v4 — there's no corePlugins.preflight flag anymore).
export default {
  content: [
    './index.html',
    './src/**/*.{js,ts,jsx,tsx}',
  ],
  theme: {
    extend: {},
  },
  plugins: [],
};
