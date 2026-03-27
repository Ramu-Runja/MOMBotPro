/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,jsx,ts,tsx}"],
  darkMode: "class",
  theme: {
    extend: {
      colors: {
        bg:       "#0A0C12",
        surface:  "#13161F",
        surface2: "#1c2030",
        border:   "#252a3d",
        accent:   "#6C63FF",
        green:    "#00D4AA",
        orange:   "#FF8800",
        red:      "#FF5C5C",
        txt:      "#e8eaf0",
        muted:    "#7c829a",
      },
      fontFamily: {
        sans:    ["DM Sans", "sans-serif"],
        display: ["Syne", "sans-serif"],
      },
    },
  },
  plugins: [],
};
