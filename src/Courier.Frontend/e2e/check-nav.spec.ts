import { test } from "@playwright/test";
import path from "path";

const SCREENSHOT_DIR = path.resolve(__dirname, "../public/guide/screenshots");
const CREDENTIALS = {
  username: process.env.GUIDE_USER || "admin",
  password: process.env.GUIDE_PASS || "!Cc20080754",
};

test("check guide nav on jobs page", async ({ page }) => {
  test.setTimeout(60_000);
  page.setViewportSize({ width: 1440, height: 900 });
  await page.goto("/login");
  await page.getByLabel("Username").waitFor({ state: "visible", timeout: 15_000 });
  await page.getByLabel("Username").fill(CREDENTIALS.username);
  await page.getByLabel("Password").fill(CREDENTIALS.password);
  await page.getByRole("button", { name: "Sign In" }).click();
  await page.waitForURL("/", { timeout: 15_000 });
  await page.waitForLoadState("networkidle");

  await page.goto("/guide/jobs");
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(2000);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, "_debug-guide-jobs-nav.png") });

  await page.goto("/guide/connections");
  await page.waitForLoadState("networkidle");
  await page.waitForTimeout(2000);
  await page.screenshot({ path: path.join(SCREENSHOT_DIR, "_debug-guide-connections-nav.png") });
});
