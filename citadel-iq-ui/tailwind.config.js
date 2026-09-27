/** @type {import('tailwindcss').Config} */
export default {
  content: [
    './index.html',
    './src/**/*.{js,ts,jsx,tsx}',
  ],
  corePlugins: {
    // MUI already provides a CSS reset (CssBaseline); avoid Tailwind's preflight fighting it.
    preflight: false,
  },
  theme: {
    extend: {},
  },
  plugins: [],
};
