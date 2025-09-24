/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  theme: {
    extend: {
      colors: {
        red: {
          50: '#ffe6e6',
          100: '#ffcccc',
          600: '#dc2626',
          DEFAULT: '#ff0000',
        },
        orange: {
          50: '#fff7ed',
          100: '#ffedd5',
          500: '#f97316',
        },
        'green-accent': '#377A4E',
        'green-dark': '#004225',
        'brand-orange': '#E89149',
        'text-dark': '#061911',
      },
      fontFamily: {
        'roboto-condensed': ['"Roboto Condensed"', 'sans-serif'],
      },
      keyframes: {
        slideUp: {
          '0%': { transform: 'translateY(20px)', opacity: '0' },
          '100%': { transform: 'translateY(0)', opacity: '1' },
        },
        fadeIn: {
          '0%': { opacity: '0' },
          '100%': { opacity: '1' },
        },
      },
      animation: {
        slideUp: 'slideUp 0.5s ease-out',
        fadeIn: 'fadeIn 0.5s ease-in',
      },
    },
  },
  plugins: [],
};
