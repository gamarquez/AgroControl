import type { Config } from "tailwindcss";

const config: Config = {
  content: [
    "./app/**/*.{ts,tsx}",
    "./components/**/*.{ts,tsx}",
    "./lib/**/*.{ts,tsx}"
  ],
  theme: {
    extend: {
      colors: {
        brand: {
          earth: "#5B4A2F",
          grass: "#647A35",
          sand: "#E9DFC7",
          clay: "#C7763E"
        }
      }
    }
  },
  plugins: []
};

export default config;
