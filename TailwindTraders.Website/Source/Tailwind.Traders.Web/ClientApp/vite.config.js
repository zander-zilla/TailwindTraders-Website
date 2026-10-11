import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import svgr from "vite-plugin-svgr";

// Output stays in ClientApp/build so the .NET host (UseStaticFiles on ClientApp/build) and the Dockerfile are unchanged.
// `npm run dev` proxies the API to the ASP.NET Core app (dotnet run / compose on :2000).
const api = process.env.API_PROXY_TARGET || "http://localhost:2000";

export default defineConfig({
  plugins: [react(), svgr()],
  build: { outDir: "build", sourcemap: true, chunkSizeWarningLimit: 1500 },
  server: { port: 3000, proxy: { "/api": api } },
  css: { preprocessorOptions: { scss: { api: "modern-compiler", quietDeps: true, silenceDeprecations: ["import", "global-builtin", "color-functions", "slash-div"] } } },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/setupTests.js"],
    include: ["src/**/*.test.{js,jsx}"],
    css: false,
  },
});
