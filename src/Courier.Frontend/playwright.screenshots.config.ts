import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  testMatch: /capture-guide-screenshots\.spec\.ts|fix-screenshots\.spec\.ts|check-nav\.spec\.ts/,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: "list",

  use: {
    baseURL: process.env.FRONTEND_URL || "http://localhost:3000",
    trace: "off",
    screenshot: "off",
  },

  projects: [
    {
      name: "screenshots",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
});
