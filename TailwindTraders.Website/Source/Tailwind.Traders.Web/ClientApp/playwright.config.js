import { defineConfig, devices } from "@playwright/test";

// Smoke E2E against a running stack (compose/kind): BASE_URL defaults to http://localhost:2000.
export default defineConfig({
  testDir: "./e2e",
  timeout: 60_000,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : "list",
  use: { baseURL: process.env.BASE_URL || "http://localhost:2000", trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
