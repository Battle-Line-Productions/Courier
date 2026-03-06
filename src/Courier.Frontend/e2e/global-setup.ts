import type { FullConfig } from "@playwright/test";
import * as fs from "fs";
import * as path from "path";

const API_BASE_URL = process.env.API_URL || "http://localhost:5000";
const FRONTEND_URL = process.env.FRONTEND_URL || "http://localhost:3000";
const MAX_WAIT_MS = 30_000;
const POLL_INTERVAL_MS = 1_000;

export const TEST_ADMIN = {
  username: "testadmin",
  displayName: "Test Admin",
  password: "TestPassword123!",
};

async function waitForService(url: string, label: string): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < MAX_WAIT_MS) {
    try {
      const response = await fetch(url);
      if (response.ok) return;
    } catch {
      // Service not ready yet
    }
    await new Promise((r) => setTimeout(r, POLL_INTERVAL_MS));
  }
  throw new Error(
    `${label} at ${url} did not become ready within ${MAX_WAIT_MS / 1000}s. ` +
      `Start the Aspire stack: cd src/Courier.AppHost && dotnet run`
  );
}

async function globalSetup(config: FullConfig): Promise<void> {
  // Only run admin setup for projects that need it (skip for fresh-db tests)
  const projects = config.projects.map((p) => p.name);
  const isFreshDbOnly =
    projects.length === 1 && projects[0] === "fresh-db";

  console.log("Waiting for API...");
  await waitForService(`${API_BASE_URL}/health`, "API");

  console.log("Waiting for frontend...");
  await waitForService(FRONTEND_URL, "Frontend");

  if (isFreshDbOnly) {
    console.log("Skipping admin setup for fresh-db tests.");
    return;
  }

  // Check if setup is already completed
  const statusResponse = await fetch(`${API_BASE_URL}/api/v1/setup/status`);
  const statusBody = await statusResponse.json();

  if (!statusBody.data?.isCompleted) {
    console.log("Running initial setup...");
    const setupResponse = await fetch(
      `${API_BASE_URL}/api/v1/setup/initialize`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          username: TEST_ADMIN.username,
          displayName: TEST_ADMIN.displayName,
          password: TEST_ADMIN.password,
          confirmPassword: TEST_ADMIN.password,
        }),
      }
    );

    if (!setupResponse.ok) {
      const err = await setupResponse.text();
      throw new Error(`Setup initialization failed: ${err}`);
    }
    console.log("Admin account created.");
  } else {
    // Verify test credentials work against the existing setup
    console.log("Setup already completed. Verifying test credentials...");
    const loginResponse = await fetch(`${API_BASE_URL}/api/v1/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        username: TEST_ADMIN.username,
        password: TEST_ADMIN.password,
      }),
    });
    if (!loginResponse.ok) {
      throw new Error(
        `Test credentials (${TEST_ADMIN.username}) do not work against the existing database. ` +
          `Reset the database by removing the Docker volume: docker volume rm courier-pgdata`
      );
    }
    console.log("Credentials verified.");
  }

  // Save credentials for tests to consume
  const authDir = path.join(__dirname, ".auth");
  if (!fs.existsSync(authDir)) {
    fs.mkdirSync(authDir, { recursive: true });
  }
  fs.writeFileSync(
    path.join(authDir, "credentials.json"),
    JSON.stringify(TEST_ADMIN)
  );
}

export default globalSetup;
