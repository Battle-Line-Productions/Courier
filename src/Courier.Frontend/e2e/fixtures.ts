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
    const { accessToken, refreshToken } = body.data;

    // Inject auth state into the page's localStorage and API client
    await page.goto("/");
    await page.evaluate(
      ({ refreshToken }) => {
        localStorage.setItem("courier_refresh_token", refreshToken);
      },
      { refreshToken }
    );

    // Set the access token via the API client by injecting it into the page context
    await page.evaluate(
      ({ accessToken }) => {
        // Store temporarily so the auth provider can pick it up on next navigation
        sessionStorage.setItem("__e2e_access_token", accessToken);
      },
      { accessToken }
    );

    // Reload to let AuthProvider restore session from the refresh token
    await page.reload();
    await page.waitForURL("/", { timeout: 10_000 });

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
