/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      screens: {
        // Tailwind's smallest default breakpoint is 640px, which leaves the 320-430px
        // range every phone actually sits in with no breakpoint of its own. `xs` covers
        // the gap between a small phone in portrait and a large one.
        xs: '400px',
      },
      colors: {
        ink: {
          900: '#0b1220',
          800: '#131c2e',
          700: '#1c2740',
          600: '#27354f',
        },
        sea: {
          400: '#38bdf8',
          500: '#0ea5e9',
          600: '#0284c7',
        },
      },
      fontFamily: {
        mono: ['ui-monospace', 'SFMono-Regular', 'Menlo', 'monospace'],
      },
    },
  },
  plugins: [],
};
