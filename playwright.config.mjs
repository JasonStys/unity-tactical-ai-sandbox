// File: playwright.config.mjs
// Purpose: Exercise the offline generated game inspector in three browser engines and mobile layout.
// Variables: projects share immutable exports, with independent browser contexts.
import { defineConfig, devices } from "@playwright/test";
export default defineConfig({
  testDir: "tests/browser",
  reporter: "list",
  use: { acceptDownloads: true },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
    { name: "webkit", use: { ...devices["Desktop Safari"] } },
    { name: "mobile", use: { ...devices["iPhone 13"] } },
  ],
});
