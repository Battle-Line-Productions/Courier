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
    const loginResponse = await page.request.post(
      `${apiUrl}/api/v1/auth/login`,
      {
        data: {
          username: TEST_ADMIN.username,
          password: TEST_ADMIN.password,
        },
      }
    );
    const body = await loginResponse.json();
    const loginData = body.data;

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
