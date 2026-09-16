import { defineConfig, devices } from "@playwright/test";

const isolatedApiUrl = process.env.PADELBOOKING_E2E_API_URL;
const isolatedMode = process.env.PADELBOOKING_E2E_ISOLATED === "1";
const frontendPort = process.env.PADELBOOKING_E2E_FRONTEND_PORT || "5189";
const frontendUrl = `http://localhost:${frontendPort}`;
const reuseTestServer = process.env.PADELBOOKING_E2E_REUSE_TEST_SERVER === "1";

if (reuseTestServer && frontendPort !== "5189") {
  throw new Error("Izolovani E2E server mora koristiti port 5189.");
}

if (!isolatedMode || isolatedApiUrl !== "http://localhost:5248/api") {
  throw new Error("Pokreni izolovane E2E testove komandom npm run test:e2e:isolated.");
}

export default defineConfig({
  testDir: "./e2e",
  timeout: 30_000,
  retries: 0,
  use: { baseURL: frontendUrl, trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: {
    command: `node node_modules/vite/bin/vite.js --host 0.0.0.0 --port ${frontendPort} --strictPort`,
    url: frontendUrl,
    reuseExistingServer: reuseTestServer,
    timeout: 30_000,
    env: {
      VITE_API_URL: isolatedApiUrl,
    },
  },
});
