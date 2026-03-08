import { test as base, type Page, type APIRequestContext } from "@playwright/test";
import { TEST_ADMIN } from "./global-setup";
import * as apiHelpers from "./helpers/api-helpers";

type CourierFixtures = {
  authenticatedPage: Page;
  apiBaseUrl: string;
  apiHelper: typeof apiHelpers & { request: APIRequestContext };
};

export const test = base.extend<CourierFixtures>({
  authenticatedPage: async ({ browser }, use) => {
    const context = await browser.newContext();
    const page = await context.newPage();

    // Perform a fresh login to get a valid session (avoids refresh token rotation conflicts)
    const apiUrl = process.env.API_URL || "http://localhost:5000";
    let loginData: any;
    let lastError = "";
    for (let attempt = 0; attempt < 5; attempt++) {
      const loginResponse = await page.request.post(
        `${apiUrl}/api/v1/auth/login`,
        {
          data: {
            username: TEST_ADMIN.username,
            password: TEST_ADMIN.password,
          },
        }
      );

      if (loginResponse.status() === 429) {
        const retryAfter = parseInt(loginResponse.headers()["retry-after"] || "2", 10);
        lastError = `429 (attempt ${attempt + 1})`;
        await new Promise((r) => setTimeout(r, retryAfter * 1000));
        continue;
      }

      if (!loginResponse.ok()) {
        lastError = `${loginResponse.status()} (attempt ${attempt + 1})`;
        await new Promise((r) => setTimeout(r, 1000));
        continue;
      }

      try {
        const body = await loginResponse.json();
        loginData = body.data;
        break;
      } catch {
        lastError = `JSON parse failed with status ${loginResponse.status()} (attempt ${attempt + 1})`;
        await new Promise((r) => setTimeout(r, 1000));
        continue;
      }
    }

    if (!loginData) {
      throw new Error(`authenticatedPage: login failed after 5 retries: ${lastError}`);
    }

    // Intercept the refresh endpoint so restoreSession() gets an instant response
    // instead of hitting the real API (avoids contention under parallel workers)
    await page.route("**/api/v1/auth/refresh", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          data: loginData,
          error: null,
          timestamp: new Date().toISOString(),
        }),
      });
    });

    // Inject auth state into the page's localStorage
    await page.goto("/");
    await page.evaluate(
      ({ refreshToken }) => {
        localStorage.setItem("courier_refresh_token", refreshToken);
      },
      { refreshToken: loginData.refreshToken }
    );

    // Reload to let AuthProvider restore session from the intercepted refresh
    await page.reload();
    await page.waitForURL("/", { timeout: 15_000 });

    // Wait for AuthProvider to fully restore the session (user menu appears when authenticated)
    await page.locator("header").getByRole("button").waitFor({ timeout: 15_000 });

    await use(page);
    await context.close();
  },

  apiBaseUrl: async ({}, use) => {
    await use(process.env.API_URL || "http://localhost:5000");
  },

  apiHelper: async ({ playwright }, use) => {
    const request = await playwright.request.newContext();
    const helper = { ...apiHelpers, request };
    await use(helper);
    await request.dispose();
  },
});

export { expect } from "@playwright/test";
