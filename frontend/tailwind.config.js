/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
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
